using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Tests.Support;
using ModelContextProtocol.Server;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-020: a tool's actual structured result must validate against its own published
/// <c>outputSchema</c> — condition 2 of contracts/tool-result-contract.md's build-time
/// enforcement. Exercises both a "success" and a "failure" shaped instance of each tool's result
/// record via <see cref="ToolResultShape"/>, since a failure result omits every domain field and
/// is exactly the shape that caught the requiredness bug documented in data-model.md §1.
/// </summary>
public class OutputSchemaConformanceTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void AllTools_SuccessAndFailureInstances_ConformToTheirOwnSchema()
    {
        var tools = McpToolDiscovery.GetAllToolMethods().Where(t => t.Attribute.UseStructuredContent).ToList();
        tools.Should().NotBeEmpty();

        var violations = new List<string>();
        foreach (var t in tools)
        {
            var options = new McpServerToolCreateOptions
            {
                Name = t.Attribute.Name,
                Title = t.Attribute.Title,
                UseStructuredContent = true,
            };
            var toolInstance = ToolResultShape.UninitializedToolInstance(t.ToolType);
            var mcpTool = McpServerTool.Create(t.Method, toolInstance, options);
            var schema = mcpTool.ProtocolTool.OutputSchema;
            if (schema is null)
            {
                violations.Add($"{t.Name}: no outputSchema (see OutputSchemaPresenceTests)");
                continue;
            }

            var resultType = ToolResultShape.GetResultType(t.Method);
            foreach (var success in new[] { true, false })
            {
                var resultInstance = ToolResultShape.BuildInstance(resultType, success);
                var element = JsonSerializer.SerializeToElement(resultInstance, resultType, WireOptions);
                var errors = SchemaValidator.Validate(schema.Value, element);
                if (errors.Count > 0)
                {
                    violations.Add($"{t.Name} (success={success}): {string.Join("; ", errors)}");
                }
            }
        }

        violations.Should().BeEmpty(because: "every tool's success and failure results must validate against its published outputSchema");
    }
}
