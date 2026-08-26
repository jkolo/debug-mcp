using AwesomeAssertions;
using DebugMcp.Tests.Support;
using ModelContextProtocol.Server;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-016/FR-020: every one of the 39 tools must publish an <c>outputSchema</c>. Fails the build
/// per contracts/tool-result-contract.md's condition 1 until every tool sets
/// <c>UseStructuredContent = true</c> and returns a typed record (US3, T044–T052).
/// </summary>
public class OutputSchemaPresenceTests
{
    [Fact]
    public void AllTools_PublishAnOutputSchema()
    {
        var tools = McpToolDiscovery.GetAllToolMethods();
        tools.Should().NotBeEmpty();

        var missing = new List<string>();
        foreach (var t in tools)
        {
            if (!t.Attribute.UseStructuredContent)
            {
                missing.Add($"{t.Name}: UseStructuredContent is false");
                continue;
            }

            var options = new McpServerToolCreateOptions
            {
                Name = t.Attribute.Name,
                Title = t.Attribute.Title,
                UseStructuredContent = t.Attribute.UseStructuredContent,
            };
            var instance = ToolResultShape.UninitializedToolInstance(t.ToolType);
            var mcpTool = McpServerTool.Create(t.Method, instance, options);

            if (mcpTool.ProtocolTool.OutputSchema is null)
            {
                missing.Add($"{t.Name}: no outputSchema generated");
            }
        }

        missing.Should().BeEmpty(because: "every tool must publish an outputSchema (FR-016/FR-020)");
    }
}
