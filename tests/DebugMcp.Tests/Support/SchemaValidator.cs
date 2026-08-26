using System.Text.Json;
using Json.Schema;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Validates a tool's serialized result against its own published <c>outputSchema</c> (FR-020,
/// US3/US4 contract tests). Backed by a real JSON Schema implementation
/// (<see href="https://github.com/gregsdennis/json-everything">json-everything</see>) rather
/// than a hand-rolled subset, so a validator bug never masquerades as a passing contract test.
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Validates <paramref name="instance"/> against <paramref name="schema"/>.
    /// </summary>
    /// <returns>Empty when valid; otherwise one message per violated schema location.</returns>
    public static IReadOnlyList<string> Validate(JsonElement schema, JsonElement instance)
    {
        var compiled = JsonSchema.FromText(schema.GetRawText());
        var results = compiled.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return [];
        }

        return CollectErrors(results).ToList();
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults results)
    {
        if (!results.IsValid && results.Errors is { Count: > 0 })
        {
            foreach (var (keyword, message) in results.Errors)
            {
                yield return $"{results.InstanceLocation}: [{keyword}] {message}";
            }
        }

        foreach (var detail in results.Details ?? [])
        {
            foreach (var error in CollectErrors(detail))
            {
                yield return error;
            }
        }
    }
}
