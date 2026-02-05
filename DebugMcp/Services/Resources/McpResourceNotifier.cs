using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Services.Resources;

/// <summary>
/// Production implementation of ResourceNotifier that sends MCP notifications
/// via IMcpServer. Subscribes to IProcessDebugger and BreakpointRegistry events
/// to trigger resource update notifications.
/// </summary>
public sealed class McpResourceNotifier : ResourceNotifier
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProcessDebugger _processDebugger;
    private readonly IDebugSessionManager _sessionManager;
    private readonly BreakpointRegistry _breakpointRegistry;
    private readonly ThreadSnapshotCache _threadSnapshotCache;
    private readonly AllowedSourcePaths _allowedSourcePaths;
    private readonly PdbSymbolCache _pdbSymbolCache;
    private readonly ILogger<McpResourceNotifier> _logger;

    private McpServer? _server;
    private bool _serverResolved;
    private readonly Lock _lock = new();

    public McpResourceNotifier(
        IServiceProvider serviceProvider,
        IProcessDebugger processDebugger,
        IDebugSessionManager sessionManager,
        BreakpointRegistry breakpointRegistry,
        ThreadSnapshotCache threadSnapshotCache,
        AllowedSourcePaths allowedSourcePaths,
        PdbSymbolCache pdbSymbolCache,
        ILogger<McpResourceNotifier> logger,
        int debounceMs = 300) : base(debounceMs)
    {
        _serviceProvider = serviceProvider;
        _processDebugger = processDebugger;
        _sessionManager = sessionManager;
        _breakpointRegistry = breakpointRegistry;
        _threadSnapshotCache = threadSnapshotCache;
        _allowedSourcePaths = allowedSourcePaths;
        _pdbSymbolCache = pdbSymbolCache;
        _logger = logger;

        // Subscribe to events that trigger resource changes
        _processDebugger.StateChanged += OnStateChanged;
        _processDebugger.StepCompleted += OnStepCompleted;
        _processDebugger.ModuleLoaded += OnModuleLoaded;
        _processDebugger.ModuleUnloaded += OnModuleUnloaded;
        _breakpointRegistry.Changed += OnBreakpointRegistryChanged;
    }

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        // Update thread cache on pause
        if (e.NewState == Models.SessionState.Paused)
        {
            try
            {
                var threads = _sessionManager.GetThreads();
                _threadSnapshotCache.Update(threads);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update thread snapshot cache on pause");
            }
        }

        // Clear caches on disconnect
        if (e.NewState == Models.SessionState.Disconnected)
        {
            _threadSnapshotCache.Clear();
            _allowedSourcePaths.Clear();
        }

        // Session state change → notify session resource
        NotifyResourceUpdated("debugger://session");
        // Thread data may have changed on pause → notify threads resource
        NotifyResourceUpdated("debugger://threads");

        // Session start/end → list changed
        if (e.OldState == Models.SessionState.Disconnected || e.NewState == Models.SessionState.Disconnected)
        {
            NotifyListChanged();
        }
    }

    private void OnStepCompleted(object? sender, StepCompleteEventArgs e)
    {
        NotifyResourceUpdated("debugger://session");
        NotifyResourceUpdated("debugger://threads");
    }

    private void OnModuleLoaded(object? sender, ModuleLoadedEventArgs e)
    {
        if (e.IsDynamic || e.IsInMemory) return;

        try
        {
            var reader = _pdbSymbolCache.GetOrCreateReader(e.ModulePath);
            if (reader != null)
            {
                var sourcePaths = reader.Documents
                    .Select(dh => reader.GetDocument(dh))
                    .Select(d => reader.GetString(d.Name))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                if (sourcePaths.Count > 0)
                {
                    _allowedSourcePaths.AddModule(e.ModulePath, sourcePaths);
                    _logger.LogDebug("Added {Count} source paths from module {Module}",
                        sourcePaths.Count, e.ModulePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract source paths from PDB for {Module}", e.ModulePath);
        }
    }

    private void OnModuleUnloaded(object? sender, ModuleUnloadedEventArgs e)
    {
        _allowedSourcePaths.RemoveModule(e.ModulePath);
    }

    private void OnBreakpointRegistryChanged(object? sender, EventArgs e)
    {
        NotifyResourceUpdated("debugger://breakpoints");
    }

    protected override void OnResourceUpdated(string uri)
    {
        _logger.LogDebug("Sending resource updated notification: {Uri}", uri);
        _ = SendResourceUpdatedAsync(uri);
    }

    protected override void OnListChanged()
    {
        _logger.LogDebug("Sending resource list changed notification");
        _ = SendListChangedAsync();
    }

    private async Task SendResourceUpdatedAsync(string uri)
    {
        var server = GetServer();
        if (server == null) return;

        try
        {
            await server.SendNotificationAsync(
                "notifications/resources/updated",
                new { uri });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send resource updated notification for {Uri}", uri);
        }
    }

    private async Task SendListChangedAsync()
    {
        var server = GetServer();
        if (server == null) return;

        try
        {
            await server.SendNotificationAsync(
                "notifications/resources/list_changed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send resource list changed notification");
        }
    }

    private McpServer? GetServer()
    {
        if (_serverResolved)
            return _server;

        lock (_lock)
        {
            if (_serverResolved)
                return _server;

            try
            {
                _server = _serviceProvider.GetService(typeof(McpServer)) as McpServer;
            }
            catch
            {
                // Server not available yet
            }

            _serverResolved = true;
        }

        return _server;
    }
}
