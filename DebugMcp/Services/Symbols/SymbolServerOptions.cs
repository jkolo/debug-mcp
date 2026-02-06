namespace DebugMcp.Services.Symbols;

/// <summary>
/// Configuration for symbol server behavior.
/// </summary>
public sealed record SymbolServerOptions
{
    public static readonly string DefaultMicrosoftServer = "https://msdl.microsoft.com/download/symbols";
    public static readonly string DefaultNuGetServer = "https://symbols.nuget.org/download/symbols";
    public static readonly string DefaultCacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".debug-mcp", "symbols");

    /// <summary>Ordered list of SSQP symbol server base URLs.</summary>
    public IReadOnlyList<string> ServerUrls { get; init; } = [DefaultMicrosoftServer, DefaultNuGetServer];

    /// <summary>Persistent cache directory path.</summary>
    public string CacheDirectory { get; init; } = DefaultCacheDirectory;

    /// <summary>Master enable/disable for symbol server downloads.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Per-file download timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Maximum PDB file size to download in MB.</summary>
    public int MaxFileSizeMB { get; init; } = 100;

    /// <summary>Maximum number of parallel downloads.</summary>
    public int MaxConcurrentDownloads { get; init; } = 4;

    /// <summary>
    /// Creates SymbolServerOptions from CLI arguments and environment variables.
    /// Priority: CLI argument > environment variable > default.
    /// </summary>
    public static SymbolServerOptions Create(
        string? symbolServers = null,
        string? symbolCache = null,
        bool noSymbols = false,
        int? symbolTimeout = null,
        int? symbolMaxSize = null)
    {
        var options = new SymbolServerOptions();

        // Resolve server URLs: CLI > env var > default
        var servers = symbolServers
            ?? Environment.GetEnvironmentVariable("DEBUG_MCP_SYMBOL_SERVERS");
        if (servers != null)
        {
            var urls = servers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();
            if (urls.Count > 0)
            {
                options = options with { ServerUrls = urls };
            }
        }

        // Resolve cache directory: CLI > env var > default
        var cache = symbolCache
            ?? Environment.GetEnvironmentVariable("DEBUG_MCP_SYMBOL_CACHE");
        if (cache != null)
        {
            options = options with { CacheDirectory = ExpandTilde(cache) };
        }

        // Resolve enabled: CLI --no-symbols > env var > default (true)
        if (noSymbols)
        {
            options = options with { Enabled = false };
        }
        else
        {
            var envNoSymbols = Environment.GetEnvironmentVariable("DEBUG_MCP_NO_SYMBOLS");
            if (envNoSymbols is "1" or "true" or "yes")
            {
                options = options with { Enabled = false };
            }
        }

        // Resolve timeout: CLI > env var > default
        if (symbolTimeout.HasValue)
        {
            options = options with { TimeoutSeconds = symbolTimeout.Value };
        }
        else if (int.TryParse(Environment.GetEnvironmentVariable("DEBUG_MCP_SYMBOL_TIMEOUT"), out var envTimeout) && envTimeout > 0)
        {
            options = options with { TimeoutSeconds = envTimeout };
        }

        // Resolve max size: CLI > env var > default
        if (symbolMaxSize.HasValue)
        {
            options = options with { MaxFileSizeMB = symbolMaxSize.Value };
        }
        else if (int.TryParse(Environment.GetEnvironmentVariable("DEBUG_MCP_SYMBOL_MAX_SIZE"), out var envMaxSize) && envMaxSize > 0)
        {
            options = options with { MaxFileSizeMB = envMaxSize };
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
