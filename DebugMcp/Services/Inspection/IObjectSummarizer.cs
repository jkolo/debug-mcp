using DebugMcp.Models.Inspection;

namespace DebugMcp.Services.Inspection;

/// <summary>
/// Summarizes objects in the debuggee, categorizing fields into valued, null, and interesting.
/// </summary>
public interface IObjectSummarizer
{
    /// <summary>
    /// Summarizes an object variable or expression, returning categorized fields
    /// with anomaly detection (nulls, empty strings, NaN, default dates, etc.).
    /// </summary>
    /// <param name="expression">Variable name or expression evaluating to an object.</param>
    /// <param name="maxPreviewItems">Max collection elements to preview inline for collection fields (1-50).</param>
    /// <param name="threadId">Thread context (null = active thread).</param>
    /// <param name="frameIndex">Stack frame context (0 = top).</param>
    /// <param name="timeoutMs">Evaluation timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Object summary with categorized fields.</returns>
    Task<ObjectSummary> SummarizeAsync(
        string expression,
        int maxPreviewItems = 5,
        int? threadId = null,
        int frameIndex = 0,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default);
}
