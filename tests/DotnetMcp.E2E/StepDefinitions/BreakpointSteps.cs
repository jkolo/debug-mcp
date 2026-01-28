using DotnetMcp.E2E.Support;
using DotnetMcp.Models.Breakpoints;
using DotnetMcp.Tests.Helpers;

namespace DotnetMcp.E2E.StepDefinitions;

[Binding]
public sealed class BreakpointSteps
{
    private readonly DebuggerContext _ctx;

    public BreakpointSteps(DebuggerContext ctx)
    {
        _ctx = ctx;
    }

    [Given(@"a breakpoint on ""(.*)"" line (\d+)")]
    public async Task GivenABreakpointOnLine(string file, int line)
    {
        var sourceFile = TestTargetProcess.GetSourceFilePath(file);
        var bp = await _ctx.BreakpointManager.SetBreakpointAsync(
            sourceFile, line, column: null, condition: null, CancellationToken.None);
        _ctx.LastSetBreakpoint = bp;
        _ctx.SetBreakpoints.Add(bp);
    }

    [Given(@"a conditional breakpoint on ""(.*)"" line (\d+) with condition ""(.*)""")]
    public async Task GivenAConditionalBreakpointOnLineWithCondition(string file, int line, string condition)
    {
        var sourceFile = TestTargetProcess.GetSourceFilePath(file);
        var bp = await _ctx.BreakpointManager.SetBreakpointAsync(
            sourceFile, line, column: null, condition: condition, CancellationToken.None);
        _ctx.LastSetBreakpoint = bp;
        _ctx.SetBreakpoints.Add(bp);
    }

    [When(@"the test target executes the ""(.*)"" command")]
    public async Task WhenTheTestTargetExecutesTheCommand(string command)
    {
        await _ctx.TargetProcess!.SendCommandAsync(command);
    }

    [When("I wait for a breakpoint hit")]
    public async Task WhenIWaitForABreakpointHit()
    {
        _ctx.LastBreakpointHit = await _ctx.BreakpointManager.WaitForBreakpointAsync(
            TimeSpan.FromSeconds(10), CancellationToken.None);
        _ctx.LastBreakpointHit.Should().NotBeNull("breakpoint should have been hit");
    }

    [When("I remove the breakpoint")]
    public async Task WhenIRemoveTheBreakpoint()
    {
        _ctx.LastSetBreakpoint.Should().NotBeNull();
        await _ctx.BreakpointManager.RemoveBreakpointAsync(
            _ctx.LastSetBreakpoint!.Id, CancellationToken.None);
    }

    [Then(@"the debugger should pause at ""(.*)"" line (\d+)")]
    public void ThenTheDebuggerShouldPauseAtLine(string file, int line)
    {
        _ctx.LastBreakpointHit.Should().NotBeNull();
        _ctx.LastBreakpointHit!.BreakpointId.Should().Be(_ctx.LastSetBreakpoint!.Id);
    }

    [Then(@"the breakpoint hit count should be (\d+)")]
    public void ThenTheBreakpointHitCountShouldBe(int expectedCount)
    {
        _ctx.LastBreakpointHit.Should().NotBeNull();
        _ctx.LastBreakpointHit!.HitCount.Should().Be(expectedCount);
    }

    [Then(@"the debugger should not pause within (\d+) seconds")]
    public async Task ThenTheDebuggerShouldNotPauseWithinSeconds(int seconds)
    {
        var hit = await _ctx.BreakpointManager.WaitForBreakpointAsync(
            TimeSpan.FromSeconds(seconds), CancellationToken.None);
        hit.Should().BeNull("no breakpoint should have been hit");
    }
}
