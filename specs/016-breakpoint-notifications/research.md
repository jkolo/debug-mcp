# Research: MCP Breakpoint Notifications

## 1. MCP Custom Notifications

**Decision**: Use `SendNotificationAsync` with custom notification method `debugger/breakpointHit`

**Rationale**:
- MCP SDK provides `McpServer.SendNotificationAsync<TParams>(string method, TParams params)` for sending arbitrary notifications
- Already used in project for logging via `NotificationMethods.LoggingMessageNotification` ("notifications/message")
- Custom method names are allowed by MCP spec (vendor-prefixed recommended but not required)
- Fire-and-forget pattern already established in `McpLogger.cs`

**Alternatives considered**:
1. Use `notifications/message` (logging) - Rejected: would mix debugging events with logs
2. Use `notifications/progress` - Rejected: semantically wrong, progress is for long-running operations
3. Polling via `breakpoint_wait` only - Rejected: doesn't meet push-notification requirement

**Implementation pattern** (from existing McpLogger.cs):
```csharp
_ = server.SendNotificationAsync("debugger/breakpointHit", notificationParams);
```

## 2. ICorDebug Breakpoint Callbacks

**Decision**: Extend existing `CorManagedCallback` to check breakpoint type and either pause (blocking) or continue (tracepoint)

**Rationale**:
- `ICorDebugManagedCallback::Breakpoint` is called for all breakpoints regardless of type
- Current implementation always pauses execution after callback
- For tracepoints: evaluate expressions, send notification, then call `Continue()` immediately
- No new ICorDebug APIs needed - just behavioral change in callback handler

**Key finding**:
- Expression evaluation requires process to be stopped (already happens in callback)
- After evaluation completes, call `ICorDebugController::Continue(false)` to resume
- Must handle evaluation timeout to prevent blocking debuggee indefinitely

**Implementation approach**:
```csharp
// In breakpoint callback
if (breakpoint.Type == BreakpointType.Tracepoint)
{
    // Evaluate expressions (sync - process is stopped)
    var evaluatedMessage = await EvaluateLogMessage(breakpoint.LogMessage, frame);
    // Send notification (async fire-and-forget)
    _ = _notifier.SendBreakpointHitAsync(breakpoint, evaluatedMessage);
    // Continue immediately (don't queue for WaitForBreakpoint)
    controller.Continue(false);
}
else
{
    // Existing behavior: queue hit, wait for continue
    _breakpointHitQueue.Enqueue(hit);
}
```

## 3. Expression Evaluation in Tracepoints

**Decision**: Reuse existing `EvaluateTool` infrastructure with template parsing for `{expression}` syntax

**Rationale**:
- `EvaluateTool` already handles ICorDebug expression evaluation
- Template syntax `{expression}` is familiar (C# string interpolation)
- Need to parse template, extract expressions, evaluate each, substitute results

**Template parsing approach**:
```csharp
// Input: "Counter is {i}, sum is {sum}"
// Parse to: ["Counter is ", "{i}", ", sum is ", "{sum}"]
// Evaluate: i=42, sum=100
// Output: "Counter is 42, sum is 100"
```

**Error handling**:
- Expression evaluation timeout: Include `<error: timeout>` in result
- Evaluation exception: Include `<error: ExceptionType>` in result
- Don't fail entire notification for single expression error

**Implementation**:
```csharp
public async Task<string> EvaluateLogMessageAsync(string template, ICorDebugFrame frame)
{
    var regex = new Regex(@"\{([^}]+)\}");
    var result = template;

    foreach (Match match in regex.Matches(template))
    {
        var expression = match.Groups[1].Value;
        try
        {
            var value = await _evaluator.EvaluateAsync(expression, frame, timeout: 1000);
            result = result.Replace(match.Value, value.ToString());
        }
        catch (Exception ex)
        {
            result = result.Replace(match.Value, $"<error: {ex.GetType().Name}>");
        }
    }

    return result;
}
```

## 4. Tracepoint Hit Counting and Filtering

**Decision**: Store hit count per tracepoint, filter notifications based on `hitCountMultiple` and `maxNotifications`

**Rationale**:
- Hit counting already exists for regular breakpoints (`Breakpoint.HitCount`)
- Filtering prevents notification flooding from hot code paths
- `hitCountMultiple=N`: notify every Nth hit (1, N, 2N, 3N...)
- `maxNotifications=M`: auto-disable after M notifications sent

**Implementation**:
```csharp
public bool ShouldNotify(Tracepoint tp)
{
    tp.HitCount++;

    if (tp.MaxNotifications > 0 && tp.NotificationsSent >= tp.MaxNotifications)
    {
        tp.Enabled = false;  // Auto-disable
        return false;
    }

    if (tp.HitCountMultiple > 0 && tp.HitCount % tp.HitCountMultiple != 0)
    {
        return false;  // Not Nth hit
    }

    tp.NotificationsSent++;
    return true;
}
```

## 5. Notification Payload Structure

**Decision**: JSON payload with breakpoint info, location, thread, timestamp, and optional log message

**Rationale**:
- Consistent with existing tool responses
- Includes all information LLM agent needs to understand the event
- `logMessage` only present for tracepoints with templates

**Payload schema**:
```json
{
    "breakpointId": "bp-1",
    "type": "tracepoint",
    "location": {
        "file": "/path/to/file.cs",
        "line": 42,
        "column": 5,
        "functionName": "MyMethod"
    },
    "threadId": 12345,
    "timestamp": "2026-02-05T10:30:00.123Z",
    "hitCount": 5,
    "logMessage": "Counter is 42, sum is 100"
}
```

## 6. Thread Safety and Performance

**Decision**: Use `Channel<T>` for notification queue, process asynchronously

**Rationale**:
- Breakpoint callbacks happen on debugger thread
- Must not block callback with notification I/O
- `Channel` provides thread-safe producer-consumer pattern
- Already used pattern in .NET for async queues

**Implementation**:
```csharp
private readonly Channel<BreakpointNotification> _notificationChannel =
    Channel.CreateUnbounded<BreakpointNotification>();

// In callback (producer)
_notificationChannel.Writer.TryWrite(notification);

// Background task (consumer)
await foreach (var notification in _notificationChannel.Reader.ReadAllAsync())
{
    await SendNotificationAsync(notification);
}
```

## 7. Backward Compatibility

**Decision**: Notifications are additive; `breakpoint_wait` continues to work unchanged

**Rationale**:
- Existing agents may rely on `breakpoint_wait` polling
- Adding notifications doesn't break existing behavior
- Both mechanisms can coexist - agent chooses preferred approach

**Key points**:
- Regular breakpoints: queue hit for `breakpoint_wait` AND send notification
- Tracepoints: send notification only (no `breakpoint_wait` since not blocking)
- `breakpoint_list` output extended with `type` field (backward compatible addition)

## Summary

| Research Area | Decision | Key Technology |
|--------------|----------|----------------|
| Custom notifications | `debugger/breakpointHit` method | `SendNotificationAsync` |
| Breakpoint callbacks | Check type, continue for tracepoints | `ICorDebugManagedCallback` |
| Expression evaluation | Parse `{expr}` template, reuse evaluator | Regex + existing evaluator |
| Hit count filtering | Per-tracepoint counters | `HitCountMultiple`, `MaxNotifications` |
| Notification payload | JSON with location, thread, timestamp, logMessage | Custom record type |
| Thread safety | Unbounded Channel for queue | `Channel<T>` |
| Backward compatibility | Additive changes only | Both mechanisms coexist |
