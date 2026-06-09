using DebugMcp.Models;
using DebugMcp.Models.Timeline;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Timeline;

public sealed class TimelineStore : ITimelineStore, IDisposable
{
    private const int MaxCapacity = 10_000;

    private readonly ILogger<TimelineStore> _logger;
    private readonly IBreakpointEventSource? _eventSource;
    private readonly IProcessDebugger? _processDebugger;
    private readonly IOutputEventSource? _ioManager;
    private readonly IDebugSessionManager? _sessionManager;

    private readonly System.Collections.Concurrent.ConcurrentQueue<TimelineEvent> _events = new();
    private int _nextEventId;
    private int _eventsDropped;
    private int _totalRecorded;
    private SessionState _lastState = SessionState.Disconnected;
    private readonly Lock _stateLock = new();

    public TimelineStore(
        IBreakpointEventSource? eventSource,
        IProcessDebugger? processDebugger,
        IOutputEventSource? ioManager,
        ILogger<TimelineStore> logger,
        IDebugSessionManager? sessionManager = null)
    {
        _eventSource = eventSource;
        _processDebugger = processDebugger;
        _ioManager = ioManager;
        _logger = logger;
        _sessionManager = sessionManager;

        if (_processDebugger != null)
        {
            _processDebugger.StateChanged += OnStateChanged;
            _processDebugger.ModuleLoaded += OnModuleLoaded;
            _processDebugger.ExceptionHit += OnExceptionHit;
            _processDebugger.ThreadCreated += OnThreadCreated;
            _processDebugger.ThreadExited += OnThreadExited;
        }

        if (_eventSource != null)
            _eventSource.BreakpointResolved += OnBreakpointResolved;

        if (_ioManager != null)
            _ioManager.OutputReceived += OnOutputReceived;
    }

    public int TotalRecorded => _totalRecorded;
    public int EventsDropped => _eventsDropped;

    public void Record(TimelineEvent e) => Append(e);

    public TimelineResponse GetAll()
    {
        var snapshot = _events.ToArray();
        return new TimelineResponse(snapshot, _totalRecorded, _eventsDropped, null);
    }

    public TimelineResponse GetFiltered(TimelineFilter filter)
    {
        var snapshot = _events.ToArray();

        IEnumerable<TimelineEvent> query = snapshot;

        if (filter.EventTypes is { Length: > 0 })
        {
            var types = filter.EventTypes
                .Select(t => Enum.TryParse<TimelineEventType>(SnakeToPascal(t), ignoreCase: true, out var parsed) ? (TimelineEventType?)parsed : null)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToHashSet();
            query = query.Where(e => types.Contains(e.EventType));
        }

        if (filter.ThreadId.HasValue)
            query = query.Where(e => e.ThreadId == filter.ThreadId.Value);

        if (filter.FromEventId.HasValue)
            query = query.Where(e => e.EventId >= filter.FromEventId.Value);

        var maxEvents = Math.Min(filter.MaxEvents, 1000);
        var events = query.Take(maxEvents).ToArray();

        return new TimelineResponse(events, _totalRecorded, _eventsDropped, null);
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _eventsDropped, 0);
        Interlocked.Exchange(ref _totalRecorded, 0);
        Interlocked.Exchange(ref _nextEventId, 0);
    }

    public void Dispose()
    {
        if (_processDebugger != null)
        {
            _processDebugger.StateChanged -= OnStateChanged;
            _processDebugger.ModuleLoaded -= OnModuleLoaded;
            _processDebugger.ExceptionHit -= OnExceptionHit;
            _processDebugger.ThreadCreated -= OnThreadCreated;
            _processDebugger.ThreadExited -= OnThreadExited;
        }

        if (_eventSource != null)
            _eventSource.BreakpointResolved -= OnBreakpointResolved;

        if (_ioManager != null)
            _ioManager.OutputReceived -= OnOutputReceived;
    }

    private void Append(TimelineEvent e)
    {
        if (_events.Count >= MaxCapacity)
        {
            _events.TryDequeue(out _);
            var dropped = Interlocked.Increment(ref _eventsDropped);
            _logger.LogWarning("Timeline cap reached — evicting oldest event (total dropped: {Dropped})", dropped);
        }
        _events.Enqueue(e);
        Interlocked.Increment(ref _totalRecorded);
        _logger.LogDebug("Timeline: recorded {EventType} (id={EventId})", e.EventType, e.EventId);
    }

    private TimelineEvent CreateEvent(TimelineEventType type, int? threadId, TimelineEventPayload payload)
    {
        var id = Interlocked.Increment(ref _nextEventId);
        return new TimelineEvent(id, DateTimeOffset.UtcNow, type, threadId, payload);
    }

    private static string SnakeToPascal(string s) =>
        string.Concat(s.Split('_').Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        SessionState previous;
        lock (_stateLock)
        {
            previous = _lastState;
            _lastState = e.NewState;
        }

        if (e.NewState == SessionState.Running && previous != SessionState.Running)
        {
            // Clear any leftover events from the previous session (including SessionEnded if present)
            Clear();

            var session = _sessionManager?.CurrentSession;
            var sessionType = session?.LaunchMode.ToString().ToLowerInvariant() ?? "launch";
            var pid = session?.ProcessId ?? 0;
            _logger.LogInformation("Timeline: session started (previous={Previous}, pid={Pid})", previous, pid);
            Record(CreateEvent(TimelineEventType.SessionStarted, null, new SessionStartedPayload(sessionType, pid)));
        }

        if (e.NewState == SessionState.Disconnected)
        {
            _logger.LogInformation("Timeline: session ended");
            Record(CreateEvent(TimelineEventType.SessionEnded, null, new SessionEndedPayload("disconnected")));
        }
    }

    private void OnModuleLoaded(object? sender, ModuleLoadedEventArgs e)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(e.ModulePath) ?? e.ModulePath;
        Record(CreateEvent(TimelineEventType.ModuleLoaded, null,
            new ModuleLoadedPayload(name, e.ModulePath, false)));
    }

    private void OnExceptionHit(object? sender, ExceptionHitEventArgs e)
    {
        var type = e.IsFirstChance ? TimelineEventType.ExceptionFirstChance : TimelineEventType.ExceptionUserUnhandled;
        Record(CreateEvent(type, e.ThreadId,
            new ExceptionPayload(e.ExceptionType, e.ExceptionMessage, !e.IsFirstChance)));
    }

    private void OnThreadCreated(object? sender, ThreadCreatedEventArgs e)
    {
        Record(CreateEvent(TimelineEventType.ThreadStarted, e.ThreadId, new ThreadStartedPayload(null)));
    }

    private void OnThreadExited(object? sender, ThreadExitedEventArgs e)
    {
        Record(CreateEvent(TimelineEventType.ThreadExited, e.ThreadId, new ThreadExitedPayload(null)));
    }

    private void OnBreakpointResolved(object? sender, ResolvedBreakpointHitEventArgs e)
    {
        var isTracepoint = e.BreakpointId.StartsWith("tp-", StringComparison.OrdinalIgnoreCase);
        var file = e.Location?.File ?? string.Empty;
        var line = e.Location?.Line ?? 0;

        if (isTracepoint)
            Record(CreateEvent(TimelineEventType.TracepointHit, e.ThreadId,
                new TracepointHitPayload(e.BreakpointId, file, line)));
        else
            Record(CreateEvent(TimelineEventType.BreakpointHit, e.ThreadId,
                new BreakpointHitPayload(e.BreakpointId, file, line)));
    }

    private void OnOutputReceived(string content, string stream, bool truncated)
    {
        var type = stream == "stdout" ? TimelineEventType.StdoutWritten : TimelineEventType.StderrWritten;
        Record(CreateEvent(type, null, new OutputPayload(content, truncated, stream)));
    }
}
