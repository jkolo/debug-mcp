namespace DebugMcp.Models.Inspection;

/// <summary>
/// Error marker for a variable that could not be inspected.
/// </summary>
/// <param name="Name">Variable name.</param>
/// <param name="Error">Error message describing why inspection failed.</param>
public sealed record VariableError(
    string Name,
    string Error);
