using System.Reflection;
using DebugMcp.Tools;
using AwesomeAssertions;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Unit.ProcessIo;

/// <summary>
/// Contract tests for process I/O tool signatures. Feature 030 US4 deliberately kept these
/// synchronous ("no fake-async Task.FromResult wrappers"); feature 069 FR-001 supersedes that
/// decision — every one of the 39 tools becomes uniformly asynchronous and cancellable, which
/// this project's own convention (dotnet-csharp.md) treats as a legitimate use of
/// <c>Task.FromResult</c> for a synchronous result behind an async-shaped interface, distinct
/// from the <c>Task.Run</c> anti-pattern 030's comment was actually warning against.
/// </summary>
public class ProcessIoAsyncTests
{
    [Fact]
    public void ProcessReadOutputTool_ReadOutputAsync_ReturnsTaskOfString()
    {
        var toolType = typeof(ProcessReadOutputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_read_output");

        method.Should().NotBeNull("process_read_output tool method should exist");
        method!.ReturnType.Should().Be(typeof(Task<string>),
            "every tool must return Task<string> (feature 069 FR-001), superseding 030 US4's synchronous-only decision");
    }

    [Fact]
    public void ProcessWriteInputTool_WriteInputAsync_ReturnsTaskOfString()
    {
        var toolType = typeof(ProcessWriteInputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_write_input");

        method.Should().NotBeNull("process_write_input tool method should exist");
        method!.ReturnType.Should().Be(typeof(Task<string>),
            "every tool must return Task<string> (feature 069 FR-001), superseding 030 US4's synchronous-only decision");
    }
}
