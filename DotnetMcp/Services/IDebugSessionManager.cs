using DotnetMcp.Models;

namespace DotnetMcp.Services;

/// <summary>
/// Manages the lifecycle of debug sessions.
/// </summary>
public interface IDebugSessionManager
{
    /// <summary>
    /// Gets the current debug session, or null if disconnected.
    /// </summary>
    DebugSession? CurrentSession { get; }

    /// <summary>
    /// Creates a new debug session by attaching to a running process.
    /// </summary>
    /// <param name="pid">Process ID to attach to.</param>
    /// <param name="timeout">Timeout for the attach operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created debug session.</returns>
    Task<DebugSession> AttachAsync(int pid, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new debug session by launching a process under debugger control.
    /// </summary>
    /// <param name="program">Path to the executable or DLL to debug.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cwd">Working directory.</param>
    /// <param name="env">Environment variables.</param>
    /// <param name="stopAtEntry">Whether to pause at entry point.</param>
    /// <param name="timeout">Timeout for the launch operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created debug session.</returns>
    Task<DebugSession> LaunchAsync(
        string program,
        string[]? args = null,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        bool stopAtEntry = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of the debug session.
    /// </summary>
    /// <returns>Current session state with context, or disconnected state if no session.</returns>
    SessionState GetCurrentState();

    /// <summary>
    /// Ends the current debug session.
    /// </summary>
    /// <param name="terminateProcess">Whether to terminate the process (only for launched processes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(bool terminateProcess = false, CancellationToken cancellationToken = default);
}
