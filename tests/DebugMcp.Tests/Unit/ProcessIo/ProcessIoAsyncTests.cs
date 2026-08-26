using System.Reflection;
using DebugMcp.Models.Results;
using DebugMcp.Services;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Unit.ProcessIo;

/// <summary>
/// Contract + behavior tests for the process I/O tools. Feature 030 US4 deliberately kept these
/// synchronous ("no fake-async Task.FromResult wrappers"); feature 069 FR-001 supersedes that
/// decision — every one of the 39 tools becomes uniformly asynchronous and cancellable, which
/// this project's own convention (dotnet-csharp.md) treats as a legitimate use of
/// <c>Task.FromResult</c> for a synchronous result behind an async-shaped interface, distinct
/// from the <c>Task.Run</c> anti-pattern 030's comment was actually warning against. US3
/// (typed structured results, 069) further supersedes the hand-rolled JSON-string return type
/// with the tools' own flat result records.
///
/// <c>ProcessIoManager</c> is a sealed concrete class with no interface, so it cannot be mocked
/// with Moq; a freshly constructed real instance (no process attached) is used instead, which is
/// enough to exercise every validation/error branch these tools have.
/// </summary>
public class ProcessIoAsyncTests
{
    private static ProcessIoManager NewIoManager() => new(NullLogger<ProcessIoManager>.Instance);

    [Fact]
    public void ProcessReadOutputTool_ReadOutputAsync_ReturnsTaskOfProcessReadOutputResult()
    {
        var toolType = typeof(ProcessReadOutputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_read_output");

        method.Should().NotBeNull("process_read_output tool method should exist");
        method!.ReturnType.Should().Be(typeof(Task<ProcessReadOutputResult>),
            "every tool must return Task<T> for its own typed result record (feature 069 US3), " +
            "superseding both 030 US4's synchronous-only decision and the pre-US3 Task<string> shape");
        method.GetCustomAttribute<McpServerToolAttribute>()!.UseStructuredContent.Should().BeTrue();
    }

    [Fact]
    public void ProcessWriteInputTool_WriteInputAsync_ReturnsTaskOfProcessWriteInputResult()
    {
        var toolType = typeof(ProcessWriteInputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_write_input");

        method.Should().NotBeNull("process_write_input tool method should exist");
        method!.ReturnType.Should().Be(typeof(Task<ProcessWriteInputResult>),
            "every tool must return Task<T> for its own typed result record (feature 069 US3), " +
            "superseding both 030 US4's synchronous-only decision and the pre-US3 Task<string> shape");
        method.GetCustomAttribute<McpServerToolAttribute>()!.UseStructuredContent.Should().BeTrue();
    }

    [Fact]
    public async Task ReadOutputAsync_InvalidStream_ReturnsInvalidParameterError()
    {
        var tool = new ProcessReadOutputTool(NewIoManager(), NullLogger<ProcessReadOutputTool>.Instance);

        var result = await tool.ReadOutputAsync(stream: "bogus");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_PARAMETER");
        result.Stdout.Should().BeNull();
        result.Stderr.Should().BeNull();
    }

    [Fact]
    public async Task ReadOutputAsync_NoProcessAttached_ReturnsNoSessionError()
    {
        var tool = new ProcessReadOutputTool(NewIoManager(), NullLogger<ProcessReadOutputTool>.Instance);

        var result = await tool.ReadOutputAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NO_SESSION");
    }

    [Fact]
    public async Task WriteInputAsync_NoProcessAttached_ReturnsNoSessionError()
    {
        var tool = new ProcessWriteInputTool(NewIoManager(), NullLogger<ProcessWriteInputTool>.Instance);

        var result = await tool.WriteInputAsync("hello");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NO_SESSION");
        result.BytesWritten.Should().BeNull();
    }
}
