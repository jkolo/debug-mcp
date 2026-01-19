using System.Diagnostics;
using System.Runtime.InteropServices;
using ClrDebug;
using DotnetMcp.Infrastructure;
using DotnetMcp.Models;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.NativeLibrary;

namespace DotnetMcp.Services;

/// <summary>
/// Low-level process debugging operations using ICorDebug via ClrDebug.
/// </summary>
public sealed class ProcessDebugger : IProcessDebugger, IDisposable
{
    private readonly ILogger<ProcessDebugger> _logger;
    private DbgShim? _dbgShim;
    private CorDebug? _corDebug;
    private CorDebugProcess? _process;
    private readonly object _lock = new();

    private SessionState _currentState = SessionState.Disconnected;
    private PauseReason? _currentPauseReason;
    private SourceLocation? _currentLocation;
    private int? _activeThreadId;

    public ProcessDebugger(ILogger<ProcessDebugger> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public bool IsAttached
    {
        get
        {
            lock (_lock)
            {
                return _process != null;
            }
        }
    }

    /// <inheritdoc />
    public SessionState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <inheritdoc />
    public PauseReason? CurrentPauseReason
    {
        get
        {
            lock (_lock)
            {
                return _currentPauseReason;
            }
        }
    }

    /// <inheritdoc />
    public SourceLocation? CurrentLocation
    {
        get
        {
            lock (_lock)
            {
                return _currentLocation;
            }
        }
    }

    /// <inheritdoc />
    public int? ActiveThreadId
    {
        get
        {
            lock (_lock)
            {
                return _activeThreadId;
            }
        }
    }

    /// <inheritdoc />
    public bool IsNetProcess(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            // Check for .NET runtime modules
            foreach (ProcessModule module in process.Modules)
            {
                var name = module.ModuleName.ToLowerInvariant();
                if (name == "coreclr.dll" || name == "libcoreclr.so" || name == "libcoreclr.dylib" ||
                    name == "clr.dll" || name == "clrjit.dll")
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public ProcessInfo? GetProcessInfo(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            var mainModule = process.MainModule;

            return new ProcessInfo(
                Pid: pid,
                Name: process.ProcessName,
                ExecutablePath: mainModule?.FileName ?? string.Empty,
                IsManaged: IsNetProcess(pid),
                CommandLine: null, // Platform-specific to retrieve
                RuntimeVersion: null // Will be determined after attach
            );
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ProcessInfo> AttachAsync(int pid, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // Validate process exists and is .NET
        var processInfo = GetProcessInfo(pid);
        if (processInfo == null)
        {
            throw new InvalidOperationException($"Process {pid} not found");
        }

        if (!processInfo.IsManaged)
        {
            throw new InvalidOperationException($"Process {pid} is not a .NET application");
        }

        // Initialize ICorDebug via dbgshim for this specific process
        await Task.Run(() => InitializeCorDebugForProcess(pid), cancellationToken);

        // Attach to process
        await Task.Run(() =>
        {
            lock (_lock)
            {
                _process = _corDebug!.DebugActiveProcess(pid, win32Attach: false);
                UpdateState(SessionState.Running);
            }
        }, cancellationToken);

        // Get runtime version after attach
        var runtimeVersion = GetRuntimeVersion();

        return processInfo with { RuntimeVersion = runtimeVersion };
    }

    /// <inheritdoc />
    public Task<ProcessInfo> LaunchAsync(
        string program,
        string[]? args = null,
        string? cwd = null,
        Dictionary<string, string>? env = null,
        bool stopAtEntry = true,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // Validate program exists first
        if (!File.Exists(program))
        {
            throw new FileNotFoundException($"Program not found: {program}");
        }

        // Launch requires different DbgShim approach:
        // 1. CreateProcessForLaunch to start suspended
        // 2. RegisterForRuntimeStartup to get callback when CLR loads
        // This is not yet implemented
        throw new NotImplementedException(
            "Launch functionality requires DbgShim.CreateProcessForLaunch and RegisterForRuntimeStartup. " +
            "Use AttachAsync to attach to an already running process.");
    }

    /// <inheritdoc />
    public async Task DetachAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_process != null)
                {
                    try
                    {
                        // Stop the process before detaching to ensure it's synchronized
                        _process.Stop(0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Stop before detach failed (may already be stopped)");
                    }

                    try
                    {
                        _process.Detach();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Detach failed, process may have exited");
                    }
                    _process = null;
                }
                _corDebug?.Terminate();
                _corDebug = null;
                UpdateState(SessionState.Disconnected);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task TerminateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_process != null)
                {
                    _process.Terminate(exitCode: 0);
                    _process = null;
                }
                _corDebug?.Terminate();
                _corDebug = null;
                UpdateState(SessionState.Disconnected);
            }
        }, cancellationToken);
    }

    private void InitializeDbgShim()
    {
        lock (_lock)
        {
            if (_dbgShim != null) return;

            // Find dbgshim library
            var dbgshimPath = FindDbgShim();
            if (dbgshimPath == null)
            {
                throw new InvalidOperationException("Could not find dbgshim library. Ensure .NET SDK is installed.");
            }

            _logger.LogDebug("Loading dbgshim from: {Path}", dbgshimPath);

            // Load dbgshim native library and create wrapper
            var dbgShimHandle = Load(dbgshimPath);
            _dbgShim = new DbgShim(dbgShimHandle);
        }
    }

    private void InitializeCorDebugForProcess(int pid)
    {
        lock (_lock)
        {
            if (_corDebug != null) return;

            InitializeDbgShim();

            // Enumerate CLR instances in the target process
            var enumResult = _dbgShim!.EnumerateCLRs(pid);
            if (enumResult.Items.Length == 0)
            {
                throw new InvalidOperationException($"No CLR runtime found in process {pid}. Is it a .NET application?");
            }

            // Use the first CLR instance found
            var runtime = enumResult.Items[0];
            _logger.LogDebug("Found CLR at: {Path}", runtime.Path);

            // Get version string for this CLR
            var versionStr = _dbgShim.CreateVersionStringFromModule(pid, runtime.Path);
            _logger.LogDebug("CLR version: {Version}", versionStr);

            // Create ICorDebug interface for this version
            _corDebug = _dbgShim.CreateDebuggingInterfaceFromVersionEx(
                CorDebugInterfaceVersion.CorDebugVersion_4_0,
                versionStr);

            _corDebug.Initialize();

            // Set up managed callback
            var callback = CreateManagedCallback();
            _corDebug.SetManagedHandler(callback);
        }
    }

    private string? FindDbgShim()
    {
        // Get platform-specific package name and library name
        string packageName;
        string runtimePath;
        string libraryName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            packageName = "microsoft.diagnostics.dbgshim.win-x64";
            runtimePath = Path.Combine("runtimes", "win-x64", "native");
            libraryName = "dbgshim.dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            packageName = "microsoft.diagnostics.dbgshim.linux-x64";
            runtimePath = Path.Combine("runtimes", "linux-x64", "native");
            libraryName = "libdbgshim.so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            packageName = "microsoft.diagnostics.dbgshim.osx-x64";
            runtimePath = Path.Combine("runtimes", "osx-x64", "native");
            libraryName = "libdbgshim.dylib";
        }
        else
        {
            return null;
        }

        // Search through all potential NuGet package locations
        foreach (var nugetPath in GetNuGetPackagesPaths())
        {
            var packagePath = Path.Combine(nugetPath, packageName);
            if (!Directory.Exists(packagePath)) continue;

            var latestVersion = Directory.GetDirectories(packagePath)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (latestVersion != null)
            {
                var libraryPath = Path.Combine(latestVersion, runtimePath, libraryName);
                if (File.Exists(libraryPath))
                {
                    _logger.LogDebug("Found dbgshim at: {Path}", libraryPath);
                    return libraryPath;
                }
            }
        }

        _logger.LogWarning("dbgshim library not found in any NuGet package location");
        return null;
    }

    private static IEnumerable<string> GetNuGetPackagesPaths()
    {
        // 1. Check NUGET_PACKAGES environment variable first (standard override)
        var envPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
        {
            yield return envPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 2. Check common NuGet cache locations
        // Linux non-standard location (some distros use this)
        var cachePath = Path.Combine(home, ".cache", "NuGetPackages");
        if (Directory.Exists(cachePath))
        {
            yield return cachePath;
        }

        // Standard NuGet packages folder (Linux/Windows/macOS default)
        var defaultPath = Path.Combine(home, ".nuget", "packages");
        if (Directory.Exists(defaultPath))
        {
            yield return defaultPath;
        }

        // Windows-specific: LocalApplicationData location
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var windowsPath = Path.Combine(localAppData, "NuGet", "v3-cache");
            if (Directory.Exists(windowsPath))
            {
                yield return windowsPath;
            }
        }
    }

    private string? GetRuntimeVersion()
    {
        // Runtime version detection after attach
        // This would be implemented using ICorDebugProcess queries
        return ".NET (version detection pending)";
    }

    private static string BuildCommandLine(string program, string[]? args)
    {
        if (program.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // .NET DLL - need to launch via dotnet
            var argsList = new List<string> { "dotnet", $"\"{program}\"" };
            if (args != null)
            {
                argsList.AddRange(args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            }
            return string.Join(" ", argsList);
        }
        else
        {
            // Direct executable
            var argsList = new List<string> { $"\"{program}\"" };
            if (args != null)
            {
                argsList.AddRange(args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            }
            return string.Join(" ", argsList);
        }
    }

    private void UpdateState(SessionState newState, PauseReason? pauseReason = null,
        SourceLocation? location = null, int? threadId = null)
    {
        var oldState = _currentState;
        _currentState = newState;
        _currentPauseReason = pauseReason;
        _currentLocation = location;
        _activeThreadId = threadId;

        StateChanged?.Invoke(this, new SessionStateChangedEventArgs
        {
            NewState = newState,
            OldState = oldState,
            PauseReason = pauseReason,
            Location = location,
            ThreadId = threadId
        });
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _process?.Detach();
            _corDebug?.Terminate();
            _process = null;
            _corDebug = null;
        }
    }

    private CorDebugManagedCallback CreateManagedCallback()
    {
        var callback = new CorDebugManagedCallback();

        // Handle all events - call Continue by default for unhandled events
        callback.OnAnyEvent += (sender, e) =>
        {
            // Continue execution for events we don't explicitly handle
            // Note: Specific handlers below will NOT call Continue for pause events
        };

        // CRITICAL: Must call Attach on new AppDomains
        callback.OnCreateAppDomain += (sender, e) =>
        {
            e.AppDomain.Attach();
            e.Controller.Continue(false);
        };

        // Handle breakpoint events
        callback.OnBreakpoint += (sender, e) =>
        {
            var location = GetCurrentLocation(e.Thread);
            var threadId = (int)e.Thread.Id;

            lock (_lock)
            {
                UpdateState(SessionState.Paused, Models.PauseReason.Breakpoint, location, threadId);
            }

            // Don't call Continue - let the session manager decide
        };

        // Handle exception events
        callback.OnException += (sender, e) =>
        {
            var location = GetCurrentLocation(e.Thread);
            var threadId = (int)e.Thread.Id;

            lock (_lock)
            {
                UpdateState(SessionState.Paused, Models.PauseReason.Exception, location, threadId);
            }

            // Don't call Continue - let the session manager decide
        };

        // Handle process exit
        callback.OnExitProcess += (sender, e) =>
        {
            lock (_lock)
            {
                _process = null;
                UpdateState(SessionState.Disconnected);
            }
            e.Controller.Continue(false);
        };

        // Handle module loads (for module tracking)
        callback.OnLoadModule += (sender, e) =>
        {
            // Continue after module load
            e.Controller.Continue(false);
        };

        // Handle assembly loads
        callback.OnLoadAssembly += (sender, e) =>
        {
            e.Controller.Continue(false);
        };

        // Handle thread creation
        callback.OnCreateThread += (sender, e) =>
        {
            e.Controller.Continue(false);
        };

        // Handle thread exit
        callback.OnExitThread += (sender, e) =>
        {
            e.Controller.Continue(false);
        };

        return callback;
    }

    private static SourceLocation? GetCurrentLocation(CorDebugThread thread)
    {
        try
        {
            var frame = thread.ActiveFrame;
            if (frame == null) return null;

            var function = frame.Function;
            var module = function.Module;

            // Note: Source location extraction requires PDB reading
            // This is a placeholder - full implementation would use System.Reflection.Metadata
            return new SourceLocation(
                File: "Unknown",
                Line: 0,
                FunctionName: $"0x{function.Token:X8}",
                ModuleName: module.Name
            );
        }
        catch
        {
            return null;
        }
    }
}
