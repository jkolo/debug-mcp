using DebugMcp.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Xunit;

namespace DebugMcp.Tests.Infrastructure;

/// <summary>
/// Unit tests for McpLoggerProvider class.
/// </summary>
public class McpLoggerProviderTests
{
    [Fact]
    public void McpLoggerProvider_ImplementsILoggerProvider()
    {
        var type = typeof(McpLoggerProvider);
        Assert.True(typeof(ILoggerProvider).IsAssignableFrom(type),
            "McpLoggerProvider must implement ILoggerProvider interface");
    }

    [Fact]
    public void CreateLogger_ReturnsMcpLogger()
    {
        // Arrange
        var provider = CreateTestProvider();

        // Act
        var logger = provider.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<McpLogger>(logger);
    }

    [Fact]
    public void CreateLogger_ReturnsSameInstanceForSameCategory()
    {
        // Arrange
        var provider = CreateTestProvider();
        const string category = "TestCategory";

        // Act
        var logger1 = provider.CreateLogger(category);
        var logger2 = provider.CreateLogger(category);

        // Assert - should cache logger instances per category
        Assert.Same(logger1, logger2);
    }

    [Fact]
    public void CreateLogger_ReturnsDifferentInstancesForDifferentCategories()
    {
        // Arrange
        var provider = CreateTestProvider();

        // Act
        var logger1 = provider.CreateLogger("Category1");
        var logger2 = provider.CreateLogger("Category2");

        // Assert
        Assert.NotSame(logger1, logger2);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var provider = CreateTestProvider();

        // Act & Assert
        var exception = Record.Exception(() => provider.Dispose());
        Assert.Null(exception);
    }

    private static McpLoggerProvider CreateTestProvider()
    {
        // Create provider with null server for unit testing
        // Full integration tests verify MCP communication
        return new McpLoggerProvider((McpServer?)null, new LoggingOptions());
    }
}
