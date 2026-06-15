using System.Text.Json;
using System.Text.Json.Serialization;

namespace DebugMcp.Models.ReSharper;

/// <summary>
/// Native ReSharper inspection severity, exposed verbatim (no remap to Roslyn's scale).
/// Ordered by descending importance so a minimum-severity threshold filter is a simple
/// comparison: Error (highest) &gt; Warning &gt; Suggestion &gt; Hint (lowest).
/// </summary>
public enum ReSharperSeverity
{
    /// <summary>Lowest — ReSharper HINT.</summary>
    Hint = 0,

    /// <summary>ReSharper SUGGESTION.</summary>
    Suggestion = 1,

    /// <summary>ReSharper WARNING.</summary>
    Warning = 2,

    /// <summary>Highest — ReSharper ERROR.</summary>
    Error = 3
}

/// <summary>
/// Serializes <see cref="ReSharperSeverity"/> as lower-case (error/warning/suggestion/hint),
/// consistent with the per-severity keys in <c>InspectionResult.Summary</c> and the casing
/// used across the other tools. Reads are case-insensitive.
/// </summary>
public sealed class ReSharperSeverityJsonConverter : JsonConverter<ReSharperSeverity>
{
    public override ReSharperSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Enum.Parse<ReSharperSeverity>(reader.GetString() ?? nameof(ReSharperSeverity.Warning), ignoreCase: true);

    public override void Write(Utf8JsonWriter writer, ReSharperSeverity value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
