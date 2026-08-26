namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>timeline_query</c>. Field names preserved from the pre-US3 hand-rolled JSON
/// (FR-021). <c>timeline_query</c> is one of the 14 FR-035 collection-returning tools, hence the
/// (currently unpopulated — wired in T054) <see cref="Truncation"/> field.
/// </summary>
/// <remarks>
/// Legacy note (characterized, not fixed — out of this migration's scope, and the underlying
/// <c>TimelineEventType</c>/<c>TimelineEventPayload</c> types under
/// <c>DebugMcp/Models/Timeline/</c> are not in this tool's file set): the pre-migration tool
/// serialized <c>response.Events</c> (a <c>List&lt;TimelineEvent&gt;</c>) directly with no enum
/// converter and no polymorphic payload configuration. Empirically verified (a standalone
/// serialization repro under the same options) that this means <c>eventType</c> was already
/// emitted as a raw <b>integer</b> enum ordinal — not the string the tool's own
/// <c>[Description]</c> example claims — and <c>payload</c> was already always <c>{}</c> (System.
/// Text.Json serializes an abstract-typed property using its declared, property-less abstract
/// type, not the runtime subtype, absent <c>[JsonPolymorphic]</c>). <see cref="TimelineQueryEventWire"/>
/// reproduces this exactly: <c>EventType</c> is typed <c>int</c> (cast from the enum), and
/// <see cref="TimelineQueryPayloadWire"/> is an intentionally empty record. <c>sessionId</c>
/// (present on the underlying <c>TimelineResponse</c> service model) was never emitted by the
/// legacy tool and is likewise omitted here.
/// </remarks>
public sealed record TimelineQueryResult(
    bool Success,
    IReadOnlyList<TimelineQueryEventWire>? Events = null,
    int? TotalEvents = null,
    int? EventsDropped = null,
    TruncationInfo? Truncation = null,
    ToolError? Error = null);

/// <summary>
/// One timeline event, as emitted on the wire (see remarks on <see cref="TimelineQueryResult"/>).
/// <see cref="ThreadId"/> is nullable-with-default (reproduces legacy conditional omission — a
/// requiredness pitfall applies recursively to nested wire types too, not just the top-level
/// result); it is placed last, after the always-present <see cref="Payload"/>, purely so C#
/// accepts the default (field order is not part of the wire contract).
/// </summary>
public sealed record TimelineQueryEventWire(
    int EventId,
    DateTimeOffset Timestamp,
    int EventType,
    TimelineQueryPayloadWire Payload,
    int? ThreadId = null);

/// <summary>
/// Intentionally empty — reproduces the legacy tool's always-<c>{}</c> payload (see remarks on
/// <see cref="TimelineQueryResult"/>).
/// </summary>
public sealed record TimelineQueryPayloadWire;
