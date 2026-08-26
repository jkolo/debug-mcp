using DebugMcp.Models;
using DebugMcp.Models.Memory;
using DebugMcp.Services;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit;

public class MemoryReadToolTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly MemoryReadTool _tool;

    public MemoryReadToolTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var logger = new Mock<ILogger<MemoryReadTool>>();
        _tool = new MemoryReadTool(_sessionManagerMock.Object, logger.Object);
    }

    private static DebugSession PausedSession() => new()
    {
        ProcessId = 1234,
        ProcessName = "test",
        ExecutablePath = "/bin/test",
        RuntimeVersion = ".NET 10.0",
        AttachedAt = DateTimeOffset.UtcNow,
        State = SessionState.Paused,
        LaunchMode = LaunchMode.Launch,
    };

    [Fact]
    public async Task ReadMemory_HexAsciiFormat_IncludesAscii()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        _sessionManagerMock
            .Setup(s => s.ReadMemoryAsync("0x1000", 16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryRegion
            {
                Address = "0x1000",
                RequestedSize = 16,
                ActualSize = 16,
                Bytes = "48 65 6C 6C 6F",
                Ascii = "Hello",
                Error = null,
            });

        var result = await _tool.ReadMemory("0x1000", 16, "hex_ascii");

        result.Success.Should().BeTrue();
        result.Memory.Should().NotBeNull();
        result.Memory!.Address.Should().Be("0x1000");
        result.Memory.Bytes.Should().Be("48 65 6C 6C 6F");
        result.Memory.Ascii.Should().Be("Hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadMemory_HexFormat_OmitsAsciiEvenThoughServiceComputedIt()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        _sessionManagerMock
            .Setup(s => s.ReadMemoryAsync("0x1000", 16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryRegion
            {
                Address = "0x1000",
                RequestedSize = 16,
                ActualSize = 16,
                Bytes = "48 65 6C 6C 6F",
                Ascii = "Hello",
                Error = null,
            });

        var result = await _tool.ReadMemory("0x1000", 16, "hex");

        result.Success.Should().BeTrue();
        result.Memory!.Ascii.Should().BeNull("legacy 'hex' format never included ascii");
    }

    [Fact]
    public async Task ReadMemory_EmptyAddress_ReturnsInvalidParameterError()
    {
        var result = await _tool.ReadMemory(" ");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.InvalidParameter);
    }

    [Fact]
    public async Task ReadMemory_SizeExceedsMax_ReturnsSizeExceededError()
    {
        var result = await _tool.ReadMemory("0x1000", 70000);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.SizeExceeded);
    }

    [Fact]
    public async Task ReadMemory_NoSession_ReturnsNoSessionError()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);

        var result = await _tool.ReadMemory("0x1000");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NoSession);
    }

    [Fact]
    public async Task ReadMemory_NotPaused_ReturnsNotPausedError()
    {
        var session = PausedSession();
        session.State = SessionState.Running;
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(session);

        var result = await _tool.ReadMemory("0x1000");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NotPaused);
    }

    [Fact]
    public async Task ReadMemory_ZeroBytesRead_ReturnsMemoryReadFailedError()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(PausedSession());
        _sessionManagerMock
            .Setup(s => s.ReadMemoryAsync("0x1000", 16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryRegion
            {
                Address = "0x1000",
                RequestedSize = 16,
                ActualSize = 0,
                Bytes = "",
                Error = "inaccessible",
            });

        var result = await _tool.ReadMemory("0x1000", 16);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.MemoryReadFailed);
        result.Error.Message.Should().Be("inaccessible");
    }
}
