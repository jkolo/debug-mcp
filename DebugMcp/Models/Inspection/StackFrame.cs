namespace DebugMcp.Models.Inspection;

/// <summary>
/// Represents a single frame in the call stack.
/// </summary>
/// <param name="Index">Frame index (0 = top of stack).</param>
/// <param name="Function">Full method name (Namespace.Class.Method).</param>
/// <param name="Module">Assembly name (e.g., "MyApp.dll").</param>
/// <param name="IsExternal">True if no source available (framework code).</param>
/// <param name="Location">Source file/line if symbols available.</param>
/// <param name="Arguments">Method arguments with values.</param>
/// <param name="FrameKind">Frame classification: "sync", "async", or "async_continuation".</param>
/// <param name="IsAwaiting">True if this async frame is suspended at an await point.</param>
/// <param name="LogicalFunction">Original async method name when Function is a MoveNext frame.</param>
public sealed record StackFrame(
    int Index,
    string Function,
    string Module,
    bool IsExternal,
    SourceLocation? Location = null,
    IReadOnlyList<Variable>? Arguments = null,
    string FrameKind = "sync",
    bool IsAwaiting = false,
    string? LogicalFunction = null);
