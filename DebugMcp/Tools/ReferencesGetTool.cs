using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Memory;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for analyzing object references.
/// </summary>
[McpServerToolType]
public sealed class ReferencesGetTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<ReferencesGetTool> _logger;

    public ReferencesGetTool(IDebugSessionManager sessionManager, ILogger<ReferencesGetTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Analyze object references - find what objects a target references (outbound).
    /// </summary>
    /// <param name="object_ref">Object reference (variable name or expression).</param>
    /// <param name="direction">Reference direction: 'outbound' (default), 'inbound', 'both'. Note: inbound not yet implemented.</param>
    /// <param name="max_results">Maximum references to return (default: 50, max: 100).</param>
    /// <param name="include_arrays">Include array element references (default: true).</param>
    /// <param name="thread_id">Thread ID (default: current thread).</param>
    /// <param name="frame_index">Frame index (0 = top of stack, default: 0).</param>
    /// <returns>Reference analysis with outbound object references.</returns>
    [McpServerTool(Name = "references_get", Title = "Get Object References",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Analyze object references - find what objects a target references")]
    public async Task<ReferencesGetResult> GetReferences(
        [Description("Object reference (variable name or expression)")] string object_ref,
        [Description("Reference direction: outbound, inbound, both")] string direction = "outbound",
        [Description("Maximum references to return (max: 100)")] int max_results = 50,
        [Description("Include array element references")] bool include_arrays = true,
        [Description("Thread ID (default: current thread)")] int? thread_id = null,
        [Description("Frame index (0 = top of stack)")] int frame_index = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("references_get",
            $"{{\"object_ref\": \"{object_ref}\", \"direction\": \"{direction}\", \"max_results\": {max_results}, \"include_arrays\": {include_arrays.ToString().ToLowerInvariant()}}}");

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(object_ref))
            {
                return new ReferencesGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "object_ref cannot be empty", new { parameter = "object_ref" }));
            }

            string[] validDirections = ["outbound", "inbound", "both"];
            if (!validDirections.Contains(direction))
            {
                return new ReferencesGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter,
                    $"direction must be one of: {string.Join(", ", validDirections)}",
                    new { parameter = "direction", value = direction, validValues = validDirections }));
            }

            if (max_results < 1)
            {
                return new ReferencesGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "max_results must be >= 1", new { parameter = "max_results", value = max_results }));
            }

            if (max_results > 100)
            {
                max_results = 100; // Clamp to max
            }

            if (frame_index < 0)
            {
                return new ReferencesGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "frame_index must be >= 0", new { parameter = "frame_index", value = frame_index }));
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("references_get", ErrorCodes.NoSession);
                return new ReferencesGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, "No active debug session"));
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("references_get", ErrorCodes.NotPaused);
                return new ReferencesGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.NotPaused,
                    $"Cannot get references: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() }));
            }

            // Get references (currently only outbound is supported)
            var references = await _sessionManager.GetOutboundReferencesAsync(
                object_ref, include_arrays, max_results, thread_id, frame_index, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("references_get", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Found {Count} outbound references for '{ObjectRef}'",
                references.OutboundCount, object_ref);

            var (boundedOutbound, outboundTruncation) = ResultTruncation.Bound(
                references.Outbound.ToList(), "references_get result exceeded the 256 KB size budget");

            var info = new ReferencesInfo(
                TargetAddress: references.TargetAddress,
                TargetType: references.TargetType,
                Outbound: boundedOutbound,
                OutboundCount: references.OutboundCount,
                Truncated: references.Truncated,
                Inbound: direction is "inbound" or "both" ? Array.Empty<ReferenceInfo>() : null,
                InboundCount: direction is "inbound" or "both" ? 0 : null,
                InboundNote: direction is "inbound" or "both" ? "Inbound reference analysis is not yet implemented" : null);

            return new ReferencesGetResult(Success: true, References: info, Truncation: outboundTruncation);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("references_get", ErrorCodes.NoSession);
            return new ReferencesGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("references_get", ErrorCodes.NotPaused);
            return new ReferencesGetResult(Success: false, Error: new ToolError(ErrorCodes.NotPaused, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid reference"))
        {
            _logger.ToolError("references_get", ErrorCodes.InvalidReference);
            return new ReferencesGetResult(Success: false, Error: new ToolError(
                ErrorCodes.InvalidReference, ex.Message, new { object_ref }));
        }
        catch (Exception ex)
        {
            _logger.ToolError("references_get", "REFERENCE_ANALYSIS_FAILED");
            _logger.LogError(ex, "Reference analysis failed for '{ObjectRef}'", object_ref);
            return new ReferencesGetResult(Success: false, Error: new ToolError(
                "REFERENCE_ANALYSIS_FAILED", $"Failed to analyze references: {ex.Message}"));
        }
    }
}
