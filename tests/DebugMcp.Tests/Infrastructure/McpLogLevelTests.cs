using DebugMcp.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DebugMcp.Tests.Infrastructure;

/// <summary>
/// Unit tests for McpLogLevel enum and extension methods.
/// </summary>
public class McpLogLevelTests
{
    [Theory]
    [InlineData(LogLevel.Trace, McpLogLevel.Debug)]
    [InlineData(LogLevel.Debug, McpLogLevel.Debug)]
    [InlineData(LogLevel.Information, McpLogLevel.Info)]
    [InlineData(LogLevel.Warning, McpLogLevel.Warning)]
    [InlineData(LogLevel.Error, McpLogLevel.Error)]
    [InlineData(LogLevel.Critical, McpLogLevel.Critical)]
    public void ToMcpLogLevel_MapsNetLogLevelCorrectly(LogLevel netLevel, McpLogLevel expectedMcpLevel)
    {
        // Act
        var result = netLevel.ToMcpLogLevel();

        // Assert
        Assert.Equal(expectedMcpLevel, result);
    }

    [Theory]
    [InlineData(McpLogLevel.Debug, "debug")]
    [InlineData(McpLogLevel.Info, "info")]
    [InlineData(McpLogLevel.Notice, "notice")]
    [InlineData(McpLogLevel.Warning, "warning")]
    [InlineData(McpLogLevel.Error, "error")]
    [InlineData(McpLogLevel.Critical, "critical")]
    [InlineData(McpLogLevel.Alert, "alert")]
    [InlineData(McpLogLevel.Emergency, "emergency")]
    public void ToMcpString_ReturnsCorrectProtocolString(McpLogLevel level, string expectedString)
    {
        // Act
        var result = level.ToMcpString();

        // Assert
        Assert.Equal(expectedString, result);
    }

    [Theory]
    [InlineData(McpLogLevel.Debug, LogLevel.Debug)]
    [InlineData(McpLogLevel.Info, LogLevel.Information)]
    [InlineData(McpLogLevel.Warning, LogLevel.Warning)]
    [InlineData(McpLogLevel.Error, LogLevel.Error)]
    [InlineData(McpLogLevel.Critical, LogLevel.Critical)]
    public void ToLogLevel_MapsBackToNetLogLevel(McpLogLevel mcpLevel, LogLevel expectedNetLevel)
    {
        // Act
        var result = mcpLevel.ToLogLevel();

        // Assert
        Assert.Equal(expectedNetLevel, result);
    }

    [Fact]
    public void McpLogLevel_HasAllRfc5424Levels()
    {
        // Assert - all 8 RFC 5424 levels are defined
        var values = Enum.GetValues<McpLogLevel>();
        Assert.Equal(8, values.Length);
        Assert.Contains(McpLogLevel.Debug, values);
        Assert.Contains(McpLogLevel.Info, values);
        Assert.Contains(McpLogLevel.Notice, values);
        Assert.Contains(McpLogLevel.Warning, values);
        Assert.Contains(McpLogLevel.Error, values);
        Assert.Contains(McpLogLevel.Critical, values);
        Assert.Contains(McpLogLevel.Alert, values);
        Assert.Contains(McpLogLevel.Emergency, values);
    }
}
