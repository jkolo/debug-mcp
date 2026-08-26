namespace DebugMcp.Models.Results;

/// <summary>
/// Uniform envelope every tool returns once migrated (feature 069, US3). Replaces the
/// hand-assembled <c>JsonSerializer.Serialize(new { success = ..., ... })</c> pattern that
/// each tool builds today via its own private <c>CreateErrorResponse</c> helper.
/// </summary>
/// <remarks>
/// <see cref="Success"/> is retained verbatim (FR-021) — existing consumers read this field
/// today. The protocol-level <c>isError</c> flag is derived from it centrally
/// (<c>ToolResultSerializer</c>, US3) rather than set by individual tools.
/// </remarks>
public sealed record ToolResult<T>(
    bool Success,
    T? Data,
    ToolError? Error,
    IReadOnlyList<string>? Warnings = null,
    TruncationInfo? Truncation = null)
{
    /// <summary>Empty by default (constitution: "partial success: return data with warnings array").</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];

    private readonly bool _invariantChecked = Validate(Success, Data, Error);

    private static bool Validate(bool success, T? data, ToolError? error)
    {
        if (success && (error is not null || data is null))
        {
            throw new ArgumentException(
                "A successful ToolResult must have non-null Data and no Error.");
        }

        if (!success && (error is null || data is not null))
        {
            throw new ArgumentException(
                "A failed ToolResult must have a non-null Error and no Data.");
        }

        return true;
    }
}

/// <summary>
/// A tool-execution failure. <see cref="Code"/> must be one of the constants in
/// <see cref="DebugMcp.Models.ErrorCodes"/> — no tool invents a code (FR-019).
/// </summary>
public sealed record ToolError(string Code, string Message, object? Details = null);

/// <summary>Marks a bounded, non-silent truncation of a collection-returning tool's result.</summary>
public sealed record TruncationInfo(int Returned, int? Available, string Reason);
