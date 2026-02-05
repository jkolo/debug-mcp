using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for session resource read (T012).
/// </summary>
public class SessionResourceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly BreakpointRegistry _registry;
    private readonly ThreadSnapshotCache _threadCache;
    private readonly AllowedSourcePaths _allowedPaths;
    private readonly DebuggerResourceProvider _provider;

    public SessionResourceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);
        _threadCache = new ThreadSnapshotCache();
        _allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            _registry,
            _threadCache,
            _allowedPaths,
            providerLogger.Object);
    }

    private DebugSession CreateActiveSession(SessionState state = SessionState.Paused) => new()
    {
        ProcessId = 1234,
        ProcessName = "MyApp",
        ExecutablePath = "/path/to/MyApp.dll",
        RuntimeVersion = ".NET 10.0.0",
        AttachedAt = new DateTimeOffset(2026, 2, 5, 10, 30, 0, TimeSpan.Zero),
        State = state,
        LaunchMode = LaunchMode.Launch,
        CommandLineArgs = new[] { "--verbose" },
        WorkingDirectory = "/app",
        PauseReason = state == SessionState.Paused ? PauseReason.Breakpoint : null,
        CurrentLocation = state == SessionState.Paused
            ? new SourceLocation("/src/Program.cs", 42, 1, "Main", "MyApp.dll")
            : null,
        ActiveThreadId = state == SessionState.Paused ? 1 : null
    };

    [Fact]
    public void GetSessionJson_WhenActive_ReturnsAllExpectedFields()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(CreateActiveSession());

        var json = _provider.GetSessionJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("processId").GetInt32().Should().Be(1234);
        root.GetProperty("processName").GetString().Should().Be("MyApp");
        root.GetProperty("executablePath").GetString().Should().Be("/path/to/MyApp.dll");
        root.GetProperty("runtimeVersion").GetString().Should().Be(".NET 10.0.0");
        root.GetProperty("state").GetString().Should().Be("Paused");
        root.GetProperty("launchMode").GetString().Should().Be("Launch");
        root.GetProperty("attachedAt").GetString().Should().Contain("2026-02-05");
        root.GetProperty("pauseReason").GetString().Should().Be("Breakpoint");
        root.GetProperty("activeThreadId").GetInt32().Should().Be(1);
        root.GetProperty("commandLineArgs").GetArrayLength().Should().Be(1);
        root.GetProperty("workingDirectory").GetString().Should().Be("/app");

        var loc = root.GetProperty("currentLocation");
        loc.GetProperty("file").GetString().Should().Be("/src/Program.cs");
        loc.GetProperty("line").GetInt32().Should().Be(42);
        loc.GetProperty("column").GetInt32().Should().Be(1);
        loc.GetProperty("functionName").GetString().Should().Be("Main");
        loc.GetProperty("moduleName").GetString().Should().Be("MyApp.dll");
    }

    [Fact]
    public void GetSessionJson_WhenRunning_OmitsNullableFields()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(CreateActiveSession(SessionState.Running));

        var json = _provider.GetSessionJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("state").GetString().Should().Be("Running");
        root.TryGetProperty("pauseReason", out _).Should().BeFalse();
        root.TryGetProperty("currentLocation", out _).Should().BeFalse();
        root.TryGetProperty("activeThreadId", out _).Should().BeFalse();
    }

    [Fact]
    public void GetSessionJson_WhenNoSession_Throws()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        var act = () => _provider.GetSessionJson();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No active debug session*");
    }

    [Fact]
    public void HasActiveSession_ReflectsSessionState()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);
        _provider.HasActiveSession.Should().BeFalse();

        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(CreateActiveSession());
        _provider.HasActiveSession.Should().BeTrue();
    }
}
