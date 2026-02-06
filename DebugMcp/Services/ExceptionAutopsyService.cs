using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services;

/// <summary>
/// Collects bundled exception context when the debugger is paused at an exception.
/// </summary>
public sealed class ExceptionAutopsyService : IExceptionAutopsyService
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<ExceptionAutopsyService> _logger;

    public ExceptionAutopsyService(
        IDebugSessionManager sessionManager,
        IProcessDebugger processDebugger,
        ILogger<ExceptionAutopsyService> logger)
    {
        _sessionManager = sessionManager;
        _processDebugger = processDebugger;
        _logger = logger;
    }

    public async Task<ExceptionAutopsyResult> GetExceptionContextAsync(
        int maxFrames = 10,
        int includeVariablesForFrames = 1,
        int maxInnerExceptions = 5,
        CancellationToken cancellationToken = default)
    {
        if (_processDebugger.CurrentPauseReason != PauseReason.Exception)
        {
            throw new InvalidOperationException(
                "Debugger is not paused at an exception. Current pause reason: " +
                (_processDebugger.CurrentPauseReason?.ToString() ?? "none"));
        }

        var threadId = _processDebugger.ActiveThreadId
            ?? throw new InvalidOperationException("No active thread available.");

        _logger.LogDebug("Collecting exception autopsy for thread {ThreadId}", threadId);

        // 1. Get exception type, message, stack trace via $exception pseudo-variable
        var exceptionDetail = await GetExceptionDetailAsync(threadId, cancellationToken);

        // 2. Get stack frames
        var (stackFrames, totalFrames) = _sessionManager.GetStackFrames(threadId, 0, maxFrames);

        // 3. Build autopsy frames with optional variable collection
        var autopsyFrames = new List<AutopsyFrame>(stackFrames.Count);
        for (var i = 0; i < stackFrames.Count; i++)
        {
            var frame = stackFrames[i];
            FrameVariables? variables = null;
            IReadOnlyList<Variable>? arguments = null;

            if (i < includeVariablesForFrames)
            {
                variables = CollectFrameVariables(threadId, frame.Index);
                arguments = CollectFrameArguments(threadId, frame.Index);
            }

            autopsyFrames.Add(new AutopsyFrame(
                Index: frame.Index,
                Function: frame.Function,
                Module: frame.Module,
                IsExternal: frame.IsExternal,
                Location: frame.Location,
                Arguments: arguments,
                Variables: variables));
        }

        // 4. Walk inner exception chain
        var (innerExceptions, truncated) = await WalkInnerExceptionsAsync(
            threadId, maxInnerExceptions, cancellationToken);

        _logger.LogDebug(
            "Autopsy complete: {FrameCount} frames, {InnerCount} inner exceptions",
            autopsyFrames.Count, innerExceptions.Count);

        return new ExceptionAutopsyResult(
            ThreadId: threadId,
            Exception: exceptionDetail,
            InnerExceptions: innerExceptions,
            InnerExceptionsTruncated: truncated,
            Frames: autopsyFrames,
            TotalFrames: totalFrames);
    }

    private async Task<ExceptionDetail> GetExceptionDetailAsync(
        int threadId, CancellationToken cancellationToken)
    {
        var typeResult = await _sessionManager.EvaluateAsync(
            "$exception.GetType().FullName", threadId, 0, cancellationToken: cancellationToken);
        var messageResult = await _sessionManager.EvaluateAsync(
            "$exception.Message", threadId, 0, cancellationToken: cancellationToken);
        var stackTraceResult = await _sessionManager.EvaluateAsync(
            "$exception.StackTrace", threadId, 0, cancellationToken: cancellationToken);

        var exceptionType = typeResult.Success ? typeResult.Value ?? "Unknown" : "Unknown";
        var exceptionMessage = messageResult.Success ? messageResult.Value ?? "" : "";
        var stackTraceString = stackTraceResult.Success ? stackTraceResult.Value : null;

        // Determine IsFirstChance from the last exception event
        var isFirstChance = true; // default assumption

        // BUG-3 fix: If $exception eval failed, fall back to stored exception info from OnException2
        var lastExInfo = _processDebugger.LastExceptionInfo;
        if (lastExInfo.HasValue)
        {
            if (exceptionType == "Unknown")
            {
                exceptionType = lastExInfo.Value.Type;
                _logger.LogDebug("Using fallback exception type from LastExceptionInfo: {Type}", exceptionType);
            }
            if (string.IsNullOrEmpty(exceptionMessage) && !messageResult.Success)
            {
                exceptionMessage = lastExInfo.Value.Message;
                _logger.LogDebug("Using fallback exception message from LastExceptionInfo");
            }
            isFirstChance = lastExInfo.Value.IsFirstChance;
        }

        return new ExceptionDetail(
            Type: exceptionType,
            Message: exceptionMessage,
            IsFirstChance: isFirstChance,
            StackTraceString: stackTraceString);
    }

    private FrameVariables CollectFrameVariables(int threadId, int frameIndex)
    {
        try
        {
            var locals = _sessionManager.GetVariables(threadId, frameIndex, "locals");
            return new FrameVariables(Locals: locals);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect variables for frame {FrameIndex}", frameIndex);
            return new FrameVariables(
                Locals: [],
                Errors: [new VariableError("locals", ex.Message)]);
        }
    }

    private IReadOnlyList<Variable>? CollectFrameArguments(int threadId, int frameIndex)
    {
        try
        {
            return _sessionManager.GetVariables(threadId, frameIndex, "arguments");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect arguments for frame {FrameIndex}", frameIndex);
            return null;
        }
    }

    private async Task<(IReadOnlyList<InnerExceptionEntry> Entries, bool Truncated)> WalkInnerExceptionsAsync(
        int threadId, int maxDepth, CancellationToken cancellationToken)
    {
        if (maxDepth <= 0)
            return ([], false);

        var entries = new List<InnerExceptionEntry>();
        var prefix = "$exception.InnerException";

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            var path = depth == 1
                ? "$exception.InnerException"
                : prefix;

            var typeResult = await _sessionManager.EvaluateAsync(
                $"{path}.GetType().FullName", threadId, 0, cancellationToken: cancellationToken);

            if (!typeResult.Success || typeResult.Value == null)
                break;

            var messageResult = await _sessionManager.EvaluateAsync(
                $"{path}.Message", threadId, 0, cancellationToken: cancellationToken);

            entries.Add(new InnerExceptionEntry(
                Type: typeResult.Value,
                Message: messageResult.Success ? messageResult.Value ?? "" : "",
                Depth: depth));

            // Build path for next level
            prefix = $"{path}.InnerException";

            // Check if we reached max depth and there are more
            if (depth == maxDepth)
            {
                var nextTypeResult = await _sessionManager.EvaluateAsync(
                    $"{prefix}.GetType().FullName", threadId, 0, cancellationToken: cancellationToken);
                if (nextTypeResult.Success && nextTypeResult.Value != null)
                    return (entries, true);
            }
        }

        return (entries, false);
    }
}
