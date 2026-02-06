namespace DebugMcp.Models.Inspection;

/// <summary>
/// Stack frame enriched with optional variable data for exception autopsy.
/// </summary>
/// <param name="Index">Frame index (0 = top of stack).</param>
/// <param name="Function">Function name.</param>
/// <param name="Module">Module name.</param>
/// <param name="IsExternal">True if no source/symbols available.</param>
/// <param name="Location">File, line, column (null if no symbols).</param>
/// <param name="Arguments">Function arguments (null if unavailable).</param>
/// <param name="Variables">Local variables (only for frames within include_variables_for_frames depth).</param>
public sealed record AutopsyFrame(
    int Index,
    string Function,
    string Module,
    bool IsExternal,
    SourceLocation? Location = null,
    IReadOnlyList<Variable>? Arguments = null,
    FrameVariables? Variables = null);
