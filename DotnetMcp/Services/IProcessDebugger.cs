using DotnetMcp.Models;

namespace DotnetMcp.Services;

/// <summary>
/// Low-level process debugging operations using ICorDebug.
/// </summary>
public interface IProcessDebugger
{
    /// <summary>
    /// Event raised when the session state changes.
    /// </summary>
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets whether a debug session is active.
    /// </summary>
    bool IsAttached { get; }

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    SessionState CurrentState { get; }

    /// <summary>
    /// Gets the pause reason when in paused state.
    /// </summary>
    PauseReason? CurrentPauseReason { get; }

    /// <summary>
    /// Gets the current source location when paused.
    /// </summary>
    SourceLocation? CurrentLocation { get; }

    /// <summary>
    /// Gets the active thread ID when paused.
    /// </summary>
    int? ActiveThreadId { get; }

    /// <summary>
    /// Checks if a process is a .NET process.
    /// </summary>
    /// <param name="pid">Process ID to check.</param>
    /// <returns>True if the process is running .NET code.</returns>
    bool IsNetProcess(int pid);

    /// <summary>
    /// Gets information about a process.
    /// </summary>
    /// <param name="pid">Process ID.</param>
    /// <returns>Process information, or null if process not found.</returns>
    ProcessInfo? GetProcessInfo(int pid);

    /// <summary>
    /// Attaches to a running .NET process.
    /// </summary>
    /// <param name="pid">Process ID to attach to.</param>
    /// <param name="timeout">Timeout for the attach operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process information.</returns>
    Task<ProcessInfo> AttachAsync(int pid, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches a process under debugger control.
    /// </summary>
    /// <param name="program">Path to the executable or DLL.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cwd">Working directory.</param>
    /// <param name="env">Environment variables.</param>
    /// <param name="stopAtEntry">Whether to pause at entry point.</param>
    /// <param name="timeout">Timeout for the launch operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process information.</returns>
    Task<ProcessInfo> LaunchAsync(
        string program,
        string[]? args = null,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        bool stopAtEntry = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches from the current process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DetachAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the debuggee process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TerminateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Event args for session state changes.
/// </summary>
public sealed class SessionStateChangedEventArgs : EventArgs
{
    /// <summary>The new session state.</summary>
    public required SessionState NewState { get; init; }

    /// <summary>The previous session state.</summary>
    public required SessionState OldState { get; init; }

    /// <summary>The reason for pause (if NewState is Paused).</summary>
    public PauseReason? PauseReason { get; init; }

    /// <summary>The current location (if NewState is Paused).</summary>
    public SourceLocation? Location { get; init; }

    /// <summary>The active thread ID (if NewState is Paused).</summary>
    public int? ThreadId { get; init; }
}
