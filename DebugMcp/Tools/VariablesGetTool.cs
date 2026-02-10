using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for inspecting variables in a paused debug session.
/// </summary>
[McpServerToolType]
public sealed class VariablesGetTool
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly ILogger<VariablesGetTool> _logger;

    private static readonly string[] ValidScopes = ["all", "locals", "arguments", "this"];

    public VariablesGetTool(IDebugSessionManager sessionManager, ILogger<VariablesGetTool> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Get variables for a stack frame.
    /// </summary>
    /// <param name="thread_id">Thread ID (default: current thread).</param>
    /// <param name="frame_index">Frame index (0 = top of stack, default: 0).</param>
    /// <param name="scope">Which variables to return: all, locals, arguments, this (default: all).</param>
    /// <param name="expand">Variable path to expand children (e.g., 'user.Address').</param>
    /// <returns>Variables with types, values, and expandability info.</returns>
    [McpServerTool(Name = "variables_get", Title = "Get Variables",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get local variables, arguments, and 'this' for a stack frame. The process must be paused. Each variable includes name, type, string value, scope (local/argument/this), and has_children flag. Use the expand parameter with a dot-separated path (e.g., 'user.Address') to drill into nested object fields. Use object_inspect for deeper inspection or evaluate for arbitrary expressions. Example response: {\"success\": true, \"variables\": [{\"name\": \"count\", \"type\": \"System.Int32\", \"value\": \"42\", \"scope\": \"local\", \"has_children\": false}, {\"name\": \"user\", \"type\": \"MyApp.User\", \"value\": \"{MyApp.User}\", \"scope\": \"local\", \"has_children\": true}]}")]
    public string GetVariables(
        [Description("Thread ID (default: current thread)")] int? thread_id = null,
        [Description("Frame index (0 = top of stack)")] int frame_index = 0,
        [Description("Which variables to return: all, locals, arguments, this")] string scope = "all",
        [Description("Variable path to expand children")] string? expand = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("variables_get",
            $"{{\"thread_id\": {(thread_id?.ToString() ?? "null")}, \"frame_index\": {frame_index}, \"scope\": \"{scope}\", \"expand\": {(expand != null ? $"\"{expand}\"" : "null")}}}");

        try
        {
            // Validate parameters
            if (frame_index < 0)
            {
                return CreateErrorResponse(ErrorCodes.InvalidParameter,
                    "frame_index must be >= 0",
                    new { parameter = "frame_index", value = frame_index });
            }

            if (!ValidScopes.Contains(scope))
            {
                return CreateErrorResponse(ErrorCodes.InvalidParameter,
                    $"scope must be one of: {string.Join(", ", ValidScopes)}",
                    new { parameter = "scope", value = scope, validValues = ValidScopes });
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.ToolError("variables_get", ErrorCodes.NoSession);
                return CreateErrorResponse(ErrorCodes.NoSession, "No active debug session");
            }

            // Check if paused
            if (session.State != SessionState.Paused)
            {
                _logger.ToolError("variables_get", ErrorCodes.NotPaused);
                return CreateErrorResponse(ErrorCodes.NotPaused,
                    $"Cannot get variables: process is not paused (current state: {session.State.ToString().ToLowerInvariant()})",
                    new { currentState = session.State.ToString().ToLowerInvariant() });
            }

            // Get variables
            var variables = _sessionManager.GetVariables(thread_id, frame_index, scope, expand);

            stopwatch.Stop();
            _logger.ToolCompleted("variables_get", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Retrieved {VariableCount} variables for frame {FrameIndex}",
                variables.Count, frame_index);

            return JsonSerializer.Serialize(new
            {
                success = true,
                variables = variables.Select(v => BuildVariableResponse(v))
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active debug session"))
        {
            _logger.ToolError("variables_get", ErrorCodes.NoSession);
            return CreateErrorResponse(ErrorCodes.NoSession, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not paused"))
        {
            _logger.ToolError("variables_get", ErrorCodes.NotPaused);
            return CreateErrorResponse(ErrorCodes.NotPaused, ex.Message);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("thread"))
        {
            _logger.ToolError("variables_get", ErrorCodes.InvalidThread);
            return CreateErrorResponse(ErrorCodes.InvalidThread, ex.Message,
                new { thread_id });
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "frameIndex")
        {
            _logger.ToolError("variables_get", ErrorCodes.InvalidFrame);
            return CreateErrorResponse(ErrorCodes.InvalidFrame,
                $"Frame index {frame_index} is out of range",
                new { frame_index });
        }
        catch (Exception ex)
        {
            _logger.ToolError("variables_get", ErrorCodes.VariablesFailed);
            return CreateErrorResponse(ErrorCodes.VariablesFailed,
                $"Failed to retrieve variables: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new
            {
                code,
                message,
                details
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BuildVariableResponse(Variable variable)
    {
        var response = new Dictionary<string, object?>
        {
            ["name"] = variable.Name,
            ["type"] = variable.Type,
            ["value"] = variable.Value,
            ["scope"] = variable.Scope.ToString().ToLowerInvariant(),
            ["has_children"] = variable.HasChildren
        };

        if (variable.ChildrenCount.HasValue)
        {
            response["children_count"] = variable.ChildrenCount.Value;
        }

        if (!string.IsNullOrEmpty(variable.Path))
        {
            response["path"] = variable.Path;
        }

        return response;
    }
}
