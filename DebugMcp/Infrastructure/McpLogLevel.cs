using Microsoft.Extensions.Logging;

namespace DebugMcp.Infrastructure;

/// <summary>
/// MCP protocol log levels following RFC 5424 severity classification.
/// </summary>
public enum McpLogLevel
{
    /// <summary>Debug-level messages (RFC 5424 severity 7).</summary>
    Debug,
    /// <summary>Informational messages (RFC 5424 severity 6).</summary>
    Info,
    /// <summary>Normal but significant conditions (RFC 5424 severity 5).</summary>
    Notice,
    /// <summary>Warning conditions (RFC 5424 severity 4).</summary>
    Warning,
    /// <summary>Error conditions (RFC 5424 severity 3).</summary>
    Error,
    /// <summary>Critical conditions (RFC 5424 severity 2).</summary>
    Critical,
    /// <summary>Action must be taken immediately (RFC 5424 severity 1).</summary>
    Alert,
    /// <summary>System is unusable (RFC 5424 severity 0).</summary>
    Emergency
}

/// <summary>
/// Extension methods for log level conversions.
/// </summary>
public static class McpLogLevelExtensions
{
    /// <summary>
    /// Maps .NET LogLevel to MCP protocol log level.
    /// </summary>
    public static McpLogLevel ToMcpLogLevel(this LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => McpLogLevel.Debug,
        LogLevel.Debug => McpLogLevel.Debug,
        LogLevel.Information => McpLogLevel.Info,
        LogLevel.Warning => McpLogLevel.Warning,
        LogLevel.Error => McpLogLevel.Error,
        LogLevel.Critical => McpLogLevel.Critical,
        LogLevel.None => McpLogLevel.Emergency,
        _ => McpLogLevel.Info
    };

    /// <summary>
    /// Gets the string representation for MCP protocol.
    /// </summary>
    public static string ToMcpString(this McpLogLevel level) => level switch
    {
        McpLogLevel.Debug => "debug",
        McpLogLevel.Info => "info",
        McpLogLevel.Notice => "notice",
        McpLogLevel.Warning => "warning",
        McpLogLevel.Error => "error",
        McpLogLevel.Critical => "critical",
        McpLogLevel.Alert => "alert",
        McpLogLevel.Emergency => "emergency",
        _ => "info"
    };

    /// <summary>
    /// Maps MCP log level to .NET LogLevel for filtering comparison.
    /// </summary>
    public static LogLevel ToLogLevel(this McpLogLevel mcpLevel) => mcpLevel switch
    {
        McpLogLevel.Debug => LogLevel.Debug,
        McpLogLevel.Info => LogLevel.Information,
        McpLogLevel.Notice => LogLevel.Information,
        McpLogLevel.Warning => LogLevel.Warning,
        McpLogLevel.Error => LogLevel.Error,
        McpLogLevel.Critical => LogLevel.Critical,
        McpLogLevel.Alert => LogLevel.Critical,
        McpLogLevel.Emergency => LogLevel.Critical,
        _ => LogLevel.Information
    };
}
