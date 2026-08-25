namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Configuration for the ReSharper inspection integration. Resolved with priority
/// CLI argument &gt; environment variable &gt; default (mirrors <c>SymbolServerOptions</c>).
/// </summary>
public sealed record ReSharperOptions
{
    /// <summary>Pinned engine version acquired on first use.</summary>
    public const string DefaultVersion = "2026.2.1";

    /// <summary>NuGet package id of the ReSharper command-line engine (dotnet tool).</summary>
    public const string PackageId = "JetBrains.ReSharper.GlobalTools";

    public static readonly string DefaultCacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".debug-mcp", "resharper");

    /// <summary>Master enable/disable for the ReSharper tools (opt-out, default on).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Root cache directory; the engine for a version lives at <c>CacheDirectory/&lt;Version&gt;</c>.</summary>
    public string CacheDirectory { get; init; } = DefaultCacheDirectory;

    /// <summary>Pinned engine version.</summary>
    public string Version { get; init; } = DefaultVersion;

    /// <summary>Timeout for the one-time engine download/install, in seconds.</summary>
    public int AcquisitionTimeoutSeconds { get; init; } = 600;

    /// <summary>Default timeout for a single inspection run, in seconds (per-call overridable).</summary>
    public int InspectionTimeoutSeconds { get; init; } = 300;

    /// <summary>Default maximum number of findings returned (capped per call).</summary>
    public int MaxResults { get; init; } = 500;

    /// <summary>Absolute tool-path for the pinned engine: <c>CacheDirectory/Version</c>.</summary>
    public string EngineToolPath => Path.Combine(CacheDirectory, Version);

    /// <summary>
    /// Creates options from CLI arguments and environment variables.
    /// Priority: CLI argument &gt; environment variable &gt; default.
    /// </summary>
    public static ReSharperOptions Create(
        bool noResharper = false,
        string? resharperCache = null,
        string? resharperVersion = null)
    {
        var options = new ReSharperOptions();

        // Enabled: CLI --no-resharper > env > default (true)
        if (noResharper)
        {
            options = options with { Enabled = false };
        }
        else
        {
            var envNo = Environment.GetEnvironmentVariable("DEBUG_MCP_NO_RESHARPER");
            if (envNo is "1" or "true" or "yes")
            {
                options = options with { Enabled = false };
            }
        }

        // Cache directory: CLI > env > default
        var cache = resharperCache ?? Environment.GetEnvironmentVariable("DEBUG_MCP_RESHARPER_CACHE");
        if (!string.IsNullOrWhiteSpace(cache))
        {
            options = options with { CacheDirectory = ExpandTilde(cache) };
        }

        // Version: CLI > env > default
        var version = resharperVersion ?? Environment.GetEnvironmentVariable("DEBUG_MCP_RESHARPER_VERSION");
        if (!string.IsNullOrWhiteSpace(version))
        {
            options = options with { Version = version };
        }

        // Acquisition timeout: env > default
        if (int.TryParse(Environment.GetEnvironmentVariable("DEBUG_MCP_RESHARPER_ACQUIRE_TIMEOUT"), out var acq) && acq > 0)
        {
            options = options with { AcquisitionTimeoutSeconds = acq };
        }

        // Inspection timeout: env > default
        if (int.TryParse(Environment.GetEnvironmentVariable("DEBUG_MCP_RESHARPER_INSPECT_TIMEOUT"), out var insp) && insp > 0)
        {
            options = options with { InspectionTimeoutSeconds = insp };
        }

        // Max results: env > default
        if (int.TryParse(Environment.GetEnvironmentVariable("DEBUG_MCP_RESHARPER_MAX_RESULTS"), out var max) && max > 0)
        {
            options = options with { MaxResults = max };
        }

        return options;
    }

    private static string ExpandTilde(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        return path;
    }
}
