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

        // Subscribe to BreakpointResolved AFTER setup (synchronous from here — before any await)
        void OnBreakpointResolved(object? sender, ResolvedBreakpointHitEventArgs e)
        {
            if (!bpToExperiments.TryGetValue(e.BreakpointId, out var indices))
                return;

            var isAnyBlocking = false;

            foreach (var idx in indices)
            {
                var exp = request.Experiments[idx];
                var hits = experimentHits[idx];

                if (hits.Count >= exp.MaxHits)
                    continue; // this experiment already saturated

                // Per-experiment condition check (synchronous, process is stopped)
                if (!string.IsNullOrWhiteSpace(exp.Condition))
                {
                    try
                    {
                        var condTask = Task.Run(() =>
                            _sessionManager.EvaluateAsync(exp.Condition, e.ThreadId, 0, timeoutMs: 500));
                        var completed = condTask.Wait(600);
                        if (completed && condTask.Result.Success && condTask.Result.Value == "False")
                            continue; // condition false
                    }
                    catch
                    {
                        // If condition evaluation fails, treat as true (don't skip)
                    }
                }

                // Capture variables synchronously (same pattern as BreakpointManager.CreateNotification)
                var values = new Dictionary<string, string>();
                var evalErrors = new Dictionary<string, string>();

                if (exp.Capture is { Count: > 0 })
                {
                    foreach (var expr in exp.Capture)
                    {
                        // eval_mode safety gate
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
                            var completed = evalTask.Wait(600);
                            if (!completed)
                            {
                                evalErrors[expr] = "timeout";
                            }
                            else if (evalTask.Result.Success)
                            {
                                values[expr] = evalTask.Result.Value ?? "null";
                            }
                            else
                            {
                                evalErrors[expr] = evalTask.Result.Error?.Code ?? "error";
                            }
                        }
                        catch (Exception ex)
                        {
                            evalErrors[expr] = ex.GetType().Name;
                        }
                    }
                }

                var hit = new ExperimentHit(
                    e.Timestamp,
                    e.ThreadId,
                    e.Location,
                    values,
                    evalErrors);

                // Track blocking BEFORE any completion-triggered early return
                if (exp.Mode == ExperimentMode.Blocking)
                    isAnyBlocking = true;

                lock (hits)
                {
                    if (hits.Count >= exp.MaxHits)
                        return; // double-check under lock
                    hits.Add(hit);
                    if (hits.Count == 1)
                    {
                        experimentStatus[idx] = ExperimentStatus.Triggered;
                    }
                    _logger.LogDebug("Experiment[{Index}] hit #{HitNum} on thread {Thread}",
                        idx, hits.Count, e.ThreadId);

                    if (hits.Count >= exp.MaxHits)
                    {
                        allTriggeredCount++;
                        if (allTriggeredCount >= request.Experiments.Count)
                        {
                            completionReason = BatchCompletionReason.AllTriggered;
                            if (isAnyBlocking)
                                e.ShouldContinue = true;
                            completionTcs.TrySetResult();
                            return;
                        }
                    }
                }

                // Total hit cap
                var total = Interlocked.Increment(ref totalHits);
                if (total >= request.MaxTotalHits)
                {
                    completionReason = BatchCompletionReason.HitLimitReached;
                    if (isAnyBlocking)
                        e.ShouldContinue = true;
                    completionTcs.TrySetResult();
                    return;
                }
            }

            // If any experiment for this hit is blocking, set ShouldContinue
            if (isAnyBlocking)
                e.ShouldContinue = true;
        }

        _eventSource.BreakpointResolved += OnBreakpointResolved;

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
