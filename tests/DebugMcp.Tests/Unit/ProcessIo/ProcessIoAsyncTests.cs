using System.Reflection;
using DebugMcp.Tools;
using FluentAssertions;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Unit.ProcessIo;

/// <summary>
/// Contract tests verifying that process I/O tools have honest synchronous signatures
/// (feature 030 US4 — no fake-async Task.FromResult wrappers).
/// </summary>
public class ProcessIoAsyncTests
{
    [Fact]
    public void ProcessReadOutputTool_ReadOutput_ReturnsString_NotTask()
    {
        var toolType = typeof(ProcessReadOutputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_read_output");

        method.Should().NotBeNull("process_read_output tool method should exist");
        method!.ReturnType.Should().Be(typeof(string),
            "process_read_output should return string, not Task<string> (no real async work)");
    }

    [Fact]
    public void ProcessWriteInputTool_WriteInput_ReturnsString_NotTask()
    {
        var toolType = typeof(ProcessWriteInputTool);
        var method = toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == "process_write_input");

        method.Should().NotBeNull("process_write_input tool method should exist");
        method!.ReturnType.Should().Be(typeof(string),
            "process_write_input should return string, not Task<string> (no real async work)");
    }
}
