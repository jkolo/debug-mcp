using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugMcp.Models.Timeline;
using DebugMcp.Services.Timeline;
using ModelContextProtocol.Server;

namespace DebugMcp.Tools;

[McpServerToolType]
public sealed class TimelineQueryTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly ITimelineStore _timelineStore;

    public TimelineQueryTool(ITimelineStore timelineStore)
    {
        _timelineStore = timelineStore;
    }

    [McpServerTool(Name = "timeline_query", Title = "Query Timeline",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Query the unified debugging timeline. Returns debug events in chronological order across all event sources (session, breakpoints, exceptions, modules, threads, output). Supports filtering by event type, thread ID, and cursor-based pagination. Example response: {\"success\": true, \"events\": [{\"eventId\": 1, \"eventType\": \"SessionStarted\", \"timestamp\": \"...\", \"threadId\": null, \"payload\": {\"sessionType\": \"launch\", \"pid\": 12345}}], \"totalEvents\": 42, \"eventsDropped\": 0}")]
    public string TimelineQuery(
        [Description("JSON array of event type names to include, e.g. [\"breakpoint_hit\",\"exception_first_chance\"]. Null or empty returns all types. Supported types: session_started, breakpoint_hit, tracepoint_hit, exception_first_chance, exception_user_unhandled, module_loaded, thread_started, thread_exited, stdout_written, stderr_written, session_ended.")] string? eventTypes = null,
        [Description("Filter to events from this thread ID only. Null returns events from all threads.")] int? threadId = null,
        [Description("Return only events with EventId >= this value (cursor for pagination). Use the last EventId from a previous response to get newer events.")] int? fromEventId = null,
        [Description("Maximum number of events to return (default 200, max 1000).")] int maxEvents = 200)
    {
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
                    return JsonSerializer.Serialize(new { success = false, error = "Invalid eventTypes — expected JSON array of strings, e.g. [\"breakpoint_hit\"]" });
                }
            }

            var filter = new TimelineFilter(
                EventTypes: parsedTypes,
                ThreadId: threadId,
                FromEventId: fromEventId,
                MaxEvents: maxEvents);

            var response = _timelineStore.GetFiltered(filter);

            return JsonSerializer.Serialize(new
            {
                success = true,
                events = response.Events,
                totalEvents = response.TotalEvents,
                eventsDropped = response.EventsDropped
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
