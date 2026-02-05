using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services;

/// <summary>
/// Manages I/O redirection for debugged processes.
/// Buffers stdout/stderr output and provides stdin access.
/// </summary>
public sealed class ProcessIoManager : IDisposable
{
    private readonly ILogger<ProcessIoManager> _logger;
    private readonly Lock _lock = new();

    private Process? _process;
    private StringBuilder _stdoutBuffer = new();
    private StringBuilder _stderrBuffer = new();
    private Task? _stdoutPumpTask;
    private Task? _stderrPumpTask;
    private CancellationTokenSource? _pumpCts;
    private bool _stdinClosed;

    public ProcessIoManager(ILogger<ProcessIoManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets whether a process is currently attached.
    /// </summary>
    public bool HasProcess
    {
        get
        {
            lock (_lock)
            {
                return _process != null && !_process.HasExited;
            }
        }
    }

    /// <summary>
    /// Attaches to a process and starts pumping its stdout/stderr.
    /// </summary>
    public void AttachToProcess(Process process)
    {
        lock (_lock)
        {
            if (_process != null)
            {
                _logger.LogWarning("Replacing existing process {OldPid} with {NewPid}",
                    _process.Id, process.Id);
                DetachFromProcessInternal();
            }

            _process = process;
            _stdoutBuffer.Clear();
            _stderrBuffer.Clear();
            _stdinClosed = false;

            _pumpCts = new CancellationTokenSource();
            var token = _pumpCts.Token;

            // Start async pumps for stdout and stderr
            if (process.StartInfo.RedirectStandardOutput)
            {
                _stdoutPumpTask = PumpStreamAsync(process.StandardOutput, _stdoutBuffer, "stdout", token);
            }

            if (process.StartInfo.RedirectStandardError)
            {
                _stderrPumpTask = PumpStreamAsync(process.StandardError, _stderrBuffer, "stderr", token);
            }

            _logger.LogInformation("Attached I/O manager to process {Pid}", process.Id);
        }
    }

    /// <summary>
    /// Detaches from the current process.
    /// </summary>
    public void DetachFromProcess()
    {
        lock (_lock)
        {
            DetachFromProcessInternal();
        }
    }

    private void DetachFromProcessInternal()
    {
        if (_process == null)
            return;

        var pid = _process.Id;

        _pumpCts?.Cancel();

        // Wait for pump tasks to complete
        try
        {
            var tasks = new List<Task>();
            if (_stdoutPumpTask != null) tasks.Add(_stdoutPumpTask);
            if (_stderrPumpTask != null) tasks.Add(_stderrPumpTask);

            if (tasks.Count > 0)
            {
                Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(1));
            }
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        _pumpCts?.Dispose();
        _pumpCts = null;
        _stdoutPumpTask = null;
        _stderrPumpTask = null;
        _process = null;

        _logger.LogInformation("Detached I/O manager from process {Pid}", pid);
    }

    /// <summary>
    /// Reads accumulated output from stdout and/or stderr.
    /// </summary>
    /// <param name="stream">Which streams to read: "stdout", "stderr", or "both"</param>
    /// <param name="clear">Whether to clear the buffer after reading</param>
    /// <returns>Tuple of (stdout, stderr) content</returns>
    public (string Stdout, string Stderr) ReadOutput(string stream = "both", bool clear = false)
    {
        lock (_lock)
        {
            var stdout = "";
            var stderr = "";

            if (stream == "both" || stream == "stdout")
            {
                stdout = _stdoutBuffer.ToString();
                if (clear)
                    _stdoutBuffer.Clear();
            }

            if (stream == "both" || stream == "stderr")
            {
                stderr = _stderrBuffer.ToString();
                if (clear)
                    _stderrBuffer.Clear();
            }

            return (stdout, stderr);
        }
    }

    /// <summary>
    /// Clears the output buffers.
    /// </summary>
    public void ClearBuffers()
    {
        lock (_lock)
        {
            _stdoutBuffer.Clear();
            _stderrBuffer.Clear();
        }
    }

    /// <summary>
    /// Writes data to the process's stdin.
    /// </summary>
    /// <param name="data">Data to write</param>
    /// <returns>Number of bytes written</returns>
    public int WriteInput(string data)
    {
        lock (_lock)
        {
            if (_process == null)
            {
                throw new InvalidOperationException("No process attached");
            }

            if (_stdinClosed)
            {
                throw new InvalidOperationException("Stdin has been closed");
            }

            if (!_process.StartInfo.RedirectStandardInput)
            {
                throw new InvalidOperationException("Process stdin is not redirected");
            }

            _process.StandardInput.Write(data);
            _process.StandardInput.Flush();

            var bytes = Encoding.UTF8.GetByteCount(data);
            _logger.LogDebug("Wrote {Bytes} bytes to process stdin", bytes);

            return bytes;
        }
    }

    /// <summary>
    /// Closes the stdin stream (sends EOF).
    /// </summary>
    public void CloseInput()
    {
        lock (_lock)
        {
            if (_process == null)
            {
                throw new InvalidOperationException("No process attached");
            }

            if (_stdinClosed)
            {
                return; // Already closed
            }

            if (_process.StartInfo.RedirectStandardInput)
            {
                _process.StandardInput.Close();
                _stdinClosed = true;
                _logger.LogDebug("Closed process stdin (EOF)");
            }
        }
    }

    private async Task PumpStreamAsync(
        StreamReader reader,
        StringBuilder buffer,
        string streamName,
        CancellationToken cancellationToken)
    {
        var charBuffer = new char[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(charBuffer, cancellationToken);
                if (read == 0)
                {
                    // End of stream
                    _logger.LogDebug("Process {StreamName} stream ended", streamName);
                    break;
                }

                lock (_lock)
                {
                    buffer.Append(charBuffer, 0, read);
                }

                _logger.LogTrace("Read {Count} chars from {StreamName}", read, streamName);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error pumping {StreamName}", streamName);
        }
    }

    public void Dispose()
    {
        DetachFromProcess();
    }
}
