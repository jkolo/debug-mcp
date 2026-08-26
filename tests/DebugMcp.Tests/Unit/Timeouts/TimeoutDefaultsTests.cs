using DebugMcp.Services.Tasks;
using DebugMcp.Services.Timeouts;

namespace DebugMcp.Tests.Unit.Timeouts;

/// <summary>
/// FR-032: 30s for ordinary blocking tools; the tool's own pre-existing documented default
/// otherwise. Reflection-level parameter-vs-default matching is already asserted generically by
/// <c>TimeoutParameterContractTests</c> for all 31 blocking tools; this file names the two
/// specific exception classes FR-032 calls out, so a regression reads as an intent violation, not
/// just an arbitrary reflection mismatch.
/// </summary>
public sealed class TimeoutDefaultsTests
{
    [Fact]
    public void LongRunningFive_KeepTheirOwnLongerDefault_NotThirtySeconds()
    {
        // The "long-running five" = TaskExecutionPolicy.QualifyingTools (US2) — the same set MCP
        // Tasks deferral applies to, for the same underlying reason: these can genuinely run long.
        foreach (var toolName in TaskExecutionPolicy.QualifyingTools)
        {
            var spec = TimeoutPolicy.Specs[toolName];
            spec.IsBlocking.Should().BeTrue();
        }

        TimeoutPolicy.Specs["resharper_inspect_solution"].DefaultValue.Should().Be(300);
        TimeoutPolicy.Specs["resharper_inspect_project"].DefaultValue.Should().Be(300);
        TimeoutPolicy.Specs["resharper_inspect_solution"].Unit.Should().Be(TimeoutUnit.Seconds);

        // batch_evaluate and debug_launch/code_load are also in the qualifying set (US2's task
        // deferral applies to them for the "can run long" reason too) but their own documented
        // defaults already equal the 30s standard — no FR-032 exception needed for those three.
        TimeoutPolicy.Specs["batch_evaluate"].DefaultValue.Should().Be(30);
        TimeoutPolicy.Specs["debug_launch"].DefaultValue.Should().Be(30000);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("evaluate_safe")]
    [InlineData("object_summarize")]
    [InlineData("collection_analyze")]
    public void PreExistingShorterDefaults_KeptAsIs_NotForcedTo30s(string toolName)
    {
        // FR-032's letter only carves out an exception for LONGER pre-existing defaults. These
        // four already shipped a 5000ms default for a genuinely fast operation; changing it to
        // 30s now would be an unrequested behavior change (data-model.md §1's documented
        // deviation policy: wire/behavior stability wins).
        TimeoutPolicy.Specs[toolName].DefaultValue.Should().Be(5000);
        TimeoutPolicy.Specs[toolName].Unit.Should().Be(TimeoutUnit.Milliseconds);
    }

    [Fact]
    public void OrdinaryBlockingTool_DefaultsToThirtySeconds()
    {
        TimeoutPolicy.Specs["breakpoint_set"].DefaultValue.Should().Be(30000);
        TimeoutPolicy.Specs["stacktrace_get"].DefaultValue.Should().Be(30000);
        TimeoutPolicy.Specs["code_load"].DefaultValue.Should().Be(30000);
    }
}
