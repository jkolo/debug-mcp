using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// File-based persistent PDB cache using SymStore-compatible directory layout.
/// Layout: {cacheDir}/{pdbFileName}/{signature}/{pdbFileName}
/// </summary>
public class PersistentSymbolCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<PersistentSymbolCache> _logger;

    public PersistentSymbolCache(string cacheDirectory, ILogger<PersistentSymbolCache> logger)
    {
        _cacheDirectory = cacheDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the cache directory path.
    /// </summary>
    public string GetCacheDirectory() => _cacheDirectory;

    /// <summary>
    /// Tries to find a cached PDB file for the given debug info.
    /// </summary>
    /// <returns>Path to cached PDB file, or null if not cached.</returns>
    public string? TryGetPath(PeDebugInfo debugInfo)
    {
        var cachedPath = BuildCachePath(debugInfo);
        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("Symbol cache hit: {PdbFileName} ({Key})", debugInfo.PdbFileName, debugInfo.SymbolServerKey);
            return cachedPath;
        }

        return null;
    }

    /// <summary>
    /// Stores a PDB file in the persistent cache.
    /// </summary>
    /// <returns>Path to the cached copy.</returns>
    public string Store(PeDebugInfo debugInfo, string sourcePath)
    {
        var cachedPath = BuildCachePath(debugInfo);
        var cachedDir = Path.GetDirectoryName(cachedPath)!;

        try
        {
            Directory.CreateDirectory(cachedDir);
            File.Copy(sourcePath, cachedPath, overwrite: true);
            _logger.LogDebug("Cached PDB: {PdbFileName} ({Key})", debugInfo.PdbFileName, debugInfo.SymbolServerKey);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to cache PDB {PdbFileName}: {Error}", debugInfo.PdbFileName, ex.Message);
        }

        return cachedPath;
    }

    private string BuildCachePath(PeDebugInfo debugInfo)
    {
        return Path.Combine(_cacheDirectory, debugInfo.PdbFileName, debugInfo.SymbolServerKey, debugInfo.PdbFileName);
    }
}
