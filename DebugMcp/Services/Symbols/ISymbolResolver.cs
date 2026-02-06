using DebugMcp.Models.Modules;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// Orchestrates the symbol resolution chain: local → embedded → cache → server.
/// </summary>
public interface ISymbolResolver
{
    /// <summary>
    /// Resolves symbols for an assembly, checking all configured sources.
    /// </summary>
    Task<SymbolResolutionResult> ResolveAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current symbol resolution status for an assembly.
    /// </summary>
    SymbolStatus GetStatus(string assemblyPath);

    /// <summary>
    /// Gets the full resolution result for an assembly, or null if not yet resolved.
    /// </summary>
    SymbolResolutionResult? GetResult(string assemblyPath);
}
