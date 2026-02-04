using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Infrastructure;

/// <summary>
/// Logger provider that sends log messages via MCP protocol notifications.
/// </summary>
/// <remarks>
/// <para>
/// This provider creates <see cref="McpLogger"/> instances that send log messages
/// to connected MCP clients using the <c>notifications/message</c> protocol method.
/// </para>
/// <para>
/// The provider lazily resolves <see cref="IMcpServer"/> from the service provider
/// on first use, allowing it to be registered before the MCP server is fully initialized.
/// </para>
/// <para>
/// Logger instances are cached per category name for efficient reuse.
/// </para>
/// </remarks>
/// <example>
/// Registration in DI container:
/// <code>
/// builder.Services.AddSingleton&lt;ILoggerProvider, McpLoggerProvider&gt;();
/// </code>
/// </example>
public sealed class McpLoggerProvider : ILoggerProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LoggingOptions _options;
    private readonly ConcurrentDictionary<string, McpLogger> _loggers = new();
    private IMcpServer? _server;
    private bool _serverResolved;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="McpLoggerProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve <see cref="IMcpServer"/>.</param>
    /// <param name="options">The logging options controlling behavior.</param>
    public McpLoggerProvider(IServiceProvider serviceProvider, LoggingOptions options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance for unit testing with an explicit server reference.
    /// </summary>
    /// <param name="server">The MCP server instance, or null for testing without MCP.</param>
    /// <param name="options">The logging options controlling behavior.</param>
    internal McpLoggerProvider(IMcpServer? server, LoggingOptions options)
    {
        _serviceProvider = null!;
        _server = server;
        _serverResolved = true;
        _options = options;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Creates or returns a cached <see cref="McpLogger"/> for the specified category.
    /// </remarks>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new McpLogger(this, name, _options));
    }

    /// <summary>
    /// Gets the MCP server instance, lazily resolving it from the service provider on first call.
    /// </summary>
    /// <returns>The MCP server instance, or null if not available.</returns>
    /// <remarks>
    /// This method is thread-safe and caches the resolved server instance.
    /// If resolution fails, it will retry on the next call.
    /// </remarks>
    internal IMcpServer? GetServer()
    {
        if (_serverResolved)
            return _server;

        lock (_lock)
        {
            if (_serverResolved)
                return _server;

            try
            {
                _server = _serviceProvider.GetService(typeof(IMcpServer)) as IMcpServer;
            }
            catch
            {
                // Server not available yet, will retry on next log
            }

            if (_server != null)
                _serverResolved = true;

            return _server;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
    }
}
