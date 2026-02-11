using System.Reflection;
using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// Contract tests for async stack trace fields in stacktrace_get response.
/// Validates backward compatibility and new async-related response fields.
/// </summary>
public class AsyncStackTraceContractTests
{
    /// <summary>
    /// T006: Every frame in the stacktrace_get response must include a frame_kind field.
    /// </summary>
    [Fact]
    public void StacktraceGet_Response_IncludesFrameKindOnEveryFrame()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false),
            new(1, "System.Threading.Tasks.Task.Run()", "System.Private.CoreLib.dll", true),
            new(2, "MyApp.Service.ProcessAsync()", "MyApp.dll", false, FrameKind: "async")
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = tool.GetStackTrace();
        var json = JsonDocument.Parse(result);
        var framesArray = json.RootElement.GetProperty("frames");

        // Assert
        foreach (var frame in framesArray.EnumerateArray())
        {
            frame.TryGetProperty("frame_kind", out var frameKind).Should().BeTrue(
                "every frame must include frame_kind field");
            frameKind.GetString().Should().BeOneOf("sync", "async", "async_continuation",
                "frame_kind must be sync, async, or async_continuation");
        }
    }

    /// <summary>
    /// T007: The include_raw parameter should be accepted without error.
    /// </summary>
    [Fact]
    public void StacktraceGet_IncludeRawParameter_AcceptedWithoutError()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false)
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = tool.GetStackTrace(include_raw: true);
        var json = JsonDocument.Parse(result);

        // Assert
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(
            "include_raw parameter should be accepted without causing an error");
    }

    /// <summary>
    /// T007 (continued): Verify include_raw parameter exists on the tool method via reflection.
    /// </summary>
    [Fact]
    public void StacktraceGet_IncludeRawParameter_ExistsOnToolMethod()
    {
        var method = typeof(StacktraceGetTool).GetMethod("GetStackTrace");
        method.Should().NotBeNull();

        var param = method!.GetParameters().FirstOrDefault(p => p.Name == "include_raw");
        param.Should().NotBeNull("include_raw parameter should exist on GetStackTrace");
        param!.ParameterType.Should().Be(typeof(bool));
        param.HasDefaultValue.Should().BeTrue();
        param.DefaultValue.Should().Be(false);
    }

    /// <summary>
    /// T008: Backward compatibility — response still contains success, thread_id, total_frames,
    /// and frames[] with index, function, module, is_external.
    /// </summary>
    [Fact]
    public void StacktraceGet_Response_BackwardCompatible()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false,
                Location: new SourceLocation("/src/Program.cs", 42, 1, "Main", "MyApp.dll")),
            new(1, "System.Runtime.CompilerServices.TaskAwaiter.GetResult()",
                "System.Private.CoreLib.dll", true)
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = tool.GetStackTrace();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert — top-level required fields
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.TryGetProperty("thread_id", out _).Should().BeTrue("response must include thread_id");
        root.TryGetProperty("total_frames", out _).Should().BeTrue("response must include total_frames");
        root.TryGetProperty("frames", out var framesElement).Should().BeTrue("response must include frames");

        // Assert — each frame has required fields
        foreach (var frame in framesElement.EnumerateArray())
        {
            frame.TryGetProperty("index", out _).Should().BeTrue("frame must include index");
            frame.TryGetProperty("function", out _).Should().BeTrue("frame must include function");
            frame.TryGetProperty("module", out _).Should().BeTrue("frame must include module");
            frame.TryGetProperty("is_external", out _).Should().BeTrue("frame must include is_external");
        }
    }

    /// <summary>
    /// T008 (continued): New fields are additive — they don't break the existing schema.
    /// </summary>
    [Fact]
    public void StacktraceGet_Response_NewFieldsAreAdditive()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Service.GetDataAsync()", "MyApp.dll", false,
                FrameKind: "async", IsAwaiting: true, LogicalFunction: "GetDataAsync")
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = tool.GetStackTrace();
        var json = JsonDocument.Parse(result);
        var frame = json.RootElement.GetProperty("frames").EnumerateArray().First();

        // Assert — new fields present
        frame.GetProperty("frame_kind").GetString().Should().Be("async");
        frame.GetProperty("is_awaiting").GetBoolean().Should().BeTrue();
        frame.GetProperty("logical_function").GetString().Should().Be("GetDataAsync");

        // Assert — existing fields still present
        frame.GetProperty("index").GetInt32().Should().Be(0);
        frame.GetProperty("function").GetString().Should().Be("MyApp.Service.GetDataAsync()");
        frame.GetProperty("module").GetString().Should().Be("MyApp.dll");
        frame.GetProperty("is_external").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// Default StackFrame has frame_kind "sync", is_awaiting false, logical_function null.
    /// </summary>
    [Fact]
    public void StackFrame_DefaultValues_MatchContract()
    {
        var frame = new StackFrame(0, "Test.Method()", "Test.dll", false);

        frame.FrameKind.Should().Be("sync");
        frame.IsAwaiting.Should().BeFalse();
        frame.LogicalFunction.Should().BeNull();
    }

    /// <summary>
    /// logical_function should be omitted from response when null.
    /// </summary>
    [Fact]
    public void StacktraceGet_Response_OmitsNullLogicalFunction()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false) // sync, no logical_function
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = tool.GetStackTrace();
        var json = JsonDocument.Parse(result);
        var frame = json.RootElement.GetProperty("frames").EnumerateArray().First();

        // Assert
        frame.TryGetProperty("logical_function", out _).Should().BeFalse(
            "logical_function should be omitted when null to keep response compact");
    }

    /// <summary>
    /// T031: Async state machine variable names should not have angle-bracket prefixes.
    /// StripStateMachineFieldName should be applied to all variable names from async frames.
    /// </summary>
    [Theory]
    [InlineData("<result>5__2")]
    [InlineData("<response>5__1")]
    [InlineData("<>1__state")]
    [InlineData("<>t__builder")]
    [InlineData("<>7__wrap1")]
    public void AsyncVariableNames_ShouldNotContainAngleBracketPrefixes(string compilerGeneratedName)
    {
        var strippedName = AsyncStackTraceService.StripStateMachineFieldName(compilerGeneratedName);

        strippedName.Should().NotStartWith("<",
            "variable names shown to users should not start with '<'");
        strippedName.Should().NotContain("<>",
            "variable names should not contain '<>' compiler prefix");
    }

    private static IDebugSessionManager CreateMockSessionManager(IReadOnlyList<StackFrame> frames)
    {
        var mock = new Mock<IDebugSessionManager>();

        var session = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/usr/bin/testapp",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Launch,
            State = SessionState.Paused,
            ActiveThreadId = 1,
            PauseReason = PauseReason.Breakpoint
        };

        mock.Setup(m => m.CurrentSession).Returns(session);
        mock.Setup(m => m.GetStackFrames(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((frames, frames.Count));

        return mock.Object;
    }
}
