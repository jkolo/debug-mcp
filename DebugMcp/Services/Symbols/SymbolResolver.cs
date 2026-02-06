using System.Collections.Concurrent;
using DebugMcp.Models.Modules;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// Orchestrates the symbol resolution chain: local → embedded → cache → server.
/// Thread-safe, tracks per-module status.
/// </summary>
public class SymbolResolver : ISymbolResolver
{
    private readonly PeDebugInfoReader _peReader;
    private readonly PersistentSymbolCache _persistentCache;
    private readonly SymbolServerClient _serverClient;
    private readonly SymbolServerOptions _options;
    private readonly ILogger<SymbolResolver> _logger;
    private readonly SemaphoreSlim _downloadSemaphore;
    private readonly ConcurrentDictionary<string, SymbolResolutionResult> _statusCache = new(StringComparer.OrdinalIgnoreCase);

    public SymbolResolver(
        PeDebugInfoReader peReader,
        PersistentSymbolCache persistentCache,
        SymbolServerClient serverClient,
        SymbolServerOptions options,
        ILogger<SymbolResolver> logger)
    {
        _peReader = peReader;
        _persistentCache = persistentCache;
        _serverClient = serverClient;
        _options = options;
        _logger = logger;
        _downloadSemaphore = new SemaphoreSlim(options.MaxConcurrentDownloads);
    }

    /// <inheritdoc/>
    public SymbolStatus GetStatus(string assemblyPath)
    {
        var normalized = Path.GetFullPath(assemblyPath);
        return _statusCache.TryGetValue(normalized, out var result) ? result.Status : SymbolStatus.None;
    }

    /// <summary>
    /// Gets the full resolution result for an assembly.
    /// </summary>
    public SymbolResolutionResult? GetResult(string assemblyPath)
    {
        var normalized = Path.GetFullPath(assemblyPath);
        return _statusCache.TryGetValue(normalized, out var result) ? result : null;
    }

    /// <inheritdoc/>
    public async Task<SymbolResolutionResult> ResolveAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(assemblyPath);

        // Return cached result if already resolved
        if (_statusCache.TryGetValue(normalized, out var existing) && existing.Status is SymbolStatus.Loaded or SymbolStatus.NotFound)
        {
            return existing;
        }

        var result = await ResolveInternalAsync(normalized, cancellationToken);
        _statusCache[normalized] = result;
        return result;
    }

    private async Task<SymbolResolutionResult> ResolveInternalAsync(string assemblyPath, CancellationToken cancellationToken)
    {
        // Step 1: Check local PDB (same directory as assembly)
        var localPdb = TryFindLocalPdb(assemblyPath);
        if (localPdb != null)
        {
            _logger.LogDebug("Local PDB found: {PdbPath}", localPdb);
            return new SymbolResolutionResult(SymbolStatus.Loaded, localPdb, SymbolSource.Local, null);
        }

        // Read PE debug info (needed for all remaining steps)
        var debugInfo = _peReader.TryReadDebugInfo(assemblyPath);
        if (debugInfo == null)
        {
            _logger.LogDebug("No debug info in assembly: {AssemblyPath}", assemblyPath);
            return new SymbolResolutionResult(SymbolStatus.NotFound, null, SymbolSource.None, "No debug info in assembly");
        }

        // Step 2: Check embedded PDB (will be implemented in US4)
        if (debugInfo.HasEmbeddedPdb)
        {
            var embeddedResult = TryResolveEmbeddedPdb(assemblyPath, debugInfo);
            if (embeddedResult != null)
            {
                return embeddedResult;
            }
        }

        // Step 3: Check persistent symbol cache
        var cachedPath = _persistentCache.TryGetPath(debugInfo);
        if (cachedPath != null)
        {
            _logger.LogDebug("Symbol cache hit: {PdbPath}", cachedPath);
            return new SymbolResolutionResult(SymbolStatus.Loaded, cachedPath, SymbolSource.Cache, null);
        }

        // Step 4: Download from symbol servers
        if (!_options.Enabled)
        {
            _logger.LogDebug("Symbol server downloads disabled for: {AssemblyPath}", assemblyPath);
            return new SymbolResolutionResult(SymbolStatus.NotFound, null, SymbolSource.None, "Symbol downloads disabled");
        }

        return await TryDownloadFromServersAsync(assemblyPath, debugInfo, cancellationToken);
    }

    private static string? TryFindLocalPdb(string assemblyPath)
    {
        var directory = Path.GetDirectoryName(assemblyPath);
        var baseName = Path.GetFileNameWithoutExtension(assemblyPath);

        if (directory == null) return null;

        var pdbPath = Path.Combine(directory, baseName + ".pdb");
        return File.Exists(pdbPath) ? pdbPath : null;
    }

    private SymbolResolutionResult? TryResolveEmbeddedPdb(string assemblyPath, PeDebugInfo debugInfo)
    {
        // Extract embedded PDB to persistent cache
        var cachedPath = _persistentCache.TryGetPath(debugInfo);
        if (cachedPath != null)
        {
            return new SymbolResolutionResult(SymbolStatus.Loaded, cachedPath, SymbolSource.Embedded, null);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"debug-mcp-embedded-{Guid.NewGuid():N}.pdb");
        try
        {
            if (_peReader.TryExtractEmbeddedPdb(assemblyPath, tempPath))
            {
                var storedPath = _persistentCache.Store(debugInfo, tempPath);
                _logger.LogDebug("Extracted embedded PDB for {AssemblyPath} to cache", assemblyPath);
                return new SymbolResolutionResult(SymbolStatus.Loaded, storedPath, SymbolSource.Embedded, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embedded PDB from {AssemblyPath}", assemblyPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return null;
    }

    private async Task<SymbolResolutionResult> TryDownloadFromServersAsync(
        string assemblyPath, PeDebugInfo debugInfo, CancellationToken cancellationToken)
    {
        // Update status to pending
        _statusCache[assemblyPath] = new SymbolResolutionResult(SymbolStatus.PendingDownload, null, SymbolSource.None, null);

        await _downloadSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Update status to downloading
            _statusCache[assemblyPath] = new SymbolResolutionResult(SymbolStatus.Downloading, null, SymbolSource.None, null);

            foreach (var serverUrl in _options.ServerUrls)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"debug-mcp-download-{Guid.NewGuid():N}.pdb");
                try
                {
                    var downloaded = await _serverClient.TryDownloadAsync(serverUrl, debugInfo, tempPath, cancellationToken);
                    if (!downloaded) continue;

                    // Validate the downloaded PDB
                    if (!_peReader.ValidatePdb(tempPath, debugInfo))
                    {
                        _logger.LogWarning("Downloaded PDB failed validation: {PdbFileName} from {ServerUrl}",
                            debugInfo.PdbFileName, serverUrl);
                        File.Delete(tempPath);
                        continue;
                    }

                    // Store in persistent cache
                    var cachedPath = _persistentCache.Store(debugInfo, tempPath);
                    _logger.LogInformation("Symbol resolved from server: {PdbFileName} via {ServerUrl}",
                        debugInfo.PdbFileName, serverUrl);

                    return new SymbolResolutionResult(SymbolStatus.Loaded, cachedPath, SymbolSource.Server, null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Symbol download failed from {ServerUrl} for {PdbFileName}",
                        serverUrl, debugInfo.PdbFileName);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
            }

            return new SymbolResolutionResult(SymbolStatus.NotFound, null, SymbolSource.None,
                $"PDB not found on any configured server ({_options.ServerUrls.Count} servers checked)");
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }
}
