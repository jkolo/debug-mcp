using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for threads resource read (T022).
/// </summary>
public class ThreadsResourceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly ThreadSnapshotCache _threadCache;
    private readonly DebuggerResourceProvider _provider;

    public ThreadsResourceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        var registryLogger = new Mock<ILogger<BreakpointRegistry>>();
        var registry = new BreakpointRegistry(registryLogger.Object);
        _threadCache = new ThreadSnapshotCache();
        var allowedPaths = new AllowedSourcePaths();
        var providerLogger = new Mock<ILogger<DebuggerResourceProvider>>();

        _provider = new DebuggerResourceProvider(
            _sessionManagerMock.Object,
            registry,
            _threadCache,
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
    public void GetThreadsJson_WhenPaused_ReturnsThreadsWithStaleFlag()
    {
        _sessionManagerMock.Setup(x => x.GetCurrentState()).Returns(SessionState.Paused);
        _threadCache.Update(new List<ThreadInfo>
        {
            new(Id: 1, Name: "Main Thread", State: DebugMcp.Models.Inspection.ThreadState.Stopped, IsCurrent: true,
                Location: new SourceLocation("/src/Program.cs", 42, 1, "Main", "MyApp.dll")),
            new(Id: 2, Name: "Worker Thread", State: DebugMcp.Models.Inspection.ThreadState.Running, IsCurrent: false,
                Location: null)
        });

        var json = _provider.GetThreadsJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("stale").GetBoolean().Should().BeFalse();
        root.GetProperty("capturedAt").GetString().Should().NotBeNullOrEmpty();

        var threads = root.GetProperty("threads");
        threads.GetArrayLength().Should().Be(2);

        var t1 = threads[0];
        t1.GetProperty("id").GetInt32().Should().Be(1);
        t1.GetProperty("name").GetString().Should().Be("Main Thread");
        t1.GetProperty("state").GetString().Should().Be("Stopped");
        t1.GetProperty("isCurrent").GetBoolean().Should().BeTrue();

        var loc = t1.GetProperty("location");
        loc.GetProperty("file").GetString().Should().Be("/src/Program.cs");
        loc.GetProperty("line").GetInt32().Should().Be(42);
        loc.GetProperty("functionName").GetString().Should().Be("Main");

        var t2 = threads[1];
        t2.GetProperty("id").GetInt32().Should().Be(2);
        t2.GetProperty("name").GetString().Should().Be("Worker Thread");
        t2.GetProperty("state").GetString().Should().Be("Running");
        t2.GetProperty("isCurrent").GetBoolean().Should().BeFalse();
        t2.TryGetProperty("location", out _).Should().BeFalse();
    }

    [Fact]
    public void GetThreadsJson_WhenRunning_ReturnsStaleTrue()
    {
        _sessionManagerMock.Setup(x => x.GetCurrentState()).Returns(SessionState.Running);
        _threadCache.Update(new List<ThreadInfo>
        {
            new(Id: 1, Name: "Main Thread", State: DebugMcp.Models.Inspection.ThreadState.Stopped, IsCurrent: true, Location: null)
        });

        var json = _provider.GetThreadsJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("stale").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("threads").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void GetThreadsJson_WhenNoSnapshot_ReturnsEmptyThreads()
    {
        _sessionManagerMock.Setup(x => x.GetCurrentState()).Returns(SessionState.Running);

        var json = _provider.GetThreadsJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("threads").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("stale").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void GetThreadsJson_WhenNoSession_Throws()
    {
        _sessionManagerMock.Setup(x => x.CurrentSession).Returns((DebugSession?)null);

        var act = () => _provider.GetThreadsJson();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No active debug session*");
    }

    [Fact]
    public void GetThreadsJson_ThreadWithAllFields_SerializedCorrectly()
    {
        _sessionManagerMock.Setup(x => x.GetCurrentState()).Returns(SessionState.Paused);
        _threadCache.Update(new List<ThreadInfo>
        {
            new(Id: 5, Name: "Finalizer", State: DebugMcp.Models.Inspection.ThreadState.Waiting, IsCurrent: false,
                Location: new SourceLocation("/src/GC.cs", 100, 5, "Finalize", "System.Runtime.dll"))
        });

        var json = _provider.GetThreadsJson();
        var doc = JsonDocument.Parse(json);
        var thread = doc.RootElement.GetProperty("threads")[0];

        thread.GetProperty("id").GetInt32().Should().Be(5);
        thread.GetProperty("name").GetString().Should().Be("Finalizer");
        thread.GetProperty("state").GetString().Should().Be("Waiting");
        thread.GetProperty("isCurrent").GetBoolean().Should().BeFalse();

        var loc = thread.GetProperty("location");
        loc.GetProperty("file").GetString().Should().Be("/src/GC.cs");
        loc.GetProperty("line").GetInt32().Should().Be(100);
        loc.GetProperty("column").GetInt32().Should().Be(5);
        loc.GetProperty("functionName").GetString().Should().Be("Finalize");
        loc.GetProperty("moduleName").GetString().Should().Be("System.Runtime.dll");
    }
}
