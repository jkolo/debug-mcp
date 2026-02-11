using DebugMcp.Models.Inspection;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services;

/// <summary>
/// Abstracts field reading for continuation chain walking, enabling unit testing without ICorDebug.
/// In production, wraps ProcessDebugger.TryGetFieldValue.
/// In tests, uses mock dictionaries.
/// </summary>
public delegate object? FieldReaderDelegate(object value, string fieldName);

/// <summary>
/// Returns the type name of a debug value, used for state machine type detection.
/// </summary>
public delegate string? TypeNameReaderDelegate(object value);

/// <summary>
/// Service for building logical async stack traces by walking Task continuation chains.
/// </summary>
public interface IAsyncStackTraceService
{
    /// <summary>
    /// Builds a logical frame list by appending continuation chain frames to physical frames.
    /// For each async frame on the physical stack, walks Task.m_continuationObject to discover
    /// suspended async callers and appends them as "async_continuation" frames.
    /// </summary>
    /// <param name="physicalFrames">Physical stack frames from ICorDebug.</param>
    /// <param name="fieldReader">Reads a named field from a debug value object.</param>
    /// <param name="typeNameReader">Gets the type name of a debug value object.</param>
    /// <param name="startingTaskValue">Optional starting Task value for chain walking.
    /// When null, the service derives the key from the top async frame's LogicalFunction (for testing).</param>
    /// <returns>Extended frame list with continuation chain frames appended.</returns>
    IReadOnlyList<StackFrame> BuildLogicalFrames(
        IReadOnlyList<StackFrame> physicalFrames,
        FieldReaderDelegate fieldReader,
        TypeNameReaderDelegate typeNameReader,
        object? startingTaskValue = null);
}

/// <summary>
/// Walks Task.m_continuationObject chains to discover async callers not on the physical stack.
/// </summary>
public class AsyncStackTraceService : IAsyncStackTraceService
{
    private const int MaxChainDepth = 50;
    private readonly ILogger<AsyncStackTraceService> _logger;

    public AsyncStackTraceService(ILogger<AsyncStackTraceService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<StackFrame> BuildLogicalFrames(
        IReadOnlyList<StackFrame> physicalFrames,
        FieldReaderDelegate fieldReader,
        TypeNameReaderDelegate typeNameReader,
        object? startingTaskValue = null)
    {
        var result = new List<StackFrame>(physicalFrames);

        // Find the topmost async frame on the physical stack
        var topAsyncFrame = physicalFrames.FirstOrDefault(f => f.FrameKind == "async");
        if (topAsyncFrame == null)
        {
            _logger.LogDebug("No async frames on physical stack, skipping chain walking");
            return result;
        }

        // Use provided starting value or derive from frame (for mock testing)
        var taskValue = startingTaskValue ?? $"task:{topAsyncFrame.LogicalFunction}";

        try
        {
            var continuationFrames = WalkContinuationChain(taskValue, fieldReader, typeNameReader);
            var nextIndex = result.Count;
            foreach (var (methodName, stateMachineType) in continuationFrames)
            {
                result.Add(new StackFrame(
                    Index: nextIndex++,
                    Function: $"{methodName}()",
                    Module: topAsyncFrame.Module,
                    IsExternal: false,
                    FrameKind: "async_continuation",
                    IsAwaiting: true,
                    LogicalFunction: methodName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error walking continuation chain for {Method}", topAsyncFrame.LogicalFunction);
        }

        return result;
    }

    /// <summary>
    /// Strips compiler-generated angle-bracket naming from state machine field names.
    /// Heuristic fallback when PDB StateMachineHoistedLocalScopes info is unavailable.
    /// Examples: "<![CDATA[<result>5__2]]>" → "result", "<![CDATA[<>1__state]]>" → "__state"
    /// </summary>
    public static string StripStateMachineFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName) || !fieldName.StartsWith('<'))
            return fieldName;

        // Pattern: <name>N__suffix — hoisted local variable
        // e.g., <result>5__2 → result
        var match = System.Text.RegularExpressions.Regex.Match(fieldName, @"^<(.+?)>\d+__\d+$");
        if (match.Success)
            return match.Groups[1].Value;

        // Pattern: <>X__suffix — internal state machine fields
        // e.g., <>1__state → __state, <>t__builder → __builder, <>7__wrap1 → __wrap1
        var internalMatch = System.Text.RegularExpressions.Regex.Match(fieldName, @"^<>\w+?__(\w+)$");
        if (internalMatch.Success)
            return $"__{internalMatch.Groups[1].Value}";

        // Fallback: return as-is
        return fieldName;
    }

    /// <summary>
    /// Walks the continuation chain from a Task value by reading m_continuationObject.
    /// </summary>
    private List<(string MethodName, string? StateMachineType)> WalkContinuationChain(
        object taskValue,
        FieldReaderDelegate fieldReader,
        TypeNameReaderDelegate typeNameReader)
    {
        var frames = new List<(string MethodName, string? StateMachineType)>();
        var currentTask = taskValue;
        var depth = 0;

        while (depth < MaxChainDepth)
        {
            // Read m_continuationObject from the Task
            var continuation = fieldReader(currentTask, "m_continuationObject");
            if (continuation == null)
            {
                _logger.LogDebug("Chain terminated: null m_continuationObject at depth {Depth}", depth);
                break;
            }

            // Try to extract state machine from the continuation
            var stateMachine = ExtractStateMachineFromContinuation(continuation, fieldReader, typeNameReader);
            if (stateMachine == null)
            {
                _logger.LogDebug("Chain terminated: unresolvable continuation at depth {Depth}", depth);
                break;
            }

            var (smValue, smTypeName) = stateMachine.Value;

            // Parse the state machine type name to get the original method name
            var (isAsync, originalMethodName) = ProcessDebugger.TryParseAsyncStateMachineFrame(
                smTypeName, "MoveNext");

            if (!isAsync || originalMethodName == null)
            {
                _logger.LogDebug("Chain terminated: non-async state machine type {TypeName} at depth {Depth}",
                    smTypeName, depth);
                break;
            }

            frames.Add((originalMethodName, smTypeName));

            // Continue chain: read <>t__builder.m_task to get the next Task
            var builder = fieldReader(smValue, "<>t__builder");
            if (builder == null)
            {
                _logger.LogDebug("Chain terminated: no builder on state machine at depth {Depth}", depth);
                break;
            }

            var nextTask = fieldReader(builder, "m_task");
            if (nextTask == null)
            {
                _logger.LogDebug("Chain terminated: no m_task on builder at depth {Depth}", depth);
                break;
            }

            currentTask = nextTask;
            depth++;
        }

        if (depth >= MaxChainDepth)
        {
            _logger.LogWarning("Continuation chain depth limit ({MaxDepth}) reached", MaxChainDepth);
        }

        return frames;
    }

    /// <summary>
    /// Extracts the state machine instance from a continuation object.
    /// Continuation can be: Action delegate (_target field), or other types.
    /// </summary>
    private (object Value, string TypeName)? ExtractStateMachineFromContinuation(
        object continuation,
        FieldReaderDelegate fieldReader,
        TypeNameReaderDelegate typeNameReader)
    {
        var typeName = typeNameReader(continuation);
        if (typeName == null)
            return null;

        // If it's a delegate (Action, Func, etc.), read _target to get the state machine
        if (typeName.StartsWith("System.Action", StringComparison.Ordinal) ||
            typeName.StartsWith("System.Func", StringComparison.Ordinal) ||
            typeName.Contains("MoveNextRunner", StringComparison.Ordinal))
        {
            var target = fieldReader(continuation, "_target");
            if (target == null)
                return null;

            var targetTypeName = typeNameReader(target);
            if (targetTypeName == null)
                return null;

            return (target, targetTypeName);
        }

        // If it's a Task itself (ContinuationTaskFromTask), try reading m_action._target
        if (typeName.Contains("Task", StringComparison.Ordinal))
        {
            var action = fieldReader(continuation, "m_action");
            if (action != null)
            {
                var target = fieldReader(action, "_target");
                if (target != null)
                {
                    var targetTypeName = typeNameReader(target);
                    if (targetTypeName != null)
                        return (target, targetTypeName);
                }
            }

            // Fallback: try _target directly (some continuation types)
            var directTarget = fieldReader(continuation, "_target");
            if (directTarget != null)
            {
                var directTypeName = typeNameReader(directTarget);
                if (directTypeName != null)
                    return (directTarget, directTypeName);
            }
        }

        _logger.LogDebug("Unresolvable continuation type: {TypeName}", typeName);
        return null;
    }
}
