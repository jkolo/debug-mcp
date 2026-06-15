namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Inputs for a single <c>jb inspectcode</c> run. Built by the inspection service and handed
/// to the runner seam (which is faked in unit tests).
/// </summary>
/// <param name="Target">Absolute path to the .sln or .csproj to inspect.</param>
/// <param name="Severity">Native minimum severity (ERROR/WARNING/SUGGESTION/HINT) or null for engine default.</param>
/// <param name="Project">Project name to scope a solution inspection to, or null for all.</param>
/// <param name="NoBuild">When true, pass --no-build to skip the pre-analysis build.</param>
public sealed record InspectionRunRequest(
    string Target,
    string? Severity,
    string? Project,
    bool NoBuild);

/// <summary>
/// Result of ensuring the engine is installed. Transient — not serialized.
/// </summary>
/// <param name="JbPath">Absolute path to the installed <c>jb</c>/<c>jb.exe</c> shim.</param>
/// <param name="Version">Installed/pinned engine version.</param>
/// <param name="Acquired">True if this call performed an install (vs a cache hit) — for logging.</param>
public sealed record EngineInstallState(
    string JbPath,
    string Version,
    bool Acquired);
