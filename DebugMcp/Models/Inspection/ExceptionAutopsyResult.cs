namespace DebugMcp.Models.Inspection;

/// <summary>
/// Bundled exception context returned by the autopsy tool.
/// </summary>
/// <param name="ThreadId">Thread where exception occurred.</param>
/// <param name="Exception">Primary exception details.</param>
/// <param name="InnerExceptions">Chain of inner exceptions (depth-capped).</param>
/// <param name="InnerExceptionsTruncated">True if chain was capped before reaching null.</param>
/// <param name="Frames">Stack frames from the exception thread.</param>
/// <param name="TotalFrames">Total frames available (may exceed returned count).</param>
/// <param name="ThrowingFrameIndex">Index of the frame that threw the exception (usually 0).</param>
/// <param name="Enrichment">Deterministic suspicion ranking over <see cref="Frames"/> (FR-022–FR-026). Strictly additive.</param>
public sealed record ExceptionAutopsyResult(
    int ThreadId,
    ExceptionDetail Exception,
    IReadOnlyList<InnerExceptionEntry> InnerExceptions,
    bool InnerExceptionsTruncated,
    IReadOnlyList<AutopsyFrame> Frames,
    int TotalFrames,
    int ThrowingFrameIndex = 0,
    EnrichmentOutcome? Enrichment = null);
