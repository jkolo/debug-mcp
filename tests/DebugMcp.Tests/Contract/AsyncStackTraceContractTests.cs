using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Tools;
using AwesomeAssertions;
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
    public async Task StacktraceGet_Response_IncludesFrameKindOnEveryFrame()
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
        var result = await tool.GetStackTraceAsync();

        // Assert
        result.Frames.Should().NotBeNull();
        foreach (var frame in result.Frames!)
        {
            frame.FrameKind.Should().BeOneOf("sync", "async", "async_continuation",
                "frame_kind must be sync, async, or async_continuation");
        }
    }

    /// <summary>
    /// T007: The include_raw parameter should be accepted without error.
    /// </summary>
    [Fact]
    public async Task StacktraceGet_IncludeRawParameter_AcceptedWithoutError()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false)
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = await tool.GetStackTraceAsync(include_raw: true);

        // Assert
        result.Success.Should().BeTrue(
            "include_raw parameter should be accepted without causing an error");
    }

    /// <summary>
    /// T007 (continued): Verify include_raw parameter exists on the tool method via reflection.
    /// </summary>
    [Fact]
    public void StacktraceGet_IncludeRawParameter_ExistsOnToolMethod()
    {
        var method = typeof(StacktraceGetTool).GetMethod("GetStackTraceAsync");
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
    public async Task StacktraceGet_Response_BackwardCompatible()
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
        var result = await tool.GetStackTraceAsync();

        // Assert — top-level required fields
        result.Success.Should().BeTrue();
        result.ThreadId.Should().NotBeNull("response must include thread_id");
        result.TotalFrames.Should().NotBeNull("response must include total_frames");
        result.Frames.Should().NotBeNull("response must include frames");

        // Assert — each frame has required fields (non-nullable on the record, always present on the wire)
        foreach (var frame in result.Frames!)
        {
            frame.Index.Should().BeGreaterThanOrEqualTo(0, "frame must include index");
            frame.Function.Should().NotBeNullOrEmpty("frame must include function");
            frame.Module.Should().NotBeNullOrEmpty("frame must include module");
        }
    }

    /// <summary>
    /// T008 (continued): New fields are additive — they don't break the existing schema.
    /// </summary>
    [Fact]
    public async Task StacktraceGet_Response_NewFieldsAreAdditive()
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
        var result = await tool.GetStackTraceAsync();
        var frame = result.Frames!.First();

        // Assert — new fields present
        frame.FrameKind.Should().Be("async");
        frame.IsAwaiting.Should().BeTrue();
        frame.LogicalFunction.Should().Be("GetDataAsync");

        // Assert — existing fields still present
        frame.Index.Should().Be(0);
        frame.Function.Should().Be("MyApp.Service.GetDataAsync()");
        frame.Module.Should().Be("MyApp.dll");
        frame.IsExternal.Should().BeFalse();
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
    public async Task StacktraceGet_Response_OmitsNullLogicalFunction()
    {
        // Arrange
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false) // sync, no logical_function
        };

        var sessionManager = CreateMockSessionManager(frames);
        var tool = new StacktraceGetTool(sessionManager, NullLogger<StacktraceGetTool>.Instance);

        // Act
        var result = await tool.GetStackTraceAsync();
        result.Frames!.First().LogicalFunction.Should().BeNull();

        // Assert — omitted from the actual wire JSON too, not just null in-memory
        var wireOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        var element = JsonSerializer.SerializeToElement(result, wireOptions);
        var frame = element.GetProperty("frames").EnumerateArray().First();
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
