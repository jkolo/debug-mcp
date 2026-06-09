using DebugMcp.Models.Timeline;

namespace DebugMcp.Services.Timeline;

public interface ITimelineStore
{
    void Record(TimelineEvent e);
    TimelineResponse GetAll();
    TimelineResponse GetFiltered(TimelineFilter filter);
    void Clear();
    int TotalRecorded { get; }
    int EventsDropped { get; }
}
