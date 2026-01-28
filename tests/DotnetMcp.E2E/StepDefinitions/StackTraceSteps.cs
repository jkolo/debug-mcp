using DotnetMcp.E2E.Support;

namespace DotnetMcp.E2E.StepDefinitions;

[Binding]
public sealed class StackTraceSteps
{
    private readonly DebuggerContext _ctx;

    public StackTraceSteps(DebuggerContext ctx)
    {
        _ctx = ctx;
    }

    [When("I request the stack trace")]
    public void WhenIRequestTheStackTrace()
    {
        var result = _ctx.SessionManager.GetStackFrames();
        _ctx.LastStackTrace = result.Frames.ToArray();
    }

    [Then(@"the stack trace should contain at least (\d+) frames")]
    public void ThenTheStackTraceShouldContainAtLeastFrames(int minFrames)
    {
        _ctx.LastStackTrace.Should().NotBeNull();
        _ctx.LastStackTrace!.Length.Should().BeGreaterThanOrEqualTo(minFrames);
    }

    [Then(@"the stack trace should contain method ""(.*)""")]
    public void ThenTheStackTraceShouldContainMethod(string methodName)
    {
        _ctx.LastStackTrace.Should().NotBeNull();
        _ctx.LastStackTrace!.Should().Contain(
            f => f.Function != null && f.Function.Contains(methodName),
            $"stack trace should contain method '{methodName}'");
    }
}
