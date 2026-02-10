using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for writing to debugged process's stdin.
/// </summary>
[McpServerToolType]
public sealed class ProcessWriteInputTool
{
    private readonly ProcessIoManager _ioManager;
    private readonly ILogger<ProcessWriteInputTool> _logger;

    public ProcessWriteInputTool(ProcessIoManager ioManager, ILogger<ProcessWriteInputTool> logger)
    {
        _ioManager = ioManager;
        _logger = logger;
    }

    /// <summary>
    /// Write data to the debugged process's stdin.
    /// </summary>
    /// <param name="data">Data to write to stdin</param>
    /// <param name="close_after">Close stdin after writing (send EOF)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [McpServerTool(Name = "process_write_input", Title = "Write Process Input",
        ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Write data to the debugged process's stdin")]
    public Task<string> WriteInputAsync(
        [Description("Data to write to stdin")] string data,
        [Description("Close stdin after writing (send EOF)")] bool close_after = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("process_write_input", JsonSerializer.Serialize(new
        {
            dataLength = data?.Length ?? 0,
            close_after
        }));

        try
        {
            // Check if process is attached
            if (!_ioManager.HasProcess)
            {
                _logger.ToolError("process_write_input", ErrorCodes.NoSession);
                return Task.FromResult(CreateErrorResponse(
                    ErrorCodes.NoSession,
                    "No process attached. Use debug_launch first."));
            }

            // Validate data
            if (data == null)
            {
                _logger.ToolError("process_write_input", ErrorCodes.InvalidParameter);
                return Task.FromResult(CreateErrorResponse(
                    ErrorCodes.InvalidParameter,
                    "Data cannot be null"));
            }

            // Write to stdin
            var bytesWritten = _ioManager.WriteInput(data);

            // Optionally close stdin
            if (close_after)
            {
                _ioManager.CloseInput();
            }

            stopwatch.Stop();
            _logger.ToolCompleted("process_write_input", stopwatch.ElapsedMilliseconds);
            _logger.LogDebug("Wrote {Bytes} bytes to stdin, closed={Closed}", bytesWritten, close_after);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                bytesWritten,
                stdinClosed = close_after
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (InvalidOperationException ex)
        {
            var errorCode = ex.Message.Contains("closed") ? ErrorCodes.StdinClosed : ErrorCodes.NoSession;
            _logger.ToolError("process_write_input", errorCode);
            return Task.FromResult(CreateErrorResponse(errorCode, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.ToolError("process_write_input", ErrorCodes.IoFailed);
            return Task.FromResult(CreateErrorResponse(
                ErrorCodes.IoFailed,
                $"Failed to write input: {ex.Message}"));
        }
    }

    private static string CreateErrorResponse(string code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
