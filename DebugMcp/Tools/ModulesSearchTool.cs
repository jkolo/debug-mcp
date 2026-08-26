using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Modules;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for searching types and methods across all loaded modules.
/// </summary>
[McpServerToolType]
public sealed class ModulesSearchTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<ModulesSearchTool> _logger;

    public ModulesSearchTool(
        IDebugSessionManager sessionManager,
        IProcessDebugger processDebugger,
        ILogger<ModulesSearchTool> logger)
    {
        _sessionManager = sessionManager;
        _processDebugger = processDebugger;
        _logger = logger;
    }

    /// <summary>
    /// Search for types and methods across all loaded modules.
    /// </summary>
    /// <param name="pattern">Search pattern (supports * wildcard). Examples: *Customer*, Get*, *Service.</param>
    /// <param name="search_type">What to search: types, methods, or both.</param>
    /// <param name="module_filter">Limit search to specific module (supports * wildcard).</param>
    /// <param name="case_sensitive">Enable case-sensitive matching.</param>
    /// <param name="max_results">Maximum results to return (max: 100).</param>
    /// <returns>Search results with matching types and/or methods.</returns>
    [McpServerTool(Name = "modules_search", Title = "Search Modules",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Search for types and methods across all loaded modules")]
    public async Task<ModulesSearchResult> SearchModules(
        [Description("Search pattern (supports * wildcard)")] string pattern,
        [Description("What to search: types, methods, or both")] string search_type = "both",
        [Description("Limit search to specific module (supports * wildcard)")] string? module_filter = null,
        [Description("Enable case-sensitive matching")] bool case_sensitive = false,
        [Description("Maximum results to return (max: 100)")] int max_results = 50,
        [Description("Maximum time to wait for the module search, in milliseconds (default: 30000)")] int timeout_ms = 30000,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("modules_search",
            $"{{\"pattern\": \"{pattern}\", \"search_type\": \"{search_type}\", \"module_filter\": {(module_filter == null ? "null" : $"\"{module_filter}\"")}, \"max_results\": {max_results}}}");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout_ms));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return CreateErrorResult(ErrorCodes.InvalidPattern,
                    "pattern cannot be empty",
                    new { parameter = "pattern" });
            }

            // Parse search type
            SearchType searchType;
            switch (search_type.ToLowerInvariant())
            {
                case "types":
                    searchType = SearchType.Types;
                    break;
                case "methods":
                    searchType = SearchType.Methods;
                    break;
                case "both":
                    searchType = SearchType.Both;
                    break;
                default:
                    return CreateErrorResult(ErrorCodes.InvalidParameter,
                        $"Invalid search_type value: {search_type}. Valid values: types, methods, both",
                        new { parameter = "search_type", value = search_type, validValues = new[] { "types", "methods", "both" } });
            }

            // Validate max_results
            if (max_results <= 0 || max_results > 100)
            {
                max_results = Math.Clamp(max_results, 1, 100);
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("modules_search", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Module search works with running or paused process (metadata only)
            _logger.LogDebug("Searching modules for pattern '{Pattern}'", pattern);

            var result = await _processDebugger.SearchModulesAsync(
                pattern,
                searchType,
                module_filter,
                case_sensitive,
                max_results,
                linkedCts.Token);

            stopwatch.Stop();
            _logger.ToolCompleted("modules_search", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Found {TotalMatches} matches for pattern '{Pattern}' (returned {ReturnedMatches})",
                result.TotalMatches, pattern, result.ReturnedMatches);

            var typeList = result.Types.Select(t => new ModulesSearchTypeMatch(
                FullName: t.FullName,
                Name: t.Name,
                Namespace: t.Namespace,
                Kind: t.Kind.ToString().ToLowerInvariant(),
                Visibility: t.Visibility.ToString().ToLowerInvariant(),
                ModuleName: t.ModuleName)).ToList();

            var methodList = result.Methods.Select(m => new ModulesSearchMethodMatch(
                DeclaringType: m.DeclaringType,
                ModuleName: m.ModuleName,
                MatchReason: m.MatchReason,
                Method: new ModulesSearchMethodDetail(
                    Name: m.Method.Name,
                    Signature: m.Method.Signature,
                    ReturnType: m.Method.ReturnType,
                    Visibility: m.Method.Visibility.ToString().ToLowerInvariant(),
                    IsStatic: m.Method.IsStatic))).ToList();

            const int perCollectionBudget = ResultTruncation.DefaultBudgetBytes / 2;
            var (boundedTypes, typesTruncation) = ResultTruncation.Bound(
                typeList, "types exceeded its share of the 256 KB size budget", perCollectionBudget);
            var (boundedMethods, methodsTruncation) = ResultTruncation.Bound(
                methodList, "methods exceeded its share of the 256 KB size budget", perCollectionBudget);

            var truncations = new[] { typesTruncation, methodsTruncation }
                .Where(t => t is not null).Cast<TruncationInfo>().ToList();
            var combinedTruncation = truncations.Count == 0
                ? null
                : new TruncationInfo(
                    Returned: truncations.Sum(t => t.Returned),
                    Available: truncations.Sum(t => t.Available ?? 0),
                    Reason: string.Join("; ", truncations.Select(t => t.Reason)));

            return new ModulesSearchResult(
                Success: true,
                Query: result.Query,
                SearchType: result.SearchType.ToString().ToLowerInvariant(),
                Types: boundedTypes,
                Methods: boundedMethods,
                TotalMatches: result.TotalMatches,
                ReturnedMatches: combinedTruncation is null ? result.ReturnedMatches : boundedTypes.Count + boundedMethods.Count,
                Truncated: result.Truncated || combinedTruncation is not null,
                ContinuationToken: result.ContinuationToken,
                Truncation: combinedTruncation);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not attached"))
        {
            _logger.ToolError("modules_search", ErrorCodes.NoSession);
            return CreateErrorResult(ErrorCodes.NoSession, ex.Message);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.ToolError("modules_search", ErrorCodes.Timeout);
            return CreateErrorResult(ErrorCodes.Timeout, $"modules_search timed out after {timeout_ms}ms", new { timeout = timeout_ms });
        }
        catch (Exception ex)
        {
            _logger.ToolError("modules_search", ErrorCodes.SearchFailed);
            _logger.LogError(ex, "Module search failed for pattern '{Pattern}'", pattern);
            return CreateErrorResult(ErrorCodes.SearchFailed,
                $"Search failed: {ex.Message}");
        }
    }

    private static ModulesSearchResult CreateErrorResult(string code, string message, object? details = null)
        => new(Success: false, Error: new ToolError(code, message, details));
}
