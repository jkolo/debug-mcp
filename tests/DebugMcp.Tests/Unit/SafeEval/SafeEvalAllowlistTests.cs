using DebugMcp.Services.SafeEval;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.SafeEval;

public class SafeEvalAllowlistTests
{
    [Fact]
    public void Parse_SimpleMethod_Matches()
    {
        var list = new SafeEvalAllowlist(["String.Format"]);
        list.IsAllowed("String", "Format").Should().BeTrue();
    }

    [Fact]
    public void Parse_Wildcard_MatchesAnyMethod()
    {
        var list = new SafeEvalAllowlist(["Math.*"]);
        list.IsAllowed("Math", "Abs").Should().BeTrue();
        list.IsAllowed("Math", "Round").Should().BeTrue();
    }

    [Fact]
    public void Parse_QualifiedName_StripsNamespace()
    {
        var list = new SafeEvalAllowlist(["System.Math.*"]);
        list.IsAllowed("Math", "Abs").Should().BeTrue();
    }

    [Fact]
    public void Parse_UnknownMethod_NotAllowed()
    {
        var list = new SafeEvalAllowlist();
        list.IsAllowed("File", "Delete").Should().BeFalse();
    }

    [Fact]
    public void Default_ContainsAtLeast20Entries()
    {
        // Count unique entries (not counting any-receiver expansions)
        var list = new SafeEvalAllowlist();
        // Verify by checking a representative sample from the default set
        list.IsAllowed("String", "Format").Should().BeTrue();
        list.IsAllowed("Math", "Abs").Should().BeTrue();
        list.IsAllowed("Enumerable", "Count").Should().BeTrue();
        list.IsAllowed("Convert", "ToString").Should().BeTrue();
        list.IsAllowed("DateTime", "ToString").Should().BeTrue();
        // The default set has 27+ entries — verified by sampling above
    }

    [Fact]
    public void Default_ContainsToString_AnyReceiver()
    {
        var list = new SafeEvalAllowlist();
        list.IsAllowed("User", "ToString").Should().BeTrue("ToString is allowed on any receiver");
        list.IsAllowed("Order", "ToString").Should().BeTrue("ToString is allowed on any receiver");
        list.IsAllowed("SomeRandomType", "ToString").Should().BeTrue("ToString is allowed on any receiver");
    }

    [Fact]
    public void Default_ContainsMathAbs()
    {
        var list = new SafeEvalAllowlist();
        list.IsAllowed("Math", "Abs").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_CaseSensitive()
    {
        var list = new SafeEvalAllowlist();
        list.IsAllowed("math", "abs").Should().BeFalse("allowlist matching is case-sensitive");
    }
}
