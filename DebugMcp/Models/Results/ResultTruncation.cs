using System.Text.Json;

namespace DebugMcp.Models.Results;

/// <summary>
/// Bounds an unbounded collection to the FR-035 serialized-result size budget (default 256 KB).
/// Applied per-tool, inside the tool method, before the result record is constructed — the
/// SDK serializes the tool's return value after the fact, so there is no single choke point
/// that could trim generically; each tool knows which of its own fields is the unbounded one.
/// Only the 14 tools FR-035 names call this. Every other tool returns a naturally bounded
/// result and MUST NOT truncate.
/// </summary>
public static class ResultTruncation
{
    public const int DefaultBudgetBytes = 256 * 1024;

    private static readonly JsonSerializerOptions EstimationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Returns as many leading items as fit within <paramref name="budgetBytes"/> when serialized,
    /// plus a <see cref="TruncationInfo"/> describing what was omitted (null when nothing was).
    /// </summary>
    public static (IReadOnlyList<T> Items, TruncationInfo? Truncation) Bound<T>(
        IReadOnlyList<T> items,
        string reason,
        int budgetBytes = DefaultBudgetBytes)
    {
        if (items.Count == 0 || SerializedSize(items) <= budgetBytes)
        {
            return (items, null);
        }

        var lo = 0;
        var hi = items.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo + 1) / 2;
            var slice = items.Take(mid).ToList();
            if (SerializedSize(slice) <= budgetBytes)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        var bounded = items.Take(lo).ToList();
        var truncation = new TruncationInfo(
            Returned: lo,
            Available: items.Count,
            Reason: reason);
        return (bounded, truncation);
    }

    private static int SerializedSize<T>(IReadOnlyList<T> items) =>
        JsonSerializer.SerializeToUtf8Bytes(items, EstimationOptions).Length;
}
