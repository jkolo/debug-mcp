using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Resources;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.Notifications;

/// <summary>
/// Tests that BuildSessionStatePayload produces the correct MCP notification
/// method name and payload field values (feature 030 US3).
/// </summary>
public class SessionStateNotificationPayloadTests
{
    [Fact]
    public void MethodName_IsDebuggerSessionStateChanged()
    {
        McpResourceNotifier.SessionStateChangedMethod
            .Should().Be("debugger/sessionStateChanged");
    }

    [Fact]
    public void BuildSessionStatePayload_MapsAllFields_WhenPaused()
    {
        var location = new SourceLocation("Program.cs", 42, Column: 1, FunctionName: "Main", ModuleName: "TestApp");
        var args = new SessionStateChangedEventArgs
        {
            OldState = SessionState.Running,
            NewState = SessionState.Paused,
            PauseReason = PauseReason.Breakpoint,
            Location = location,
            ThreadId = 7
        };

        var payload = McpResourceNotifier.BuildSessionStatePayload(args);

        payload.OldState.Should().Be("Running");
        payload.NewState.Should().Be("Paused");
        payload.PauseReason.Should().Be("Breakpoint");
        payload.ActiveThreadId.Should().Be(7);
        payload.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        payload.Location.Should().NotBeNull();
        payload.Location!.File.Should().Be("Program.cs");
        payload.Location.Line.Should().Be(42);
        payload.Location.Column.Should().Be(1);
    }

    [Fact]
    public void BuildSessionStatePayload_NullLocation_WhenRunning()
    {
        var args = new SessionStateChangedEventArgs
        {
            OldState = SessionState.Paused,
            NewState = SessionState.Running,
            PauseReason = null,
            Location = null,
            ThreadId = null
        };

        var payload = McpResourceNotifier.BuildSessionStatePayload(args);

        payload.NewState.Should().Be("Running");
        payload.OldState.Should().Be("Paused");
        payload.PauseReason.Should().BeNull();
        payload.Location.Should().BeNull();
        payload.ActiveThreadId.Should().BeNull();
    }

    [Fact]
    public void BuildSessionStatePayload_DisconnectedState_SerializesCorrectly()
    {
        var args = new SessionStateChangedEventArgs
        {
            OldState = SessionState.Running,
            NewState = SessionState.Disconnected
        };

        var payload = McpResourceNotifier.BuildSessionStatePayload(args);

        payload.NewState.Should().Be("Disconnected");
        payload.OldState.Should().Be("Running");
    }
}
