using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DebugMcp.Prompts;

[McpServerPromptType]
public sealed class DebuggingPrompts
{
    /// <summary>
    /// Server instruction injected into MCP client system prompt.
    /// Pushes the model to use the debugger instead of guessing from source code.
    /// </summary>
    public const string ServerInstruction = """
        You have access to a full .NET debugger through this MCP server. \
        When investigating bugs, exceptions, or unexpected behavior, ALWAYS launch the debugger \
        and verify your hypotheses with real runtime data before proposing fixes. \
        Do NOT guess from source code alone — set breakpoints, inspect variables, evaluate expressions, \
        and confirm root causes with evidence. Reading code can tell you what SHOULD happen; \
        only the debugger tells you what ACTUALLY happens. \
        Use the debugging prompts provided by this server for step-by-step guidance on common scenarios.
        """;

    [McpServerPrompt(Name = "diagnose_exception", Title = "Diagnose Exception")]
    [Description(
        "Step-by-step workflow to diagnose an exception. "
        + "Launches the debugger, sets an exception breakpoint, reproduces the crash, "
        + "and walks through the full exception chain to find root cause.")]
    public static IEnumerable<PromptMessage> DiagnoseException(
        [Description("The exception type to catch, e.g. 'System.NullReferenceException'. "
            + "Use '*' or omit to break on all first-chance exceptions.")]
        string? exceptionType = null,
        [Description("Absolute path to the .NET project or DLL to debug.")]
        string? projectPath = null)
    {
        var exceptionFilter = string.IsNullOrWhiteSpace(exceptionType) || exceptionType == "*"
            ? "all first-chance exceptions"
            : $"`{exceptionType}`";

        var launchStep = string.IsNullOrWhiteSpace(projectPath)
            ? "1. **Launch the target process** using `debug_launch` with the project path."
            : $"1. **Launch the target process** using `debug_launch` with path `{projectPath}`.";

        yield return UserMessage($"""
            # Diagnose Exception

            Follow these steps to find the root cause of an exception:

            {launchStep}
            2. **Set an exception breakpoint** using `breakpoint_set_exception` for {exceptionFilter}.
            3. **Continue execution** with `debug_continue` and reproduce the problem.
            4. When the debugger pauses on the exception:
               - Use `exception_get_context` to get the full exception chain, message, and inner exceptions.
               - Use `stacktrace_get` to see exactly where the exception was thrown.
               - Use `variables_get` to inspect local variables at the throw site.
            5. If the exception has an `InnerException`, examine it — the root cause is often deeper in the chain.
            6. Use `evaluate` to test expressions and verify your hypothesis about why the exception occurred.
            7. Once you identify the root cause, use `debug_disconnect` to end the session.

            **Key principle:** Do NOT guess from the stack trace alone. Inspect the actual runtime values \
            to understand WHY the exception was thrown, not just WHERE.
            """);
    }

    [McpServerPrompt(Name = "find_bug_source", Title = "Find Bug Source")]
    [Description(
        "Systematic workflow to locate the source of a bug. "
        + "Sets breakpoints at suspect locations, steps through execution, "
        + "and narrows down to the exact line where behavior diverges from expectations.")]
    public static IEnumerable<PromptMessage> FindBugSource(
        [Description("Source file path where the bug is suspected.")]
        string file,
        [Description("Line number to set the initial breakpoint.")]
        int line,
        [Description("Short description of expected vs actual behavior, e.g. 'expected count=3 but got 0'.")]
        string? symptom = null)
    {
        var symptomNote = string.IsNullOrWhiteSpace(symptom) ? "" : $"\n**Symptom:** {symptom}\n";

        yield return UserMessage($"""
            # Find Bug Source
            {symptomNote}
            Follow these steps to locate the root cause:

            1. **Launch the target process** using `debug_launch`.
            2. **Set a breakpoint** at `{file}:{line}` using `breakpoint_set`.
            3. **Continue execution** with `debug_continue` and trigger the buggy code path.
            4. When the debugger pauses:
               - Use `variables_get` to inspect all local variables. Do their values match what you expect?
               - Use `members_get` to inspect relevant object fields and properties.
               - Use `evaluate` to test specific expressions or conditions.
            5. **Step through** using `debug_step` (stepOver / stepInto / stepOut):
               - Step over each line, checking variables after each step.
               - When a variable's value diverges from expectations, you've found the bug's neighborhood.
               - Step INTO the method where the divergence happens to pinpoint the exact cause.
            6. If the breakpoint location was wrong, use `breakpoint_remove` and `breakpoint_set` to move it.
            7. Once confirmed, use `debug_disconnect` to end the session.

            **Key principle:** Narrow down by bisection. Start broad, then zoom in on where \
            actual values diverge from expected values. Each step should halve the suspect area.
            """);
    }

    [McpServerPrompt(Name = "inspect_runtime_state", Title = "Inspect Runtime State")]
    [Description(
        "Workflow to inspect the current runtime state of a paused .NET process. "
        + "Gets threads, stack traces, variables, and object details "
        + "to build a complete picture of what the program is doing.")]
    public static IEnumerable<PromptMessage> InspectRuntimeState(
        [Description("If true, attach to a running process instead of launching a new one.")]
        bool attach = false,
        [Description("Process ID to attach to (required when attach=true).")]
        int? processId = null)
    {
        var startStep = attach && processId.HasValue
            ? $"1. **Attach to process** {processId.Value} using `debug_attach`."
            : attach
                ? "1. **Attach to the running process** using `debug_attach` with the target PID."
                : "1. **Launch the target process** using `debug_launch`.";

        yield return UserMessage($"""
            # Inspect Runtime State

            Follow these steps to get a complete picture of the program's runtime state:

            {startStep}
            2. **Pause execution** using `debug_pause` (skip if already paused at a breakpoint).
            3. **List all threads** using `threads_list` to see what the process is doing.
            4. For each thread of interest:
               - Use `stacktrace_get` with the thread ID to see the call stack.
               - Use `variables_get` with the desired frame to inspect locals and arguments.
            5. **Drill into objects:**
               - Use `members_get` to see fields and properties of complex objects.
               - Use `object_inspect` for a deep view of a specific object's structure.
               - Use `object_summarize` for an AI-friendly overview of large objects.
               - Use `collection_analyze` to understand collections (counts, types, value distributions).
            6. **Evaluate expressions** using `evaluate` to compute derived values or test conditions.
            7. **Take a snapshot** using `snapshot_create` to save the current state for later comparison.
            8. Use `debug_continue` to resume or `debug_disconnect` to end.

            **Key principle:** Build the picture layer by layer — threads → stacks → frames → variables → objects. \
            Don't try to inspect everything at once; focus on the threads and frames relevant to your investigation.
            """);
    }

    [McpServerPrompt(Name = "trace_data_flow", Title = "Trace Data Flow")]
    [Description(
        "Non-blocking workflow to trace how data flows through code using tracepoints. "
        + "Sets logging breakpoints that record values without stopping execution, "
        + "then analyzes the sequence of recorded values.")]
    public static IEnumerable<PromptMessage> TraceDataFlow(
        [Description("Source file path where tracing should start.")]
        string file,
        [Description("Line number for the first tracepoint.")]
        int line,
        [Description("Log message template with {expression} placeholders, "
            + "e.g. 'userId={userId}, count={items.Count}'.")]
        string? logMessage = null)
    {
        var logTemplate = string.IsNullOrWhiteSpace(logMessage)
            ? "`'value={variableName}'` (replace with actual expressions)"
            : $"`'{logMessage}'`";

        yield return UserMessage($"""
            # Trace Data Flow

            Use tracepoints (non-blocking breakpoints) to observe how data flows through the code \
            without stopping execution:

            1. **Launch the target process** using `debug_launch`.
            2. **Set tracepoints** at key locations using `tracepoint_set`:
               - First tracepoint at `{file}:{line}` with log message {logTemplate}.
               - Add more tracepoints at downstream locations to see how values change.
               - Tracepoints log values and continue execution — they do NOT pause the debugger.
            3. **Continue execution** with `debug_continue` and exercise the code path.
            4. **Wait for notifications** using `breakpoint_wait` — tracepoint hits arrive as notifications.
            5. **Analyze the trace:**
               - Look at the sequence of logged values to understand the data flow.
               - Identify where values change unexpectedly.
               - Check if methods are called in the expected order.
            6. **Refine:** Add or move tracepoints based on what you learn, then repeat.
            7. Use `debug_disconnect` to end the session.

            **Key principle:** Tracepoints are ideal when you need to observe behavior without altering timing \
            (e.g., race conditions, event ordering). Unlike breakpoints, they don't pause execution, \
            so they capture the natural flow of the program.
            """);
    }

    private static PromptMessage UserMessage(string text) => new()
    {
        Role = Role.User,
        Content = new TextContentBlock { Text = text }
    };
}
