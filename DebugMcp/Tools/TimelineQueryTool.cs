using System.ComponentModel;
using System.Text.Json;
using DebugMcp.Models;
using DebugMcp.Models.Results;
using DebugMcp.Models.Timeline;
using DebugMcp.Services.Timeline;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class TimelineQueryTool
{
    private readonly ITimelineStore _timelineStore;

    public TimelineQueryTool(ITimelineStore timelineStore)
    {
        _timelineStore = timelineStore;
    }

    [McpServerTool(Name = "timeline_query", Title = "Query Timeline",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Query the unified debugging timeline. Returns debug events in chronological order across all event sources (session, breakpoints, exceptions, modules, threads, output). Supports filtering by event type, thread ID, and cursor-based pagination. Example response: {\"success\": true, \"events\": [{\"eventId\": 1, \"eventType\": \"SessionStarted\", \"timestamp\": \"...\", \"threadId\": null, \"payload\": {\"sessionType\": \"launch\", \"pid\": 12345}}], \"totalEvents\": 42, \"eventsDropped\": 0}")]
    public Task<TimelineQueryResult> TimelineQueryAsync(
        [Description("JSON array of event type names to include, e.g. [\"breakpoint_hit\",\"exception_first_chance\"]. Null or empty returns all types. Supported types: session_started, breakpoint_hit, tracepoint_hit, exception_first_chance, exception_user_unhandled, module_loaded, thread_started, thread_exited, stdout_written, stderr_written, session_ended.")] string? eventTypes = null,
        [Description("Filter to events from this thread ID only. Null returns events from all threads.")] int? threadId = null,
        [Description("Return only events with EventId >= this value (cursor for pagination). Use the last EventId from a previous response to get newer events.")] int? fromEventId = null,
        [Description("Maximum number of events to return (default 200, max 1000).")] int maxEvents = 200,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string[]? parsedTypes = null;
            if (!string.IsNullOrWhiteSpace(eventTypes))
            {
                try
                {
                    parsedTypes = JsonSerializer.Deserialize<string[]>(eventTypes);
                }
                catch
                {
                    return Task.FromResult(new TimelineQueryResult(
                        Success: false,
                        Error: new ToolError(
                            ErrorCodes.InvalidParameter,
                            "Invalid eventTypes — expected JSON array of strings, e.g. [\"breakpoint_hit\"]")));
                }
            }

            var filter = new TimelineFilter(
                EventTypes: parsedTypes,
                ThreadId: threadId,
                FromEventId: fromEventId,
                MaxEvents: maxEvents);

            var response = _timelineStore.GetFiltered(filter);

            var events = response.Events.Select(e => new TimelineQueryEventWire(
                EventId: e.EventId,
                Timestamp: e.Timestamp,
                EventType: (int)e.EventType,
                ThreadId: e.ThreadId,
                Payload: new TimelineQueryPayloadWire())).ToList();

            return Task.FromResult(new TimelineQueryResult(
                Success: true,
                Events: events,
                TotalEvents: response.TotalEvents,
                EventsDropped: response.EventsDropped));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TimelineQueryResult(
                Success: false,
                Error: new ToolError(ErrorCodes.SearchFailed, ex.Message)));
        }
    }
}
