namespace DebugMcp.Models.Inspection;

/// <summary>
/// Variables for a single stack frame with per-variable status.
/// </summary>
/// <param name="Locals">Successfully retrieved local variables (one-level expanded).</param>
/// <param name="Errors">Variables that failed to inspect (null if all succeeded).</param>
public sealed record FrameVariables(
    IReadOnlyList<Variable> Locals,
    IReadOnlyList<VariableError>? Errors = null);
