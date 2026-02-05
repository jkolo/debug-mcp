using System.Threading.Channels;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Infrastructure;

/// <summary>
/// Sends MCP notifications when breakpoints are hit.
/// Uses fire-and-forget pattern - failures are logged but don't affect debuggee.
/// </summary>
public sealed class BreakpointNotifier : IBreakpointNotifier, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BreakpointNotifier> _logger;
    private readonly Channel<BreakpointNotification> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;

    private McpServer? _server;
    private bool _serverResolved;
    private readonly Lock _lock = new();

    /// <summary>
    /// Notification method name for breakpoint hit events.
    /// </summary>
    public const string NotificationMethod = "debugger/breakpointHit";

    public BreakpointNotifier(IServiceProvider serviceProvider, ILogger<BreakpointNotifier> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Unbounded channel - we rely on MaxNotifications to limit high-frequency tracepoints
        _channel = Channel.CreateUnbounded<BreakpointNotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start background processing task
        _processingTask = ProcessNotificationsAsync(_cts.Token);
    }

    /// <summary>
    /// Constructor for unit testing with explicit server reference.
    /// </summary>
    internal BreakpointNotifier(McpServer? server, ILogger<BreakpointNotifier> logger)
    {
        _serviceProvider = null!;
        _server = server;
        _serverResolved = true;
        _logger = logger;

        _channel = Channel.CreateUnbounded<BreakpointNotification>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _processingTask = ProcessNotificationsAsync(_cts.Token);
    }

    /// <inheritdoc />
    public Task SendBreakpointHitAsync(BreakpointNotification notification)
    {
        // Fire-and-forget: write to channel, don't wait
        if (!_channel.Writer.TryWrite(notification))
        {
            _logger.LogWarning("Failed to queue breakpoint notification for {BreakpointId} - channel full or closed",
                notification.BreakpointId);
        }
        else
        {
            _logger.LogDebug("Queued breakpoint notification for {BreakpointId}", notification.BreakpointId);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var notification in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                await SendNotificationToMcpAsync(notification);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in notification processing loop");
        }
    }

    private async Task SendNotificationToMcpAsync(BreakpointNotification notification)
    {
        var server = GetServer();
        if (server == null)
        {
            _logger.LogDebug("MCP server not available, skipping notification for {BreakpointId}",
                notification.BreakpointId);
            return;
        }

        try
        {
            // Build notification params matching the contract schema
            var notificationParams = new
            {
                breakpointId = notification.BreakpointId,
                type = notification.Type == BreakpointType.Blocking ? "blocking" : "tracepoint",
                location = new
                {
                    file = notification.Location.File,
                    line = notification.Location.Line,
                    column = notification.Location.Column,
                    functionName = notification.Location.FunctionName,
                    moduleName = notification.Location.ModuleName
                },
                threadId = notification.ThreadId,
                timestamp = notification.Timestamp.ToString("O"),
                hitCount = notification.HitCount,
                logMessage = notification.LogMessage,
                exceptionInfo = notification.ExceptionInfo != null ? new
                {
                    type = notification.ExceptionInfo.Type,
                    message = notification.ExceptionInfo.Message,
                    isFirstChance = notification.ExceptionInfo.IsFirstChance,
                    stackTrace = notification.ExceptionInfo.StackTrace
                } : null
            };

            // Fire and forget - don't await the notification send
            _ = server.SendNotificationAsync(NotificationMethod, notificationParams);

            _logger.LogInformation(
                "Sent breakpoint notification: {BreakpointId} ({Type}) at {File}:{Line}",
                notification.BreakpointId,
                notification.Type,
                notification.Location.File,
                notification.Location.Line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send MCP notification for breakpoint {BreakpointId}",
                notification.BreakpointId);
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

            if (_server != null)
                _serverResolved = true;

            return _server;
        }
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Ignore errors during shutdown
        }

        _cts.Dispose();
    }
}
