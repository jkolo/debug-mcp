namespace DebugMcp.Models.Breakpoints;

/// <summary>
/// Compact variable representation included in breakpointHit notifications.
/// </summary>
public sealed record VariableSummary(
    string Name,
    string Type,
    string Value,
    bool HasChildren);
