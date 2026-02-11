namespace DebugMcp.Models.Snapshots;

/// <summary>
/// Categorizes a change within a snapshot diff.
/// </summary>
public enum DiffChangeType
{
    /// <summary>Variable exists in snapshot B but not A.</summary>
    Added,

    /// <summary>Variable exists in snapshot A but not B.</summary>
    Removed,

    /// <summary>Variable exists in both snapshots with different values.</summary>
    Modified
}

/// <summary>
/// A single change within a snapshot diff.
/// </summary>
/// <param name="Name">Variable name.</param>
/// <param name="Path">Full dot-separated path.</param>
/// <param name="Type">CLR type name.</param>
/// <param name="OldValue">Value in snapshot A (null for added).</param>
/// <param name="NewValue">Value in snapshot B (null for removed).</param>
/// <param name="ChangeType">Whether the variable was added, removed, or modified.</param>
public sealed record DiffEntry(
    string Name,
    string Path,
    string Type,
    string? OldValue,
    string? NewValue,
    DiffChangeType ChangeType);
