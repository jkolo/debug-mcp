using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for inspecting heap object contents.
/// </summary>
[McpServerToolType]
public sealed class ObjectInspectTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<ObjectInspectTool> _logger;

    public ObjectInspectTool(IDebugSessionManager sessionManager, ILogger<ObjectInspectTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Inspect a heap object's contents including all fields.
    /// </summary>
    /// <param name="object_ref">Object reference (variable name or expression, e.g., 'customer', 'this._orders').</param>
    /// <param name="depth">Maximum depth for nested object expansion (default: 1, max: 10).</param>
    /// <param name="thread_id">Thread ID (default: current thread).</param>
    /// <param name="frame_index">Frame index (0 = top of stack, default: 0).</param>
    /// <returns>Object inspection with type, size, fields, and values.</returns>
    [McpServerTool(Name = "object_inspect", Title = "Inspect Object",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Inspect a heap object's contents including all fields")]
    public async Task<ObjectInspectResult> InspectObject(
        [Description("Object reference (variable name or expression)")] string object_ref,
        [Description("Maximum depth for nested object expansion (1-10)")] int depth = 1,
        [Description("Thread ID (default: current thread)")] int? thread_id = null,
        [Description("Frame index (0 = top of stack)")] int frame_index = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("object_inspect",
            $"{{\"object_ref\": \"{object_ref}\", \"depth\": {depth}, \"thread_id\": {(thread_id?.ToString() ?? "null")}, \"frame_index\": {frame_index}}}");

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(object_ref))
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "object_ref cannot be empty",
                    new { parameter = "object_ref" });
            }

            if (depth < 1 || depth > 10)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "depth must be between 1 and 10",
                    new { parameter = "depth", value = depth });
            }

            if (frame_index < 0)
            {
                return CreateErrorResult(ErrorCodes.InvalidParameter,
                    "frame_index must be >= 0",
                    new { parameter = "frame_index", value = frame_index });
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("object_inspect", ErrorCodes.NoSession);
                return CreateErrorResult(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("object_inspect", ErrorCodes.NotPaused);
                return CreateErrorResult(ErrorCodes.NotPaused,
                    $"Cannot inspect object: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() });
            }

            // Inspect object
            var inspection = await _sessionManager.InspectObjectAsync(object_ref, depth, thread_id, frame_index, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("object_inspect", stopwatch.ElapsedMilliseconds);

            // Handle null reference
            if (inspection.IsNull)
            {
                _logger.LogInformation("Object '{ObjectRef}' is null", object_ref);
                return new ObjectInspectResult(
                    Success: true,
                    Inspection: new ObjectInspectionResult(
                        IsNull: true,
                        TypeName: inspection.TypeName));
            }

            _logger.LogInformation("Inspected object '{ObjectRef}': {TypeName} with {FieldCount} fields",
                object_ref, inspection.TypeName, inspection.Fields.Count);

            return new ObjectInspectResult(
                Success: true,
                Inspection: new ObjectInspectionResult(
                    IsNull: inspection.IsNull,
                    TypeName: inspection.TypeName,
                    Address: inspection.Address,
                    Size: inspection.Size,
                    Fields: inspection.Fields.Select(f => new InspectedFieldResult(
                        Name: f.Name,
                        TypeName: f.TypeName,
                        Value: f.Value,
                        Offset: f.Offset,
                        Size: f.Size,
                        HasChildren: f.HasChildren,
                        ChildCount: f.ChildCount)).ToList(),
                    HasCircularRef: inspection.HasCircularRef,
                    Truncated: inspection.Truncated));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("object_inspect", ErrorCodes.NoSession);
            return CreateErrorResult(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("object_inspect", ErrorCodes.NotPaused);
            return CreateErrorResult(ErrorCodes.NotPaused, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid reference"))
        {
            _logger.ToolError("object_inspect", ErrorCodes.InvalidReference);
            return CreateErrorResult(ErrorCodes.InvalidReference, ex.Message,
                new { object_ref });
        }
        catch (Exception ex)
        {
            _logger.ToolError("object_inspect", "INSPECTION_FAILED");
            _logger.LogError(ex, "Object inspection failed for '{ObjectRef}'", object_ref);
            return CreateErrorResult("INSPECTION_FAILED",
                $"Failed to inspect object: {ex.Message}");
        }
    }

    private static ObjectInspectResult CreateErrorResult(string code, string message, object? details = null)
    {
        return new ObjectInspectResult(Success: false, Error: new ToolError(code, message, details));
    }
}
