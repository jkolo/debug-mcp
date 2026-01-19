using DotnetMcp.Infrastructure;
using DotnetMcp.Models;
using Microsoft.Extensions.Logging;

namespace DotnetMcp.Services;

/// <summary>
/// Manages the lifecycle of debug sessions.
/// </summary>
public sealed class DebugSessionManager : IDebugSessionManager
{
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<DebugSessionManager> _logger;
    private DebugSession? _currentSession;
    private readonly object _lock = new();

    public DebugSessionManager(IProcessDebugger processDebugger, ILogger<DebugSessionManager> logger)
    {
        _processDebugger = processDebugger;
        _logger = logger;

        // Subscribe to state changes from the process debugger
        _processDebugger.StateChanged += OnStateChanged;
    }

    /// <inheritdoc />
    public DebugSession? CurrentSession
    {
        get
        {
            lock (_lock)
            {
                return _currentSession;
            }
        }
    }

    /// <inheritdoc />
    public async Task<DebugSession> AttachAsync(int pid, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentSession != null)
            {
                throw new InvalidOperationException("A debug session is already active. Disconnect first.");
            }
        }

        _logger.AttachingToProcess(pid);

        var processInfo = await _processDebugger.AttachAsync(pid, timeout, cancellationToken);

        var session = new DebugSession
        {
            ProcessId = processInfo.Pid,
            ProcessName = processInfo.Name,
            ExecutablePath = processInfo.ExecutablePath,
            RuntimeVersion = processInfo.RuntimeVersion ?? "Unknown",
            AttachedAt = DateTime.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Attach
        };

        lock (_lock)
        {
            _currentSession = session;
        }

        _logger.AttachedToProcess(session.ProcessId, session.ProcessName, session.RuntimeVersion);

        return session;
    }

    /// <inheritdoc />
    public async Task<DebugSession> LaunchAsync(
        string program,
        string[]? args = null,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        bool stopAtEntry = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentSession != null)
            {
                throw new InvalidOperationException("A debug session is already active. Disconnect first.");
            }
        }

        _logger.LaunchingProcess(program);

        var processInfo = await _processDebugger.LaunchAsync(
            program, args, cwd, env, stopAtEntry, timeout, cancellationToken);

        var session = new DebugSession
        {
            ProcessId = processInfo.Pid,
            ProcessName = processInfo.Name,
            ExecutablePath = processInfo.ExecutablePath,
            RuntimeVersion = processInfo.RuntimeVersion ?? "Unknown",
            AttachedAt = DateTime.UtcNow,
            State = stopAtEntry ? SessionState.Paused : SessionState.Running,
            LaunchMode = LaunchMode.Launch,
            CommandLineArgs = args,
            WorkingDirectory = cwd,
            PauseReason = stopAtEntry ? Models.PauseReason.Entry : null
        };

        lock (_lock)
        {
            _currentSession = session;
        }

        _logger.LaunchedProcess(session.ProcessId, session.ProcessName);

        return session;
    }

    /// <inheritdoc />
    public SessionState GetCurrentState()
    {
        lock (_lock)
        {
            return _currentSession?.State ?? SessionState.Disconnected;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(bool terminateProcess = false, CancellationToken cancellationToken = default)
    {
        DebugSession? session;
        lock (_lock)
        {
            session = _currentSession;
            if (session == null)
            {
                return; // Already disconnected
            }
        }

        _logger.DisconnectingFromProcess(session.ProcessId, terminateProcess);

        if (terminateProcess && session.LaunchMode == LaunchMode.Launch)
        {
            await _processDebugger.TerminateAsync(cancellationToken);
        }
        else
        {
            await _processDebugger.DetachAsync(cancellationToken);
        }

        lock (_lock)
        {
            _currentSession = null;
        }

        _logger.DisconnectedFromProcess(session.ProcessId);
    }

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        lock (_lock)
        {
            if (_currentSession == null) return;

            var oldState = _currentSession.State.ToString();
            _currentSession.State = e.NewState;
            _currentSession.PauseReason = e.PauseReason;
            _currentSession.CurrentLocation = e.Location;
            _currentSession.ActiveThreadId = e.ThreadId;

            _logger.SessionStateChanged(oldState, e.NewState.ToString());

            if (e.NewState == SessionState.Paused && e.Location != null)
            {
                _logger.ProcessPaused(
                    $"{e.Location.File}:{e.Location.Line}",
                    e.PauseReason?.ToString() ?? "Unknown",
                    e.ThreadId ?? 0);
            }
        }
    }
}
