using System.ComponentModel;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for getting type memory layout.
/// </summary>
[McpServerToolType]
public sealed class LayoutGetTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<LayoutGetTool> _logger;

    public LayoutGetTool(IDebugSessionManager sessionManager, ILogger<LayoutGetTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Get the memory layout of a type including field offsets, sizes, and padding.
    /// </summary>
    /// <param name="type_name">Full type name (e.g., 'MyApp.Models.Customer') or object reference.</param>
    /// <param name="include_inherited">Include inherited fields from base classes (default: true).</param>
    /// <param name="include_padding">Include padding analysis between fields (default: true).</param>
    /// <param name="thread_id">Thread ID (default: current thread).</param>
    /// <param name="frame_index">Frame index (0 = top of stack, default: 0).</param>
    /// <returns>Type memory layout with fields, offsets, sizes, and padding.</returns>
    [McpServerTool(Name = "layout_get", Title = "Get Memory Layout",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get the memory layout of a type including field offsets, sizes, and padding")]
    public async Task<LayoutGetResult> GetLayout(
        [Description("Full type name or object reference")] string type_name,
        [Description("Include inherited fields from base classes")] bool include_inherited = true,
        [Description("Include padding analysis between fields")] bool include_padding = true,
        [Description("Thread ID (default: current thread)")] int? thread_id = null,
        [Description("Frame index (0 = top of stack)")] int frame_index = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("layout_get",
            $"{{\"type_name\": \"{type_name}\", \"include_inherited\": {include_inherited.ToString().ToLowerInvariant()}, \"include_padding\": {include_padding.ToString().ToLowerInvariant()}}}");

        try
        {
            // Validate parameters
            if (string.IsNullOrWhiteSpace(type_name))
            {
                return new LayoutGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "type_name cannot be empty", new { parameter = "type_name" }));
            }

            if (frame_index < 0)
            {
                return new LayoutGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.InvalidParameter, "frame_index must be >= 0", new { parameter = "frame_index", value = frame_index }));
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("layout_get", ErrorCodes.NoSession);
                return new LayoutGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, "No active debug session"));
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("layout_get", ErrorCodes.NotPaused);
                return new LayoutGetResult(Success: false, Error: new ToolError(
                    ErrorCodes.NotPaused,
                    $"Cannot get layout: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() }));
            }

            // Get layout
            var layout = await _sessionManager.GetTypeLayoutAsync(
                type_name, include_inherited, include_padding, thread_id, frame_index, cancellationToken);

            stopwatch.Stop();
            _logger.ToolCompleted("layout_get", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Got layout for type '{TypeName}': {TotalSize} bytes, {FieldCount} fields",
                layout.TypeName, layout.TotalSize, layout.Fields.Count);

            return new LayoutGetResult(
                Success: true,
                Layout: new LayoutInfo(
                    TypeName: layout.TypeName,
                    TotalSize: layout.TotalSize,
                    HeaderSize: layout.HeaderSize,
                    DataSize: layout.DataSize,
                    Fields: layout.Fields.Select(f => new LayoutFieldInfo(
                        f.Name, f.TypeName, f.Offset, f.Size, f.Alignment, f.IsReference, f.DeclaringType)).ToList(),
                    IsValueType: layout.IsValueType,
                    Padding: include_padding && layout.Padding.Count > 0 ? layout.Padding : null,
                    BaseType: layout.BaseType));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("layout_get", ErrorCodes.NoSession);
            return new LayoutGetResult(Success: false, Error: new ToolError(ErrorCodes.NoSession, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("layout_get", ErrorCodes.NotPaused);
            return new LayoutGetResult(Success: false, Error: new ToolError(ErrorCodes.NotPaused, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.ToolError("layout_get", ErrorCodes.TypeNotFound);
            return new LayoutGetResult(Success: false, Error: new ToolError(
                ErrorCodes.TypeNotFound, ex.Message, new { type_name }));
        }
        catch (Exception ex)
        {
            _logger.ToolError("layout_get", "LAYOUT_FAILED");
            _logger.LogError(ex, "Layout retrieval failed for '{TypeName}'", type_name);
            return new LayoutGetResult(Success: false, Error: new ToolError(
                "LAYOUT_FAILED", $"Failed to get type layout: {ex.Message}"));
        }
    }
}
