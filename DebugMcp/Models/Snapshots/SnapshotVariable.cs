using DebugMcp.Models.Inspection;

namespace DebugMcp.Models.Snapshots;

/// <summary>
/// A single captured variable within a snapshot.
/// </summary>
/// <param name="Name">Variable name (e.g., "retryCount").</param>
/// <param name="Path">Dot-separated path (e.g., "order.Customer.Name").</param>
/// <param name="Type">CLR type name (e.g., "System.Int32").</param>
/// <param name="Value">String representation (same format as variables_get).</param>
/// <param name="Scope">Where this variable comes from (Local, Argument, This, Field, etc.).</param>
/// <param name="Children">Expanded child variables when depth > 0.</param>
public sealed record SnapshotVariable(
    string Name,
    string Path,
    string Type,
    string Value,
    VariableScope Scope,
    IReadOnlyList<SnapshotVariable>? Children = null);
