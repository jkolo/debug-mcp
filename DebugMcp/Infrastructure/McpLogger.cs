using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace DebugMcp.Infrastructure;

/// <summary>
/// Logger that sends log messages via MCP protocol notifications.
/// </summary>
/// <remarks>
/// <para>
/// This logger implements <see cref="ILogger"/> and sends log messages to MCP clients
/// using the <c>notifications/message</c> protocol method. It supports:
/// </para>
/// <list type="bullet">
///   <item><description>Level filtering based on client-set logging level</description></item>
///   <item><description>Payload truncation for messages exceeding 64KB</description></item>
///   <item><description>Optional stderr output for debugging</description></item>
///   <item><description>Fire-and-forget async delivery for non-blocking operations</description></item>
/// </list>
/// </remarks>
public sealed class McpLogger : ILogger
{
    private readonly McpLoggerProvider _provider;
    private readonly string _categoryName;
    private readonly LoggingOptions _options;
    private const string TruncatedSuffix = " [truncated]";

    /// <summary>
    /// Initializes a new instance of the <see cref="McpLogger"/> class.
    /// </summary>
    /// <param name="provider">The logger provider that created this logger.</param>
    /// <param name="categoryName">The category name for messages produced by this logger.</param>
    /// <param name="options">The logging options controlling behavior.</param>
    internal McpLogger(McpLoggerProvider provider, string categoryName, LoggingOptions options)
    {
        _provider = provider;
        _categoryName = categoryName;
        _options = options;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Log level filtering respects the MCP client's requested level (via <c>logging/setLevel</c>)
    /// if set, otherwise uses the default minimum level from <see cref="LoggingOptions.DefaultMinLevel"/>.
    /// </remarks>
    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
            return false;

        // Check against server's client-set level if available, otherwise use default
        var server = _provider.GetServer();
        var minLevel = server?.LoggingLevel?.ToLogLevel() ?? _options.DefaultMinLevel.ToLogLevel();
        return logLevel >= minLevel;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Messages are sent as MCP <c>notifications/message</c> notifications with:
    /// <list type="bullet">
    ///   <item><description><c>level</c>: RFC 5424 severity mapped from .NET LogLevel</description></item>
    ///   <item><description><c>logger</c>: The category name</description></item>
    ///   <item><description><c>data</c>: The formatted message (truncated if over 64KB)</description></item>
    /// </list>
    /// </remarks>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (exception != null)
        {
            message = $"{message}{Environment.NewLine}{exception}";
        }

        // Truncate if needed
        message = TruncateIfNeeded(message);

        // Write to stderr if enabled
        if (_options.EnableStderr)
        {
            WriteToStderr(logLevel, message);
        }

        // Send MCP notification (fire-and-forget)
        SendMcpNotification(logLevel, message);
    }

    /// <summary>
    /// Truncates the message if it exceeds <see cref="LoggingOptions.MaxPayloadBytes"/>.
    /// </summary>
    /// <param name="message">The message to potentially truncate.</param>
    /// <returns>The original message if within limits, otherwise truncated with " [truncated]" suffix.</returns>
    private string TruncateIfNeeded(string message)
    {
        if (message.Length <= _options.MaxPayloadBytes)
            return message;

        var truncateAt = _options.MaxPayloadBytes - TruncatedSuffix.Length;
        return string.Concat(message.AsSpan(0, truncateAt), TruncatedSuffix);
    }

    private void WriteToStderr(LogLevel logLevel, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = logLevel switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "FAIL",
            LogLevel.Critical => "CRIT",
            _ => "????"
        };
        Console.Error.WriteLine($"[{timestamp}] [{levelStr}] [{_categoryName}] {message}");
    }

    private void SendMcpNotification(LogLevel logLevel, string message)
    {
        var server = _provider.GetServer();
        if (server == null)
            return;

        var mcpLevel = logLevel.ToMcpLogLevel().ToSdkLoggingLevel();

        var notification = new LoggingMessageNotificationParams
        {
            Level = mcpLevel,
            Logger = _categoryName,
            Data = JsonSerializer.SerializeToElement(message)
        };

        // Fire and forget - don't await, don't block
        _ = server.SendNotificationAsync(
            NotificationMethods.LoggingMessageNotification,
            notification);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Extension methods for log level conversions to SDK types.
/// </summary>
internal static class LoggingLevelExtensions
{
    /// <summary>
    /// Converts SDK LoggingLevel to .NET LogLevel.
    /// </summary>
    public static LogLevel ToLogLevel(this LoggingLevel level) => level switch
    {
        LoggingLevel.Debug => LogLevel.Debug,
        LoggingLevel.Info => LogLevel.Information,
        LoggingLevel.Notice => LogLevel.Information,
        LoggingLevel.Warning => LogLevel.Warning,
        LoggingLevel.Error => LogLevel.Error,
        LoggingLevel.Critical => LogLevel.Critical,
        LoggingLevel.Alert => LogLevel.Critical,
        LoggingLevel.Emergency => LogLevel.Critical,
        _ => LogLevel.Information
    };

    /// <summary>
    /// Converts our McpLogLevel to SDK's LoggingLevel.
    /// </summary>
    public static LoggingLevel ToSdkLoggingLevel(this McpLogLevel level) => level switch
    {
        McpLogLevel.Debug => LoggingLevel.Debug,
        McpLogLevel.Info => LoggingLevel.Info,
        McpLogLevel.Notice => LoggingLevel.Notice,
        McpLogLevel.Warning => LoggingLevel.Warning,
        McpLogLevel.Error => LoggingLevel.Error,
        McpLogLevel.Critical => LoggingLevel.Critical,
        McpLogLevel.Alert => LoggingLevel.Alert,
        McpLogLevel.Emergency => LoggingLevel.Emergency,
        _ => LoggingLevel.Info
    };
}
