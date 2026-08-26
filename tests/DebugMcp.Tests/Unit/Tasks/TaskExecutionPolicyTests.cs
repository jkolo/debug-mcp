using AwesomeAssertions;
using DebugMcp.Services.Tasks;
using DebugMcp.Tests.Support;
using ModelContextProtocol.Extensions.Tasks;
using Xunit;

namespace DebugMcp.Tests.Unit.Tasks;

/// <summary>
/// FR-013: exactly the five long-running tools may ever be deferred; every other tool must be
/// pinned to Synchronous regardless of client capability declaration. The wire-level guarantee
/// that opt-in gating itself works is verified separately in <c>McpTasksHarnessTests</c> — this
/// class only checks OUR classification table.
/// </summary>
public class TaskExecutionPolicyTests
{
    private static readonly string[] ExpectedQualifyingTools =
    [
        "resharper_inspect_solution",
        "resharper_inspect_project",
        "batch_evaluate",
        "debug_launch",
        "code_load",
    ];

    [Fact]
    public void QualifyingTools_MatchExactlyTheFR013List()
    {
        TaskExecutionPolicy.QualifyingTools.Should().BeEquivalentTo(ExpectedQualifyingTools);
    }

    [Fact]
    public void AllRegisteredTools_ClassifyConsistentlyWithQualifyingSet()
    {
        var allToolNames = McpToolDiscovery.GetAllToolMethods()
            .Select(t => t.Name)
            .ToList();

        allToolNames.Should().NotBeEmpty();

        foreach (var name in allToolNames)
        {
            var mode = TaskExecutionPolicy.GetMode(name);
            var expected = ExpectedQualifyingTools.Contains(name)
                ? McpTaskExecutionMode.Optional
                : McpTaskExecutionMode.Synchronous;

            mode.Should().Be(expected, because: $"tool '{name}' must classify as {expected}");
        }
    }

    [Fact]
    public void UnknownToolName_ClassifiesAsSynchronous()
    {
        TaskExecutionPolicy.GetMode("no_such_tool").Should().Be(McpTaskExecutionMode.Synchronous);
    }

    [Fact]
    public void NullToolName_ClassifiesAsSynchronous()
    {
        TaskExecutionPolicy.GetMode(null).Should().Be(McpTaskExecutionMode.Synchronous);
    }
}
