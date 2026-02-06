using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Inspection;

public class BreakpointWaitAutopsyTests
{
    private readonly Mock<IBreakpointManager> _breakpointManagerMock;
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IExceptionAutopsyService> _autopsyServiceMock;
    private readonly Mock<ILogger<BreakpointWaitTool>> _loggerMock;
    private readonly BreakpointWaitTool _sut;

    public BreakpointWaitAutopsyTests()
    {
        _breakpointManagerMock = new Mock<IBreakpointManager>();
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _autopsyServiceMock = new Mock<IExceptionAutopsyService>();
        _loggerMock = new Mock<ILogger<BreakpointWaitTool>>();

        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/path/to/TestApp.dll",
            RuntimeVersion = ".NET 10.0",
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Attach,
            AttachedAt = DateTimeOffset.UtcNow
        });

        _sut = new BreakpointWaitTool(
            _breakpointManagerMock.Object,
            _sessionManagerMock.Object,
            _autopsyServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task WaitForBreakpoint_WithIncludeAutopsyTrue_WhenExceptionHit_IncludesAutopsyInResponse()
    {
        // Arrange — exception breakpoint hit
        var hit = new BreakpointHit(
            BreakpointId: "ex-001",
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: new BreakpointLocation(File: "/src/Program.cs", Line: 42),
            HitCount: 1,
            ExceptionInfo: new ExceptionInfo(
                Type: "System.NullReferenceException",
                Message: "Object reference not set",
                IsFirstChance: true));

        _breakpointManagerMock
            .Setup(m => m.WaitForBreakpointAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hit);

        var autopsyResult = new ExceptionAutopsyResult(
            ThreadId: 1,
            Exception: new ExceptionDetail("System.NullReferenceException", "Object reference not set", true, "at Program.Main()"),
            InnerExceptions: [],
            InnerExceptionsTruncated: false,
            Frames: [new AutopsyFrame(0, "Program.Main", "TestApp.dll", false,
                new SourceLocation("/src/Program.cs", 42))],
            TotalFrames: 1);

        _autopsyServiceMock
            .Setup(m => m.GetExceptionContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(autopsyResult);

        // Act
        var json = await _sut.WaitForBreakpointAsync(include_autopsy: true);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        root.GetProperty("hit").GetBoolean().Should().BeTrue();
        root.TryGetProperty("autopsy", out var autopsy).Should().BeTrue("autopsy field should be present");
        autopsy.GetProperty("threadId").GetInt32().Should().Be(1);
        autopsy.GetProperty("exception").GetProperty("type").GetString()
            .Should().Be("System.NullReferenceException");
        autopsy.GetProperty("frames").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WaitForBreakpoint_WithIncludeAutopsyTrue_WhenRegularBreakpointHit_NoAutopsyInResponse()
    {
        // Arrange — regular breakpoint, no exception
        var hit = new BreakpointHit(
            BreakpointId: "bp-001",
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: new BreakpointLocation(File: "/src/Program.cs", Line: 10),
            HitCount: 1);

        _breakpointManagerMock
            .Setup(m => m.WaitForBreakpointAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hit);

        // Act
        var json = await _sut.WaitForBreakpointAsync(include_autopsy: true);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        root.GetProperty("hit").GetBoolean().Should().BeTrue();
        root.TryGetProperty("autopsy", out _).Should().BeFalse(
            "autopsy should not be present for regular breakpoints");

        // Autopsy service should NOT have been called
        _autopsyServiceMock.Verify(
            m => m.GetExceptionContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WaitForBreakpoint_WithoutIncludeAutopsy_WhenExceptionHit_ResponseUnchanged()
    {
        // Arrange — exception hit, but include_autopsy defaults to false
        var hit = new BreakpointHit(
            BreakpointId: "ex-002",
            ThreadId: 1,
            Timestamp: DateTimeOffset.UtcNow,
            Location: new BreakpointLocation(File: "/src/Program.cs", Line: 42),
            HitCount: 1,
            ExceptionInfo: new ExceptionInfo(
                Type: "System.InvalidOperationException",
                Message: "Sequence contains no elements",
                IsFirstChance: true));

        _breakpointManagerMock
            .Setup(m => m.WaitForBreakpointAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hit);

        // Act — default include_autopsy = false
        var json = await _sut.WaitForBreakpointAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert — backward compatible: no autopsy field
        root.GetProperty("hit").GetBoolean().Should().BeTrue();
        root.GetProperty("exceptionInfo").GetProperty("type").GetString()
            .Should().Be("System.InvalidOperationException");
        root.TryGetProperty("autopsy", out _).Should().BeFalse(
            "autopsy should not be present when include_autopsy is not set");

        // Autopsy service should NOT have been called
        _autopsyServiceMock.Verify(
            m => m.GetExceptionContextAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
