using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for breakpoints resource read (T013).
/// </summary>
public class BreakpointsResourceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly BreakpointRegistry _registry;
    private readonly DebuggerResourceProvider _provider;

    public BreakpointsResourceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        _registry = new BreakpointRegistry(registryLogger.Object);
        var threadCache = new ThreadSnapshotCache();
        var allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            _registry,
            threadCache,
            allowedPaths,
            providerLogger.Object);

        // Set up active session for most tests
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "MyApp",
            ExecutablePath = "/path/to/MyApp.dll",
            RuntimeVersion = ".NET 10.0.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Attach
        });
    }

    [Fact]
    public void GetBreakpointsJson_WithBreakpoints_ReturnsAllFields()
    {
        _registry.Add(new Breakpoint(
            Id: "bp-abc123",
            Location: new BreakpointLocation("/src/Program.cs", 42),
            State: BreakpointState.Bound,
            Enabled: true,
            Verified: true,
            HitCount: 3,
            Condition: "x > 5"));

        var json = _provider.GetBreakpointsJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("breakpoints").GetArrayLength().Should().Be(1);
        var bp = root.GetProperty("breakpoints")[0];
        bp.GetProperty("id").GetString().Should().Be("bp-abc123");
        bp.GetProperty("type").GetString().Should().Be("Breakpoint");
        bp.GetProperty("file").GetString().Should().Be("/src/Program.cs");
        bp.GetProperty("line").GetInt32().Should().Be(42);
        bp.GetProperty("enabled").GetBoolean().Should().BeTrue();
        bp.GetProperty("verified").GetBoolean().Should().BeTrue();
        bp.GetProperty("state").GetString().Should().Be("Bound");
        bp.GetProperty("hitCount").GetInt32().Should().Be(3);
        bp.GetProperty("condition").GetString().Should().Be("x > 5");
    }

    [Fact]
    public void GetBreakpointsJson_WithTracepoint_SetsTypeCorrectly()
    {
        _registry.Add(new Breakpoint(
            Id: "tp-def456",
            Location: new BreakpointLocation("/src/Program.cs", 10),
            State: BreakpointState.Bound,
            Enabled: true,
            Verified: true,
            HitCount: 0,
            Type: BreakpointType.Tracepoint,
            LogMessage: "Counter is {i}"));

        var json = _provider.GetBreakpointsJson();
        var doc = JsonDocument.Parse(json);
        var bp = doc.RootElement.GetProperty("breakpoints")[0];

        bp.GetProperty("type").GetString().Should().Be("Tracepoint");
        bp.GetProperty("logMessage").GetString().Should().Be("Counter is {i}");
    }

    [Fact]
    public void GetBreakpointsJson_WithExceptionBreakpoints_ReturnsAllFields()
    {
        _registry.AddException(new ExceptionBreakpoint(
            Id: "ex-ghi789",
            ExceptionType: "System.NullReferenceException",
            BreakOnFirstChance: true,
            BreakOnSecondChance: true,
            IncludeSubtypes: true,
            Enabled: true,
            Verified: true,
            HitCount: 0));

        var json = _provider.GetBreakpointsJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("exceptionBreakpoints").GetArrayLength().Should().Be(1);
        var eb = root.GetProperty("exceptionBreakpoints")[0];
        eb.GetProperty("id").GetString().Should().Be("ex-ghi789");
        eb.GetProperty("exceptionType").GetString().Should().Be("System.NullReferenceException");
        eb.GetProperty("breakOnFirstChance").GetBoolean().Should().BeTrue();
        eb.GetProperty("breakOnSecondChance").GetBoolean().Should().BeTrue();
        eb.GetProperty("includeSubtypes").GetBoolean().Should().BeTrue();
        eb.GetProperty("enabled").GetBoolean().Should().BeTrue();
        eb.GetProperty("verified").GetBoolean().Should().BeTrue();
        eb.GetProperty("hitCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public void GetBreakpointsJson_Empty_ReturnsEmptyArrays()
    {
        var json = _provider.GetBreakpointsJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("breakpoints").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("exceptionBreakpoints").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void GetBreakpointsJson_WhenNoSession_Throws()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        var act = () => _provider.GetBreakpointsJson();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No active debug session*");
    }
}
