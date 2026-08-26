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
/// MCP tool for inspecting type members (methods, properties, fields, events).
/// </summary>
[McpServerToolType]
public sealed class MembersGetTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<MembersGetTool> _logger;

    public MembersGetTool(
        IDebugSessionManager sessionManager,
        IProcessDebugger processDebugger,
        ILogger<MembersGetTool> logger)
    {
        _sessionManager = sessionManager;
        _processDebugger = processDebugger;
        _logger = logger;
    }

    /// <summary>
    /// Get members (methods, properties, fields, events) of a type.
    /// </summary>
    /// <param name="type_name">Full type name to inspect (e.g., 'System.String' or 'MyApp.Models.Customer').</param>
    /// <param name="module_name">Module containing the type (optional, searches all if omitted).</param>
    /// <param name="include_inherited">Include inherited members from base types.</param>
    /// <param name="member_kinds">Comma-separated list of member kinds to include: methods, properties, fields, events.</param>
    /// <param name="visibility">Filter by visibility: public, internal, private, protected.</param>
    /// <param name="include_static">Include static members.</param>
    /// <param name="include_instance">Include instance members.</param>
    /// <returns>Type members with methods, properties, fields, and events.</returns>
    [McpServerTool(Name = "members_get", Title = "Get Type Members",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get members (methods, properties, fields, events) of a type")]
    public async Task<MembersGetResult> GetMembers(
        [Description("Full type name to inspect")] string type_name,
        [Description("Module containing the type (optional)")] string? module_name = null,
        [Description("Include inherited members from base types")] bool include_inherited = false,
        [Description("Comma-separated list of member kinds: methods, properties, fields, events")] string? member_kinds = null,
        [Description("Filter by visibility: public, internal, private, protected")] string? visibility = null,
        [Description("Include static members")] bool include_static = true,
        [Description("Include instance members")] bool include_instance = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("members_get",
            $"{{\"type_name\": \"{type_name}\", \"module_name\": {(module_name == null ? "null" : $"\"{module_name}\"")}, \"include_inherited\": {include_inherited.ToString().ToLowerInvariant()}}}");

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(type_name))
            {
                return new MembersGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "type_name cannot be empty", new { parameter = "type_name" }));
            }

            // Parse member kinds
            string[]? memberKindsArray = null;
            if (!string.IsNullOrEmpty(member_kinds))
            {
                memberKindsArray = member_kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var validKinds = new[] { "methods", "properties", "fields", "events" };
                foreach (var kind in memberKindsArray)
                {
                    if (!validKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
                    {
                        return new MembersGetResult(Success: false, Error: new ToolError(
                            ErrorCodes.InvalidParameter,
                            $"Invalid member_kinds value: {kind}. Valid values: methods, properties, fields, events",
                            new { parameter = "member_kinds", value = kind, validValues = validKinds }));
                    }
                }
            }

            // Parse visibility filter
            Visibility? visibilityFilter = null;
            if (!string.IsNullOrEmpty(visibility))
            {
                if (!Enum.TryParse<Visibility>(visibility, ignoreCase: true, out var parsedVisibility))
                {
                    return new MembersGetResult(Success: false, Error: new ToolError(
                        ErrorCodes.InvalidParameter,
                        $"Invalid visibility value: {visibility}. Valid values: public, internal, private, protected",
                        new { parameter = "visibility", value = visibility, validValues = new[] { "public", "internal", "private", "protected" } }));
                }
                visibilityFilter = parsedVisibility;
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("members_get", ErrorCodes.NoSession);
                return new MembersGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, "No active debug session"));
            }

            // Member inspection works with running or paused process (metadata only)
            _logger.LogDebug("Getting members for type {TypeName}", type_name);

            var result = await _processDebugger.GetMembersAsync(
                type_name,
                module_name,
                include_inherited,
                memberKindsArray,
                visibilityFilter,
                include_static,
                include_instance,
                cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("members_get", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Retrieved {MethodCount} methods, {PropertyCount} properties, {FieldCount} fields, {EventCount} events for type {TypeName}",
                result.MethodCount, result.PropertyCount, result.FieldCount, result.EventCount, type_name);

            // Build response
            var methodList = result.Methods.Select(m => new MemberMethodInfo(
                Name: m.Name,
                Signature: m.Signature,
                ReturnType: m.ReturnType,
                Parameters: m.Parameters.Select(p => new MemberParameterInfo(
                    p.Name, p.Type, p.IsOptional, p.IsOut, p.IsRef, p.DefaultValue)).ToList(),
                Visibility: m.Visibility.ToString().ToLowerInvariant(),
                IsStatic: m.IsStatic,
                IsVirtual: m.IsVirtual,
                IsAbstract: m.IsAbstract,
                IsGeneric: m.IsGeneric,
                GenericParameters: m.GenericParameters,
                DeclaringType: m.DeclaringType)).ToList();

            var propertyList = result.Properties.Select(p => new MemberPropertyInfo(
                Name: p.Name,
                Type: p.Type,
                Visibility: p.Visibility.ToString().ToLowerInvariant(),
                IsStatic: p.IsStatic,
                HasGetter: p.HasGetter,
                HasSetter: p.HasSetter,
                GetterVisibility: p.GetterVisibility?.ToString().ToLowerInvariant(),
                SetterVisibility: p.SetterVisibility?.ToString().ToLowerInvariant(),
                IsIndexer: p.IsIndexer,
                IndexerParameters: p.IndexerParameters?.Select(ip => new MemberIndexerParameterInfo(ip.Name, ip.Type)).ToList())).ToList();

            var fieldList = result.Fields.Select(f => new MemberFieldInfo(
                Name: f.Name,
                Type: f.Type,
                Visibility: f.Visibility.ToString().ToLowerInvariant(),
                IsStatic: f.IsStatic,
                IsReadOnly: f.IsReadOnly,
                IsConst: f.IsConst,
                ConstValue: f.ConstValue)).ToList();

            var eventList = result.Events.Select(e => new MemberEventInfo(
                Name: e.Name,
                Type: e.Type,
                Visibility: e.Visibility.ToString().ToLowerInvariant(),
                IsStatic: e.IsStatic,
                AddMethod: e.AddMethod,
                RemoveMethod: e.RemoveMethod)).ToList();

            // Four independent unbounded collections share the 256 KB budget in equal quarters —
            // simpler and more predictable than a cross-collection proportional split, at the
            // cost of occasionally trimming one collection while another has headroom to spare.
            const int perCollectionBudget = ResultTruncation.DefaultBudgetBytes / 4;
            var (boundedMethods, methodsTruncation) = ResultTruncation.Bound(
                methodList, "methods exceeded its share of the 256 KB size budget", perCollectionBudget);
            var (boundedProperties, propertiesTruncation) = ResultTruncation.Bound(
                propertyList, "properties exceeded its share of the 256 KB size budget", perCollectionBudget);
            var (boundedFields, fieldsTruncation) = ResultTruncation.Bound(
                fieldList, "fields exceeded its share of the 256 KB size budget", perCollectionBudget);
            var (boundedEvents, eventsTruncation) = ResultTruncation.Bound(
                eventList, "events exceeded its share of the 256 KB size budget", perCollectionBudget);

            var truncations = new[] { methodsTruncation, propertiesTruncation, fieldsTruncation, eventsTruncation }
                .Where(t => t is not null)
                .Cast<TruncationInfo>()
                .ToList();
            var combinedTruncation = truncations.Count == 0
                ? null
                : new TruncationInfo(
                    Returned: truncations.Sum(t => t.Returned),
                    Available: truncations.Sum(t => t.Available ?? 0),
                    Reason: string.Join("; ", truncations.Select(t => t.Reason)));

            return new MembersGetResult(
                Success: true,
                TypeName: result.TypeName,
                Methods: boundedMethods,
                Properties: boundedProperties,
                Fields: boundedFields,
                Events: boundedEvents,
                IncludesInherited: result.IncludesInherited,
                MethodCount: result.MethodCount,
                PropertyCount: result.PropertyCount,
                FieldCount: result.FieldCount,
                EventCount: result.EventCount,
                Truncation: combinedTruncation);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not attached"))
        {
            _logger.ToolError("members_get", ErrorCodes.NoSession);
            return new MembersGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.ToolError("members_get", ErrorCodes.TypeNotFound);
            return new MembersGetResult(Success: false, Error: new ToolError(
                ErrorCodes.TypeNotFound, ex.Message, new { typeName = type_name, moduleName = module_name }));
        }
        catch (Exception ex)
        {
            _logger.ToolError("members_get", ErrorCodes.MetadataError);
            _logger.LogError(ex, "Member inspection failed for type '{TypeName}'", type_name);
            return new MembersGetResult(Success: false, Error: new ToolError(
                ErrorCodes.MetadataError, $"Failed to inspect members: {ex.Message}"));
        }
    }
}
