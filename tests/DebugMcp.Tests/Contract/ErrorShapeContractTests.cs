using AwesomeAssertions;
using DebugMcp.Models.Results;
using DebugMcp.Tests.Support;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-018/FR-019: one failure shape for all 39 tools — <c>code</c>, <c>message</c>, optional
/// <c>details</c>, with <c>code</c> drawn from the existing <c>ErrorCodes</c> set. Verified
/// structurally: every tool's flat result record must expose an <c>Error</c> property of the
/// shared <see cref="ToolError"/> type (not a bespoke per-tool error shape), which is what makes
/// "one shape for all 39 tools" true by construction rather than by convention.
/// </summary>
public class ErrorShapeContractTests
{
    [Fact]
    public void AllTools_ExposeTheSharedToolErrorType()
    {
        var tools = McpToolDiscovery.GetAllToolMethods().Where(t => t.Attribute.UseStructuredContent).ToList();
        tools.Should().NotBeEmpty();

        var violations = new List<string>();
        foreach (var t in tools)
        {
            var resultType = ToolResultShape.GetResultType(t.Method);
            var errorProperty = resultType.GetProperty("Error");
            if (errorProperty is null)
            {
                violations.Add($"{t.Name}: {resultType.Name} has no Error property");
                continue;
            }

            if (errorProperty.PropertyType != typeof(ToolError))
            {
                violations.Add($"{t.Name}: Error property is {errorProperty.PropertyType.Name}, expected {nameof(ToolError)}");
            }
        }

        violations.Should().BeEmpty(because: "every tool must fail through the shared ToolError shape, not an invented one");
    }
}
