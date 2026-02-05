using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Services.CodeAnalysis;

/// <summary>
/// Service interface for Roslyn-based static code analysis.
/// Provides code navigation and analysis without requiring a debug session.
/// </summary>
public interface ICodeAnalysisService
{
    /// <summary>
    /// Gets information about the currently loaded workspace, or null if none loaded.
    /// </summary>
    WorkspaceInfo? CurrentWorkspace { get; }

    /// <summary>
    /// Loads a solution (.sln) or project (.csproj) file into the analysis workspace.
    /// Replaces any previously loaded workspace.
    /// </summary>
    /// <param name="path">Absolute path to .sln or .csproj file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Information about the loaded workspace.</returns>
    Task<WorkspaceInfo> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the symbol at a specific source code location.
    /// </summary>
    /// <param name="file">Absolute path to source file.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Symbol information, or null if no symbol at location.</returns>
    Task<SymbolInfo?> GetSymbolAtLocationAsync(string file, int line, int column, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a symbol by its fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">Fully qualified name (e.g., "Namespace.Class.Method").</param>
    /// <param name="symbolKind">Optional filter for symbol kind.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Symbol information, or null if not found.</returns>
    Task<SymbolInfo?> FindSymbolByNameAsync(string fullyQualifiedName, SymbolKind? symbolKind = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all usages of a symbol across the workspace.
    /// </summary>
    /// <param name="symbol">Symbol to find usages for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of usage locations.</returns>
    Task<IReadOnlyList<SymbolUsage>> FindUsagesAsync(SymbolInfo symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all assignments to a symbol (variable, field, or property).
    /// </summary>
    /// <param name="symbol">Symbol to find assignments for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of assignment locations.</returns>
    Task<IReadOnlyList<SymbolAssignment>> FindAssignmentsAsync(SymbolInfo symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets compilation diagnostics for a project or all projects in the workspace.
    /// </summary>
    /// <param name="projectName">Optional project name. If null, returns diagnostics for all projects.</param>
    /// <param name="minSeverity">Minimum severity to include (default: Warning).</param>
    /// <param name="maxResults">Maximum number of diagnostics to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of diagnostics.</returns>
    Task<IReadOnlyList<DiagnosticInfo>> GetDiagnosticsAsync(
        string? projectName = null,
        DiagnosticSeverity minSeverity = DiagnosticSeverity.Warning,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Navigates to the definition of a symbol at a given location.
    /// </summary>
    /// <param name="file">Absolute path to source file.</param>
    /// <param name="line">1-based line number.</param>
    /// <param name="column">1-based column number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Go-to-definition result, or null if no symbol at location.</returns>
    Task<GoToDefinitionResult?> GoToDefinitionAsync(string file, int line, int column, CancellationToken cancellationToken = default);
}
