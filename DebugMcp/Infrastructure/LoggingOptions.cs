namespace DebugMcp.Infrastructure;

/// <summary>
/// Configuration options for MCP logging behavior.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets whether to write logs to stderr alongside MCP notifications.
    /// Default: false (MCP only).
    /// </summary>
    public bool EnableStderr { get; set; } = false;

    /// <summary>
    /// Gets or sets the default minimum log level for MCP notifications
    /// before client sets it via logging/setLevel.
    /// Default: Info.
    /// </summary>
    public McpLogLevel DefaultMinLevel { get; set; } = McpLogLevel.Info;

    /// <summary>
    /// Gets or sets the maximum payload size in bytes before truncation.
    /// Default: 64KB (65536 bytes).
    /// </summary>
    public int MaxPayloadBytes { get; set; } = 64 * 1024;
}
