using DebugMcp.Models.Inspection;

namespace DebugMcp.Services.Inspection;

/// <summary>
/// Analyzes collections in the debuggee, returning structured summaries.
/// </summary>
public interface ICollectionAnalyzer
{
    /// <summary>
    /// Analyzes a collection variable or expression and returns a summary
    /// with count, element types, statistics, and element previews.
    /// </summary>
    /// <param name="expression">Variable name or expression evaluating to a collection.</param>
    /// <param name="maxPreviewItems">Number of first/last elements to include (1-50).</param>
    /// <param name="threadId">Thread context (null = active thread).</param>
    /// <param name="frameIndex">Stack frame context (0 = top).</param>
    /// <param name="timeoutMs">Evaluation timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection summary with count, types, statistics, and previews.</returns>
    Task<CollectionSummary> AnalyzeAsync(
        string expression,
        int maxPreviewItems = 5,
        int? threadId = null,
        int frameIndex = 0,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default);
}
