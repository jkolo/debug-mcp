using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for resource listing behavior (T014).
/// Verifies that resources appear/disappear based on session state.
/// </summary>
public class ResourceListTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly DebuggerResourceProvider _provider;

    public ResourceListTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        var threadCache = new ThreadSnapshotCache();
        var allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            registry,
            threadCache,
            allowedPaths,
            providerLogger.Object);
    }

    [Fact]
    public void HasActiveSession_WhenSessionActive_ReturnsTrue()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });

        _provider.HasActiveSession.Should().BeTrue();
    }

    [Fact]
    public void HasActiveSession_WhenNoSession_ReturnsFalse()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        _provider.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void GetSessionJson_WhenActive_Succeeds()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });

        var json = _provider.GetSessionJson();
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"processId\":1234");
    }

    [Fact]
    public void GetBreakpointsJson_WhenActive_Succeeds()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });

        var json = _provider.GetBreakpointsJson();
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"breakpoints\"");
    }

    [Fact]
    public void AllResources_WhenNoSession_ThrowInvalidOperation()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        ((Action)(() => _provider.GetSessionJson()))
            .Should().Throw<InvalidOperationException>();
        ((Action)(() => _provider.GetBreakpointsJson()))
            .Should().Throw<InvalidOperationException>();
        ((Action)(() => _provider.GetThreadsJson()))
            .Should().Throw<InvalidOperationException>();
    }
}
