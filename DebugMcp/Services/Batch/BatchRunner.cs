using System.Collections.Concurrent;
using System.Threading.Channels;
using DebugMcp.Models;
using DebugMcp.Models.Batch;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Services.Breakpoints;
using DebugMcp.Services.SafeEval;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Batch;

public sealed class BatchRunner : IBatchRunner, IDisposable
{
    private readonly IBreakpointEventSource _eventSource;
    private readonly IBreakpointManager _breakpointManager;
    private readonly IDebugSessionManager _sessionManager;
    private readonly ISafeExpressionAnalyzer? _safeAnalyzer;
    private readonly ILogger<BatchRunner> _logger;
    private readonly IProcessDebugger? _processDebugger;

    private volatile bool _isRunning;

    public BatchRunner(
        IBreakpointEventSource eventSource,
        IBreakpointManager breakpointManager,
        IDebugSessionManager sessionManager,
        ISafeExpressionAnalyzer? safeAnalyzer,
        ILogger<BatchRunner> logger,
        IProcessDebugger? processDebugger = null)
    {
        _eventSource = eventSource;
        _breakpointManager = breakpointManager;
        _sessionManager = sessionManager;
        _safeAnalyzer = safeAnalyzer;
        _logger = logger;
        _processDebugger = processDebugger;
    }

    public bool IsRunning => _isRunning;

    public async Task<BatchResult> RunAsync(BatchRequest request, CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("batch_already_running");

        if (request.Experiments.Count is < 1 or > 20)
            throw new ArgumentException("validation_error: experiments must be 1–20");

        // Reject exception-type triggers (not yet supported via BreakpointResolved path)
        for (var i = 0; i < request.Experiments.Count; i++)
        {
            if (request.Experiments[i].Trigger is ExperimentTrigger.ExceptionType)
                throw new ArgumentException($"validation_error: experiment[{i}] exception_type triggers not yet supported in batch mode");
        }

        _isRunning = true;
        _logger.LogInformation("Batch starting: {Count} experiments, timeout={Timeout}s, eval_mode={Mode}",
            request.Experiments.Count, request.TimeoutSeconds, request.EvalMode);

        // State: per-experiment results (index → mutable list of hits)
        var experimentHits = new List<ExperimentHit>[request.Experiments.Count];
        var experimentStatus = new ExperimentStatus[request.Experiments.Count];
        var experimentErrors = new string?[request.Experiments.Count];
        for (var i = 0; i < experimentHits.Length; i++)
        {
            experimentHits[i] = [];
            experimentStatus[i] = ExperimentStatus.NotTriggered;
        }

        // Dispatch table: breakpoint ID → list of experiment indices
        var bpToExperiments = new Dictionary<string, List<int>>();
        // Registered batch breakpoints (to remove on cleanup)
        var batchBpIds = new List<string>();
        // Pre-existing breakpoints to restore
        var frozenBpIds = new List<(string Id, bool WasEnabled)>();
        // Completion signaling
        var completionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionReason = BatchCompletionReason.AllTriggered;
        var totalHits = 0;
        var allTriggeredCount = 0; // how many experiments have reached MaxHits

        // Blocking experiments defer their capture out of the ICorDebug callback: a property
        // getter / method call needs a func-eval, which can only run while the process is in a
        // stable pause (not mid-callback). The callback enqueues the hit and leaves the process
        // paused; the processing task below captures the values and then resumes (BUG-016).
        var blockingChannel = Channel.CreateUnbounded<(List<int> Indices, int ThreadId, BreakpointLocation Location, DateTimeOffset Timestamp)>();

        // Step 1: freeze pre-existing breakpoints
        var existing = await _breakpointManager.GetBreakpointsAsync(cancellationToken);
        var existingEx = await _breakpointManager.GetExceptionBreakpointsAsync(cancellationToken);
        foreach (var bp in existing.Where(b => b.Enabled))
        {
            frozenBpIds.Add((bp.Id, true));
            await _breakpointManager.SetBreakpointEnabledAsync(bp.Id, false, cancellationToken);
        }
        // Exception breakpoints are handled by their Enabled flag in BreakpointManager.OnExceptionHit

        // Step 2: register experiments as breakpoints/tracepoints
        for (var i = 0; i < request.Experiments.Count; i++)
        {
            var exp = request.Experiments[i];
            if (exp.Trigger is not ExperimentTrigger.SourceLocation loc)
                continue;

            try
            {
                Breakpoint bp;
                if (exp.Mode == ExperimentMode.NonBlocking)
                {
                    bp = await _breakpointManager.SetTracepointAsync(
                        loc.File, loc.Line, null, null, 0, 0, cancellationToken);
                }
                else
                {
                    bp = await _breakpointManager.SetBreakpointAsync(
                        loc.File, loc.Line, null, null, cancellationToken);
                }

                if (!bpToExperiments.TryGetValue(bp.Id, out var indices))
                {
                    indices = [];
                    bpToExperiments[bp.Id] = indices;
                    batchBpIds.Add(bp.Id);
                }
                indices.Add(i);
                _logger.LogDebug("Experiment[{Index}] registered as {BpId} at {File}:{Line}", i, bp.Id, loc.File, loc.Line);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register experiment[{Index}] at {File}:{Line}", i, loc.File, loc.Line);
                experimentStatus[i] = ExperimentStatus.Error;
                experimentErrors[i] = ex.Message;
                allTriggeredCount++; // error counts as "done" for completion check
            }
        }

        // Records a hit against an experiment and updates completion bookkeeping. Returns false
        // once the batch has completed (so callers can stop continuing the process).
        bool CommitHit(int idx, ExperimentHit hit)
        {
            var exp = request.Experiments[idx];
            var hits = experimentHits[idx];
            lock (hits)
            {
                if (hits.Count >= exp.MaxHits)
                    return !completionTcs.Task.IsCompleted;
                hits.Add(hit);
                if (hits.Count == 1)
                    experimentStatus[idx] = ExperimentStatus.Triggered;
                _logger.LogDebug("Experiment[{Index}] hit #{HitNum} on thread {Thread}", idx, hits.Count, hit.ThreadId);

                if (hits.Count >= exp.MaxHits)
                {
                    allTriggeredCount++;
                    if (allTriggeredCount >= request.Experiments.Count)
                    {
                        completionReason = BatchCompletionReason.AllTriggered;
                        completionTcs.TrySetResult();
                        return false;
                    }
                }
            }

            var total = Interlocked.Increment(ref totalHits);
            if (total >= request.MaxTotalHits)
            {
                completionReason = BatchCompletionReason.HitLimitReached;
                completionTcs.TrySetResult();
                return false;
            }
            return true;
        }

        // Captures an experiment's expressions while the process is in a stable pause (async,
        // so property getters / method calls can func-eval correctly).
        async Task<(Dictionary<string, string> Values, Dictionary<string, string> Errors)> CaptureAsync(
            Experiment exp, int threadId, CancellationToken captureCt)
        {
            var values = new Dictionary<string, string>();
            var evalErrors = new Dictionary<string, string>();
            if (exp.Capture is { Count: > 0 })
            {
                foreach (var expr in exp.Capture)
                {
                    if (request.EvalMode == EvalMode.Safe && _safeAnalyzer != null)
                    {
                        var analysis = _safeAnalyzer.Analyze(expr);
                        if (!analysis.IsAllowed)
                        {
                            evalErrors[expr] = $"blocked by safe eval: {analysis.Rejection?.Message ?? "unsafe expression"}";
                            continue;
                        }
                    }

                    try
                    {
                        var r = await _sessionManager.EvaluateAsync(expr, threadId, 0, timeoutMs: 2000, captureCt);
                        if (r.Success) values[expr] = r.Value ?? "null";
                        else evalErrors[expr] = r.Error?.Code ?? "error";
                    }
                    catch (OperationCanceledException)
                    {
                        evalErrors[expr] = "timeout";
                    }
                    catch (Exception ex)
                    {
                        evalErrors[expr] = ex.GetType().Name;
                    }
                }
            }
            return (values, evalErrors);
        }

        // Subscribe to BreakpointResolved AFTER setup (synchronous from here — before any await)
        void OnBreakpointResolved(object? sender, ResolvedBreakpointHitEventArgs e)
        {
            if (!bpToExperiments.TryGetValue(e.BreakpointId, out var indices))
                return;

            var blockingIndices = new List<int>();

            foreach (var idx in indices)
            {
                var exp = request.Experiments[idx];
                if (experimentHits[idx].Count >= exp.MaxHits)
                    continue; // already saturated

                // Blocking experiments are captured by the deferred processing task while paused.
                if (exp.Mode == ExperimentMode.Blocking)
                {
                    blockingIndices.Add(idx);
                    continue;
                }

                // Non-blocking (tracepoint): the process does not pause, so we must capture
                // synchronously here. Func-eval (property getters) can't run mid-callback and will
                // surface as a per-expression error — that is an inherent tracepoint limitation.
                if (!string.IsNullOrWhiteSpace(exp.Condition))
                {
                    try
                    {
                        var condTask = Task.Run(() =>
                            _sessionManager.EvaluateAsync(exp.Condition, e.ThreadId, 0, timeoutMs: 500));
                        if (condTask.Wait(600) && condTask.Result.Success && condTask.Result.Value == "False")
                            continue;
                    }
                    catch { /* treat condition errors as true */ }
                }

                var values = new Dictionary<string, string>();
                var evalErrors = new Dictionary<string, string>();
                if (exp.Capture is { Count: > 0 })
                {
                    foreach (var expr in exp.Capture)
                    {
                        if (request.EvalMode == EvalMode.Safe && _safeAnalyzer != null)
                        {
                            var analysis = _safeAnalyzer.Analyze(expr);
                            if (!analysis.IsAllowed)
                            {
                                evalErrors[expr] = $"blocked by safe eval: {analysis.Rejection?.Message ?? "unsafe expression"}";
                                continue;
                            }
                        }
                        try
                        {
                            var evalTask = Task.Run(() =>
                                _sessionManager.EvaluateAsync(expr, e.ThreadId, 0, timeoutMs: 500));
                            if (!evalTask.Wait(600)) evalErrors[expr] = "capture_requires_pause";
                            else if (evalTask.Result.Success) values[expr] = evalTask.Result.Value ?? "null";
                            else evalErrors[expr] = evalTask.Result.Error?.Code ?? "error";
                        }
                        catch (Exception ex)
                        {
                            evalErrors[expr] = ex.GetType().Name;
                        }
                    }
                }

                CommitHit(idx, new ExperimentHit(e.Timestamp, e.ThreadId, e.Location, values, evalErrors));
            }

            // Hand blocking hits to the processing task and leave the process paused (do not set
            // ShouldContinue) so the deferred capture can func-eval, then resume.
            if (blockingIndices.Count > 0)
                blockingChannel.Writer.TryWrite((blockingIndices, e.ThreadId, e.Location, e.Timestamp));
        }

        // Deferred capture loop for blocking experiments. Runs off the ICorDebug callback thread;
        // each iteration sees the process in a stable pause.
        async Task ProcessBlockingHitsAsync(CancellationToken loopCt)
        {
            await foreach (var (indices, threadId, location, timestamp) in blockingChannel.Reader.ReadAllAsync(loopCt))
            {
                foreach (var idx in indices)
                {
                    var exp = request.Experiments[idx];
                    if (experimentHits[idx].Count >= exp.MaxHits)
                        continue;

                    if (!string.IsNullOrWhiteSpace(exp.Condition))
                    {
                        try
                        {
                            var cr = await _sessionManager.EvaluateAsync(exp.Condition, threadId, 0, timeoutMs: 2000, loopCt);
                            if (cr.Success && cr.Value == "False")
                                continue;
                        }
                        catch { /* treat condition errors as true */ }
                    }

                    var (values, evalErrors) = await CaptureAsync(exp, threadId, loopCt);
                    CommitHit(idx, new ExperimentHit(timestamp, threadId, location, values, evalErrors));
                }

                // Resume for the next hit unless the batch just finished.
                if (!completionTcs.Task.IsCompleted)
                {
                    try { await _sessionManager.ContinueAsync(loopCt); }
                    catch (Exception ex) { _logger.LogDebug(ex, "Batch resume after deferred capture failed"); }
                }
            }
        }

        _eventSource.BreakpointResolved += OnBreakpointResolved;

        using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var processingTask = ProcessBlockingHitsAsync(processingCts.Token);

        // Subscribe to process exit
        void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
        {
            if (e.NewState == SessionState.Disconnected)
            {
                completionReason = BatchCompletionReason.ProcessExited;
                completionTcs.TrySetResult();
            }
        }

        if (_processDebugger != null)
            _processDebugger.StateChanged += OnStateChanged;

        try
        {
            // Step 3: wait for completion (timeout + cancellation)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            try
            {
                await completionTcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    completionReason = BatchCompletionReason.Cancelled;
                else
                    completionReason = BatchCompletionReason.Timeout;
            }
        }
        finally
        {
            // Step 4: cleanup (always runs — even on exception)
            _eventSource.BreakpointResolved -= OnBreakpointResolved;
            if (_processDebugger != null)
                _processDebugger.StateChanged -= OnStateChanged;

            // Drain the deferred-capture task: complete the channel so its loop ends, then await
            // it (bounded) so any in-flight capture finishes before results are built.
            blockingChannel.Writer.TryComplete();
            try { await processingTask.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Deferred capture task did not drain cleanly");
                processingCts.Cancel();
            }

            // Remove batch breakpoints
            foreach (var bpId in batchBpIds)
            {
                try { await _breakpointManager.RemoveBreakpointAsync(bpId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to remove batch bp {Id}", bpId); }
            }

            // Restore frozen breakpoints
            foreach (var (id, wasEnabled) in frozenBpIds)
            {
                try { await _breakpointManager.SetBreakpointEnabledAsync(id, wasEnabled); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to restore bp {Id}", id); }
            }

            // Deferred blocking capture leaves the process paused at the final hit; resume it so
            // the post-batch state matches the original (running) semantics.
            if (completionReason != BatchCompletionReason.ProcessExited &&
                _sessionManager.CurrentSession?.State == SessionState.Paused)
            {
                try { await _sessionManager.ContinueAsync(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to resume process after batch"); }
            }

            _isRunning = false;
        }

        // Build final result
        var results = new ExperimentResult[request.Experiments.Count];
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = new ExperimentResult(
                i,
                experimentStatus[i],
                experimentHits[i].Count,
                experimentHits[i].AsReadOnly(),
                experimentErrors[i]);
        }

        var triggered = results.Count(r => r.Status == ExperimentStatus.Triggered);
        var notTriggered = results.Count(r => r.Status == ExperimentStatus.NotTriggered);
        var errors = results.Count(r => r.Status == ExperimentStatus.Error);

        _logger.LogInformation("Batch complete: reason={Reason}, triggered={T}, notTriggered={NT}, errors={E}",
            completionReason, triggered, notTriggered, errors);

        return new BatchResult(
            completionReason,
            request.Experiments.Count,
            triggered,
            notTriggered,
            errors,
            results);
    }

    public void Dispose() { }
}
