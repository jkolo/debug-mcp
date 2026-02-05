using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

/// <summary>
/// MCP tool for reading debugged process's stdout/stderr output.
/// </summary>
[McpServerToolType]
public sealed class ProcessReadOutputTool
{
    private readonly ProcessIoManager _ioManager;
    private readonly ILogger<ProcessReadOutputTool> _logger;

    public ProcessReadOutputTool(ProcessIoManager ioManager, ILogger<ProcessReadOutputTool> logger)
    {
        _ioManager = ioManager;
        _logger = logger;
    }

    /// <summary>
    /// Read accumulated stdout and/or stderr output from the debugged process.
    /// </summary>
    /// <param name="stream">Which stream to read: "stdout", "stderr", or "both" (default)</param>
    /// <param name="clear">Clear the buffer after reading (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [McpServerTool(Name = "process_read_output")]
    [Description("Read accumulated stdout and/or stderr output from the debugged process")]
    public Task<string> ReadOutputAsync(
        [Description("Which stream to read: 'stdout', 'stderr', or 'both' (default)")] string stream = "both",
        [Description("Clear the buffer after reading")] bool clear = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.ToolInvoked("process_read_output", JsonSerializer.Serialize(new { stream, clear }));

        try
        {
            // Validate stream parameter
            if (stream != "both" && stream != "stdout" && stream != "stderr")
            {
                _logger.ToolError("process_read_output", ErrorCodes.InvalidParameter);
                return Task.FromResult(CreateErrorResponse(
                    ErrorCodes.InvalidParameter,
                    $"Invalid stream: '{stream}'. Must be 'stdout', 'stderr', or 'both'"));
            }

            // Check if process is attached
            if (!_ioManager.HasProcess)
            {
                _logger.ToolError("process_read_output", ErrorCodes.NoSession);
                return Task.FromResult(CreateErrorResponse(
                    ErrorCodes.NoSession,
                    "No process attached. Use debug_launch first."));
            }

            // Read output
            var (stdout, stderr) = _ioManager.ReadOutput(stream, clear);

            stopwatch.Stop();
            _logger.ToolCompleted("process_read_output", stopwatch.ElapsedMilliseconds);
            _logger.LogDebug("Read output: stdout={StdoutLen} chars, stderr={StderrLen} chars",
                stdout.Length, stderr.Length);

            // Build response based on requested stream
            object response = stream switch
            {
                "stdout" => new
                {
                    success = true,
                    stdout,
                    stdoutBytes = System.Text.Encoding.UTF8.GetByteCount(stdout)
                },
                "stderr" => new
                {
                    success = true,
                    stderr,
                    stderrBytes = System.Text.Encoding.UTF8.GetByteCount(stderr)
                },
                _ => new
                {
                    success = true,
                    stdout,
                    stderr,
                    stdoutBytes = System.Text.Encoding.UTF8.GetByteCount(stdout),
                    stderrBytes = System.Text.Encoding.UTF8.GetByteCount(stderr)
                }
            };

            return Task.FromResult(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception ex)
        {
            _logger.ToolError("process_read_output", ErrorCodes.IoFailed);
            return Task.FromResult(CreateErrorResponse(
                ErrorCodes.IoFailed,
                $"Failed to read output: {ex.Message}"));
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
