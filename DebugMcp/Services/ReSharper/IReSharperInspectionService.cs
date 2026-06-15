using DebugMcp.Models.ReSharper;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Orchestrates a ReSharper inspection: validate input → ensure engine (lazy install) →
/// run inspectcode → parse → sort, count, cap. Throws <see cref="ReSharperException"/>
/// subtypes (carrying error codes) and <see cref="ArgumentException"/>/<see cref="OperationCanceledException"/>
/// which the tool layer maps to the standard error envelope.
/// </summary>
public interface IReSharperInspectionService
{
    /// <param name="target">Absolute path to a .sln or .csproj.</param>
    /// <param name="severity">Native minimum severity (error/warning/suggestion/hint) or null.</param>
    /// <param name="project">Project name to scope a solution inspection to, or null.</param>
    /// <param name="noBuild">Skip the engine's pre-analysis build.</param>
    /// <param name="inspectionTimeoutSeconds">Per-run inspection budget (separate from acquisition).</param>
    /// <param name="maxResults">Upper bound on returned findings.</param>
    Task<InspectionResult> InspectAsync(
        string target,
        string? severity,
        string? project,
        bool noBuild,
        int inspectionTimeoutSeconds,
        int maxResults,
        CancellationToken cancellationToken);
}
