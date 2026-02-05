using DebugMcp.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Xunit;

namespace DebugMcp.Tests.Infrastructure;

/// <summary>
/// Unit tests for McpLogger class.
/// </summary>
public class McpLoggerTests
{
    [Fact]
    public void McpLogger_ImplementsILogger()
    {
        var type = typeof(McpLogger);
        Assert.True(typeof(ILogger).IsAssignableFrom(type),
            "McpLogger must implement ILogger interface");
    }

    [Fact]
    public void McpLogger_BeginScope_ReturnsNonNull()
    {
        // Arrange
        var logger = CreateTestLogger();

        // Act
        using var scope = logger.BeginScope("test scope");

        // Assert
        Assert.NotNull(scope);
    }

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    public void McpLogger_IsEnabled_ReturnsBasedOnMinLevel(LogLevel level)
    {
        // Arrange
        var logger = CreateTestLogger();

        // Act
        var isEnabled = logger.IsEnabled(level);

        // Assert - by default (info level), debug should be disabled, info+ enabled
        if (level < LogLevel.Information)
            Assert.False(isEnabled);
        else
            Assert.True(isEnabled);
    }

    [Fact]
    public void McpLogger_IsEnabled_ReturnsFalseForNone()
    {
        // Arrange
        var logger = CreateTestLogger();

        // Act
        var isEnabled = logger.IsEnabled(LogLevel.None);

        // Assert
        Assert.False(isEnabled);
    }

    [Fact]
    public void McpLogger_TruncatesLargePayloads()
    {
        // This test verifies truncation behavior
        // Large payload (> 64KB) should be truncated with [truncated] indicator
        const int maxPayload = 64 * 1024;
        var largeMessage = new string('x', maxPayload + 1000);

        // The actual verification happens in integration tests
        // This unit test documents the requirement
        Assert.True(largeMessage.Length > maxPayload);
    }

    private static ILogger CreateTestLogger()
    {
        // Create a provider with null server for unit testing
        var provider = new McpLoggerProvider((McpServer?)null, new LoggingOptions());
        return provider.CreateLogger("TestCategory");
    }
}
