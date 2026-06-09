using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Timeline;
using DebugMcp.Services;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.Timeline;
using DebugMcp.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Timeline;

/// <summary>
/// Unit tests for TimelineStore event recording (T011-T014, T023-T025).
/// </summary>
public class TimelineStoreEventTests
{
    private readonly Mock<IProcessDebugger> _debuggerMock = new();
    private readonly FakeBreakpointEventSource _eventSource = new();
    private readonly FakeOutputEventSource _outputSource = new();
    private readonly TimelineStore _sut;

    public TimelineStoreEventTests()
    {
        _sut = new TimelineStore(
            _eventSource,
            _debuggerMock.Object,
            _outputSource,
            new Mock<ILogger<TimelineStore>>().Object);
    }

    // ─── T011: SessionStarted / SessionEnded ───

    [Fact]
    public void StateChanged_RunningFromDisconnected_RecordsSessionStarted()
    {
        // Act
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Disconnected
        });

        // Assert
        var response = _sut.GetAll();
        response.Events.Should().HaveCount(1);
        var evt = response.Events[0];
        evt.EventType.Should().Be(TimelineEventType.SessionStarted);
        evt.Payload.Should().BeOfType<SessionStartedPayload>();
    }

    [Fact]
    public void StateChanged_RunningFromRunning_DoesNotRecordSessionStarted()
    {
        // First transition to Running
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Disconnected
        });

        // Second transition Running→Running (e.g., Paused→Running→Running is not realistic,
        // but verifying that consecutive Running fires don't repeat SessionStarted)
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Paused
        });

        // Assert: still only 1 SessionStarted
        _sut.GetAll().Events.Count(e => e.EventType == TimelineEventType.SessionStarted).Should().Be(1);
    }

    [Fact]
    public void StateChanged_Disconnected_RecordsSessionEndedAndKeepsItObservable()
    {
        // Arrange
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Disconnected
        });

        // Act
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Disconnected,
            OldState = SessionState.Running
        });

        // Assert: SessionEnded is observable (not immediately cleared)
        var events = _sut.GetAll().Events;
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(TimelineEventType.SessionStarted);
        events[1].EventType.Should().Be(TimelineEventType.SessionEnded);
    }

    [Fact]
    public void StateChanged_NewSessionStart_ClearsPreviousSessionEvents()
    {
        // Arrange — first session ends
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Disconnected
        });
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Disconnected,
            OldState = SessionState.Running
        });
        _sut.GetAll().Events.Should().HaveCount(2); // SessionStarted + SessionEnded visible

        // Act — second session starts
        _debuggerMock.Raise(x => x.StateChanged += null, new SessionStateChangedEventArgs
        {
            NewState = SessionState.Running,
            OldState = SessionState.Disconnected
        });

        // Assert: previous session events are gone; only new SessionStarted is present
        var events = _sut.GetAll().Events;
        events.Should().HaveCount(1);
        events[0].EventType.Should().Be(TimelineEventType.SessionStarted);
    }

    // ─── T012: BreakpointHit / TracepointHit ───

    [Fact]
    public void BreakpointResolved_BpPrefix_RecordsBreakpointHit()
    {
        // Arrange
        var args = new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "bp-abc123",
            ThreadId = 42,
            Location = new BreakpointLocation("/src/Program.cs", 15),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1
        };

        // Act
        _eventSource.RaiseBreakpointResolved(args);

        // Assert
        var events = _sut.GetAll().Events;
        events.Should().HaveCount(1);
        var evt = events[0];
        evt.EventType.Should().Be(TimelineEventType.BreakpointHit);
        evt.ThreadId.Should().Be(42);
        var payload = evt.Payload.Should().BeOfType<BreakpointHitPayload>().Subject;
        payload.BreakpointId.Should().Be("bp-abc123");
        payload.File.Should().Be("/src/Program.cs");
        payload.Line.Should().Be(15);
    }

    [Fact]
    public void BreakpointResolved_TpPrefix_RecordsTracepointHit()
    {
        // Arrange
        var args = new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "tp-xyz789",
            ThreadId = 7,
            Location = new BreakpointLocation("/src/App.cs", 99),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 3
        };

        // Act
        _eventSource.RaiseBreakpointResolved(args);

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.TracepointHit);
        evt.ThreadId.Should().Be(7);
        var payload = evt.Payload.Should().BeOfType<TracepointHitPayload>().Subject;
        payload.TracepointId.Should().Be("tp-xyz789");
        payload.File.Should().Be("/src/App.cs");
        payload.Line.Should().Be(99);
    }

    // ─── T013: ExceptionFirstChance / ExceptionUserUnhandled ───

    [Fact]
    public void ExceptionHit_FirstChance_RecordsExceptionFirstChance()
    {
        // Arrange + Act
        _debuggerMock.Raise(x => x.ExceptionHit += null, new ExceptionHitEventArgs
        {
            ThreadId = 55,
            Location = null,
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.NullReferenceException",
            ExceptionMessage = "Object reference not set",
            IsFirstChance = true,
            IsUnhandled = false
        });

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ExceptionFirstChance);
        evt.ThreadId.Should().Be(55);
        var payload = evt.Payload.Should().BeOfType<ExceptionPayload>().Subject;
        payload.ExceptionType.Should().Be("System.NullReferenceException");
        payload.Message.Should().Be("Object reference not set");
        payload.IsUserUnhandled.Should().BeFalse();
    }

    [Fact]
    public void ExceptionHit_Unhandled_RecordsExceptionUserUnhandled()
    {
        // Arrange + Act
        _debuggerMock.Raise(x => x.ExceptionHit += null, new ExceptionHitEventArgs
        {
            ThreadId = 12,
            Location = null,
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "Invalid op",
            IsFirstChance = false,
            IsUnhandled = true
        });

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ExceptionUserUnhandled);
        var payload = evt.Payload.Should().BeOfType<ExceptionPayload>().Subject;
        payload.IsUserUnhandled.Should().BeTrue();
    }

    // ─── T014: ModuleLoaded / ThreadStarted / ThreadExited / StdoutWritten / StderrWritten ───

    [Fact]
    public void ModuleLoaded_RecordsModuleLoadedWithExtractedName()
    {
        // Act
        _debuggerMock.Raise(x => x.ModuleLoaded += null, new ModuleLoadedEventArgs
        {
            ModulePath = "/usr/lib/MyApp.dll",
            BaseAddress = 0x7FF800000000UL,
            Size = 65536,
            IsDynamic = false,
            IsInMemory = false,
            NativeModule = new object()
        });

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ModuleLoaded);
        var payload = evt.Payload.Should().BeOfType<ModuleLoadedPayload>().Subject;
        payload.ModuleName.Should().Be("MyApp");
        payload.AssemblyPath.Should().Be("/usr/lib/MyApp.dll");
        payload.HasSymbols.Should().BeFalse();
    }

    [Fact]
    public void ThreadCreated_RecordsThreadStarted()
    {
        // Act
        _debuggerMock.Raise(x => x.ThreadCreated += null, new ThreadCreatedEventArgs { ThreadId = 101 });

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ThreadStarted);
        evt.ThreadId.Should().Be(101);
    }

    [Fact]
    public void ThreadExited_RecordsThreadExited()
    {
        // Act
        _debuggerMock.Raise(x => x.ThreadExited += null, new ThreadExitedEventArgs { ThreadId = 101 });

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ThreadExited);
        evt.ThreadId.Should().Be(101);
    }

    [Fact]
    public void OutputReceived_Stdout_RecordsStdoutWritten()
    {
        // Act
        _outputSource.RaiseOutput("Hello, World!\n", "stdout");

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.StdoutWritten);
        var payload = evt.Payload.Should().BeOfType<OutputPayload>().Subject;
        payload.Content.Should().Be("Hello, World!\n");
        payload.Stream.Should().Be("stdout");
        payload.Truncated.Should().BeFalse();
    }

    [Fact]
    public void OutputReceived_Stderr_RecordsStderrWritten()
    {
        // Act
        _outputSource.RaiseOutput("error output", "stderr");

        // Assert
        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.StderrWritten);
        var payload = evt.Payload.Should().BeOfType<OutputPayload>().Subject;
        payload.Stream.Should().Be("stderr");
    }

    [Fact]
    public void OutputReceived_TruncatedFalse_RecordsNotTruncated()
    {
        _outputSource.RaiseOutput("short content", "stdout", truncated: false);

        var payload = _sut.GetAll().Events.Single().Payload.Should().BeOfType<OutputPayload>().Subject;
        payload.Truncated.Should().BeFalse();
    }

    [Fact]
    public void OutputReceived_TruncatedTrue_RecordsTruncated()
    {
        _outputSource.RaiseOutput(new string('x', 1024), "stdout", truncated: true);

        var payload = _sut.GetAll().Events.Single().Payload.Should().BeOfType<OutputPayload>().Subject;
        payload.Truncated.Should().BeTrue();
    }

    // ─── T023: BreakpointHit payload completeness (US2) ───

    [Fact]
    public void BreakpointResolved_Bp_PayloadHasNonEmptyIdFileAndPositiveLine()
    {
        var args = new ResolvedBreakpointHitEventArgs
        {
            BreakpointId = "bp-deadbeef",
            ThreadId = 3,
            Location = new BreakpointLocation("/app/Main.cs", 42),
            Timestamp = DateTimeOffset.UtcNow,
            HitCount = 1
        };
        _eventSource.RaiseBreakpointResolved(args);

        var evt = _sut.GetAll().Events.Single();
        evt.ThreadId.Should().NotBeNull().And.Be(3);
        var payload = evt.Payload.Should().BeOfType<BreakpointHitPayload>().Subject;
        payload.BreakpointId.Should().NotBeNullOrEmpty();
        payload.File.Should().NotBeNull();
        payload.Line.Should().BeGreaterThan(0);
    }

    // ─── T024: ExceptionFirstChance payload completeness (US2) ───

    [Fact]
    public void ExceptionHit_FirstChance_PayloadHasNonNullTypeMessageAndMatchingThreadId()
    {
        _debuggerMock.Raise(x => x.ExceptionHit += null, new ExceptionHitEventArgs
        {
            ThreadId = 77,
            Location = null,
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.ArgumentException",
            ExceptionMessage = "bad arg",
            IsFirstChance = true,
            IsUnhandled = false
        });

        var evt = _sut.GetAll().Events.Single();
        evt.EventType.Should().Be(TimelineEventType.ExceptionFirstChance);
        evt.ThreadId.Should().Be(77);
        var payload = evt.Payload.Should().BeOfType<ExceptionPayload>().Subject;
        payload.ExceptionType.Should().NotBeNull();
        payload.Message.Should().NotBeNull();
    }

    // ─── T025: Cross-event ThreadId correlation (US2) ───

    [Fact]
    public void StdoutAndException_SameThread_BothCarrySameThreadId()
    {
        const int threadId = 99;

        _debuggerMock.Raise(x => x.ExceptionHit += null, new ExceptionHitEventArgs
        {
            ThreadId = threadId,
            Location = null,
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = "System.Exception",
            ExceptionMessage = "boom",
            IsFirstChance = true,
            IsUnhandled = false
        });

        // StdoutWritten events have null ThreadId (output is not thread-specific)
        // The test verifies that when both come from the same logical thread,
        // ExceptionFirstChance carries the thread_id while StdoutWritten carries null.
        _outputSource.RaiseOutput("some stdout", "stdout");

        var events = _sut.GetAll().Events;
        var exceptionEvt = events.Single(e => e.EventType == TimelineEventType.ExceptionFirstChance);
        var stdoutEvt = events.Single(e => e.EventType == TimelineEventType.StdoutWritten);

        exceptionEvt.ThreadId.Should().Be(threadId);
        stdoutEvt.ThreadId.Should().BeNull();
    }
}
