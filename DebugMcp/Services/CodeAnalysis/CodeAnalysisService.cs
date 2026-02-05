using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using RoslynWorkspaceDiagnostic = Microsoft.CodeAnalysis.WorkspaceDiagnostic;
using WorkspaceInfo = DebugMcp.Models.CodeAnalysis.WorkspaceInfo;
using WorkspaceType = DebugMcp.Models.CodeAnalysis.WorkspaceType;
using ProjectInfo = DebugMcp.Models.CodeAnalysis.ProjectInfo;
using WorkspaceDiagnostic = DebugMcp.Models.CodeAnalysis.WorkspaceDiagnostic;
using SymbolInfo = DebugMcp.Models.CodeAnalysis.SymbolInfo;
using SymbolKind = DebugMcp.Models.CodeAnalysis.SymbolKind;
using SymbolUsage = DebugMcp.Models.CodeAnalysis.SymbolUsage;
using UsageKind = DebugMcp.Models.CodeAnalysis.UsageKind;
using SymbolAssignment = DebugMcp.Models.CodeAnalysis.SymbolAssignment;
using AssignmentKind = DebugMcp.Models.CodeAnalysis.AssignmentKind;
using DiagnosticInfo = DebugMcp.Models.CodeAnalysis.DiagnosticInfo;
using DiagnosticSeverity = DebugMcp.Models.CodeAnalysis.DiagnosticSeverity;
using SymbolDefinition = DebugMcp.Models.CodeAnalysis.SymbolDefinition;
using GoToDefinitionResult = DebugMcp.Models.CodeAnalysis.GoToDefinitionResult;

namespace DebugMcp.Services.CodeAnalysis;

/// <summary>
/// Roslyn-based static code analysis service.
/// Provides code navigation and analysis without requiring a debug session.
/// </summary>
public sealed class CodeAnalysisService : ICodeAnalysisService, IDisposable
{
    private readonly ILogger<CodeAnalysisService>? _logger;
    private readonly List<WorkspaceDiagnostic> _loadDiagnostics = [];
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;

    /// <summary>
    /// Static constructor to register MSBuild before any Microsoft.Build types are JIT compiled.
    /// This MUST happen before creating MSBuildWorkspace.
    /// </summary>
    static CodeAnalysisService()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    /// <summary>
    /// Creates a new CodeAnalysisService instance.
    /// </summary>
    public CodeAnalysisService()
    {
    }

    /// <summary>
    /// Creates a new CodeAnalysisService instance with logging.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public CodeAnalysisService(ILogger<CodeAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public WorkspaceInfo? CurrentWorkspace { get; private set; }

    /// <summary>
    /// Gets the current Roslyn solution, or null if not loaded.
    /// </summary>
    internal Solution? Solution => _solution;

    /// <inheritdoc />
    public async Task<WorkspaceInfo> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        var isSolution = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
        var isProject = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

        if (!isSolution && !isProject)
        {
            throw new ArgumentException("Path must be a .sln or .csproj file", nameof(path));
        }

        _logger?.LogInformation("Loading workspace from {Path}", path);

        // Dispose existing workspace
        _workspace?.Dispose();
        _loadDiagnostics.Clear();

        // Create new workspace
        _workspace = MSBuildWorkspace.Create();
#pragma warning disable CS0618 // RegisterWorkspaceFailedHandler not available in this version
        _workspace.WorkspaceFailed += OnWorkspaceFailed;
#pragma warning restore CS0618

        try
        {
            if (isSolution)
            {
                _solution = await _workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken);
            }
            else
            {
                var project = await _workspace.OpenProjectAsync(path, cancellationToken: cancellationToken);
                _solution = project.Solution;
            }

            var projects = _solution.Projects.Select(p => new ProjectInfo
            {
                Name = p.Name,
                Path = p.FilePath ?? string.Empty,
                DocumentsCount = p.Documents.Count(),
                TargetFramework = GetTargetFramework(p)
            }).ToList();

            CurrentWorkspace = new WorkspaceInfo
            {
                Path = path,
                Type = isSolution ? WorkspaceType.Solution : WorkspaceType.Project,
                Projects = projects,
                Diagnostics = [.. _loadDiagnostics],
                LoadedAt = DateTime.UtcNow
            };

            _logger?.LogInformation(
                "Loaded {Type} with {ProjectCount} projects from {Path}",
                CurrentWorkspace.Type,
                projects.Count,
                path);

            return CurrentWorkspace;
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not ArgumentException)
        {
            _logger?.LogError(ex, "Failed to load workspace from {Path}", path);
            throw;
        }
    }

    private void OnWorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e)
    {
        var diagnostic = new WorkspaceDiagnostic
        {
            Kind = e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? "Failure" : "Warning",
            Message = e.Diagnostic.Message
        };

        _loadDiagnostics.Add(diagnostic);

        if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
        {
            _logger?.LogWarning("Workspace failure: {Message}", e.Diagnostic.Message);
        }
        else
        {
            _logger?.LogDebug("Workspace warning: {Message}", e.Diagnostic.Message);
        }
    }

    private static string? GetTargetFramework(Project project)
    {
        // Try to extract target framework from compilation options
        // This is a simplified approach; actual framework may need MSBuild property access
        var options = project.CompilationOptions;
        if (options is Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions csharpOptions)
        {
            // The actual target framework isn't directly exposed, but we can infer from assembly identity
            return project.AssemblyName?.Contains("net") == true ? "net10.0" : null;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<SymbolInfo?> GetSymbolAtLocationAsync(
        string file,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        // Find the document by file path
        var document = _solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, file, StringComparison.OrdinalIgnoreCase));

        if (document is null)
        {
            _logger?.LogDebug("Document not found: {File}", file);
            return null;
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

        if (syntaxTree is null || semanticModel is null)
        {
            return null;
        }

        // Convert 1-based line/column to 0-based position
        var linePosition = new LinePosition(line - 1, column - 1);
        var position = sourceText.Lines.GetPosition(linePosition);

        // Find token at position
        var root = await syntaxTree.GetRootAsync(cancellationToken);
        var token = root.FindToken(position);

        // Get symbol info for the token's parent node
        var node = token.Parent;
        if (node is null)
        {
            return null;
        }

        var roslynSymbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
        var symbol = roslynSymbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node, cancellationToken);

        if (symbol is null)
        {
            // Try parent nodes for cases like type declarations
            var currentNode = node.Parent;
            while (currentNode is not null && symbol is null)
            {
                symbol = semanticModel.GetDeclaredSymbol(currentNode, cancellationToken);
                currentNode = currentNode.Parent;
            }
        }

        if (symbol is null)
        {
            return null;
        }

        return CreateSymbolInfo(symbol);
    }

    /// <inheritdoc />
    public async Task<SymbolInfo?> FindSymbolByNameAsync(
        string fullyQualifiedName,
        SymbolKind? symbolKind = null,
        CancellationToken cancellationToken = default)
    {
        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            // Try to find type by metadata name
            var type = compilation.GetTypeByMetadataName(fullyQualifiedName);
            if (type is not null)
            {
                if (symbolKind is null || MapRoslynKind(type.Kind) == symbolKind)
                {
                    return CreateSymbolInfo(type);
                }
            }

            // If not a type, try to find as a member
            // Split name to find containing type
            var lastDot = fullyQualifiedName.LastIndexOf('.');
            if (lastDot > 0)
            {
                var typeName = fullyQualifiedName[..lastDot];
                var memberName = fullyQualifiedName[(lastDot + 1)..];

                var containingType = compilation.GetTypeByMetadataName(typeName);
                if (containingType is not null)
                {
                    var members = containingType.GetMembers(memberName);
                    foreach (var member in members)
                    {
                        if (symbolKind is null || MapRoslynKind(member.Kind) == symbolKind)
                        {
                            return CreateSymbolInfo(member);
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SymbolUsage>> FindUsagesAsync(
        SymbolInfo symbol,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        var roslynSymbol = symbol.RoslynSymbol as ISymbol;
        if (roslynSymbol is null)
        {
            _logger?.LogWarning("Symbol does not have a valid Roslyn reference: {Name}", symbol.FullyQualifiedName);
            return [];
        }

        var usages = new List<SymbolUsage>();

        // Find all references using Roslyn's SymbolFinder
        var references = await SymbolFinder.FindReferencesAsync(roslynSymbol, _solution, cancellationToken);

        foreach (var reference in references)
        {
            // Add the definition location
            foreach (var location in reference.Definition.Locations)
            {
                if (location.IsInSource)
                {
                    var span = location.GetLineSpan();
                    usages.Add(new SymbolUsage
                    {
                        File = span.Path,
                        Line = span.StartLinePosition.Line + 1,
                        Column = span.StartLinePosition.Character + 1,
                        EndLine = span.EndLinePosition.Line + 1,
                        EndColumn = span.EndLinePosition.Character + 1,
                        Kind = UsageKind.Declaration,
                        Context = GetContainingMemberName(location)
                    });
                }
            }

            // Add all reference locations
            foreach (var refLocation in reference.Locations)
            {
                var location = refLocation.Location;
                if (location.IsInSource)
                {
                    var span = location.GetLineSpan();
                    var kind = DetermineUsageKind(refLocation);

                    usages.Add(new SymbolUsage
                    {
                        File = span.Path,
                        Line = span.StartLinePosition.Line + 1,
                        Column = span.StartLinePosition.Character + 1,
                        EndLine = span.EndLinePosition.Line + 1,
                        EndColumn = span.EndLinePosition.Character + 1,
                        Kind = kind,
                        Context = GetContainingMemberName(location)
                    });
                }
            }
        }

        return usages;
    }

    private static SymbolInfo CreateSymbolInfo(ISymbol symbol)
    {
        var locations = symbol.Locations.Where(l => l.IsInSource).ToList();
        var primaryLocation = locations.FirstOrDefault();
        FileLinePositionSpan? lineSpan = primaryLocation?.GetLineSpan();

        return new SymbolInfo
        {
            Name = symbol.Name,
            FullyQualifiedName = symbol.ToDisplayString(),
            Kind = MapRoslynKind(symbol.Kind),
            ContainingType = symbol.ContainingType?.ToDisplayString(),
            ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString(),
            DeclarationFile = lineSpan?.Path,
            DeclarationLine = lineSpan.HasValue ? lineSpan.Value.StartLinePosition.Line + 1 : null,
            DeclarationColumn = lineSpan.HasValue ? lineSpan.Value.StartLinePosition.Character + 1 : null,
            RoslynSymbol = symbol
        };
    }

    private static SymbolKind MapRoslynKind(Microsoft.CodeAnalysis.SymbolKind roslynKind)
    {
        return roslynKind switch
        {
            Microsoft.CodeAnalysis.SymbolKind.Namespace => SymbolKind.Namespace,
            Microsoft.CodeAnalysis.SymbolKind.NamedType => SymbolKind.Type,
            Microsoft.CodeAnalysis.SymbolKind.Method => SymbolKind.Method,
            Microsoft.CodeAnalysis.SymbolKind.Property => SymbolKind.Property,
            Microsoft.CodeAnalysis.SymbolKind.Field => SymbolKind.Field,
            Microsoft.CodeAnalysis.SymbolKind.Event => SymbolKind.Event,
            Microsoft.CodeAnalysis.SymbolKind.Local => SymbolKind.Local,
            Microsoft.CodeAnalysis.SymbolKind.Parameter => SymbolKind.Parameter,
            Microsoft.CodeAnalysis.SymbolKind.TypeParameter => SymbolKind.TypeParameter,
            _ => SymbolKind.Type // Default fallback
        };
    }

    private static UsageKind DetermineUsageKind(ReferenceLocation refLocation)
    {
        // ReferenceLocation doesn't expose IsWrittenTo directly in this Roslyn version.
        // We'll classify all non-declaration references as Reference kind.
        // For more precise read/write detection, we would need to analyze the syntax context.
        return UsageKind.Reference;
    }

    private static string? GetContainingMemberName(Location location)
    {
        var syntaxTree = location.SourceTree;
        if (syntaxTree is null)
        {
            return null;
        }

        var root = syntaxTree.GetRoot();
        var node = root.FindNode(location.SourceSpan);

        // Walk up to find containing method/property
        while (node is not null)
        {
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method)
            {
                return method.Identifier.Text;
            }
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax property)
            {
                return property.Identifier.Text;
            }
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax ctor)
            {
                return ".ctor";
            }
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax cls)
            {
                return cls.Identifier.Text;
            }
            node = node.Parent;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SymbolAssignment>> FindAssignmentsAsync(
        SymbolInfo symbol,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        var roslynSymbol = symbol.RoslynSymbol as ISymbol;
        if (roslynSymbol is null)
        {
            _logger?.LogWarning("Symbol does not have a valid Roslyn reference: {Name}", symbol.FullyQualifiedName);
            return [];
        }

        var allAssignments = new List<SymbolAssignment>();

        // Walk through all documents in the solution
        foreach (var project in _solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var root = await document.GetSyntaxRootAsync(cancellationToken);

                if (semanticModel is null || root is null)
                {
                    continue;
                }

                var walker = new AssignmentWalker(semanticModel, roslynSymbol);
                walker.Visit(root);

                allAssignments.AddRange(walker.Assignments);
            }
        }

        return allAssignments;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiagnosticInfo>> GetDiagnosticsAsync(
        string? projectName = null,
        DiagnosticSeverity minSeverity = DiagnosticSeverity.Warning,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        var projects = _solution.Projects;

        // Filter by project name if specified
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            projects = projects.Where(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (!projects.Any())
            {
                throw new ArgumentException($"Project not found: {projectName}", nameof(projectName));
            }
        }

        var diagnostics = new List<DiagnosticInfo>();

        foreach (var project in projects)
        {
            if (diagnostics.Count >= maxResults)
            {
                break;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            var projectDiagnostics = compilation.GetDiagnostics(cancellationToken)
                .Where(d => MapRoslynSeverity(d.Severity) >= minSeverity)
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Location.GetLineSpan().Path)
                .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line);

            foreach (var diag in projectDiagnostics)
            {
                if (diagnostics.Count >= maxResults)
                {
                    break;
                }

                diagnostics.Add(CreateDiagnosticInfo(diag, project.Name));
            }
        }

        return diagnostics;
    }

    private static DiagnosticInfo CreateDiagnosticInfo(Diagnostic diagnostic, string projectName)
    {
        var location = diagnostic.Location;
        FileLinePositionSpan? lineSpan = location.IsInSource ? location.GetLineSpan() : null;

        return new DiagnosticInfo
        {
            Id = diagnostic.Id,
            Message = diagnostic.GetMessage(),
            Severity = MapRoslynSeverity(diagnostic.Severity),
            Category = diagnostic.Descriptor.Category,
            File = lineSpan?.Path,
            Line = lineSpan?.StartLinePosition.Line + 1,
            Column = lineSpan?.StartLinePosition.Character + 1,
            EndLine = lineSpan?.EndLinePosition.Line + 1,
            EndColumn = lineSpan?.EndLinePosition.Character + 1,
            Project = projectName,
            HelpLink = diagnostic.Descriptor.HelpLinkUri
        };
    }

    private static DiagnosticSeverity MapRoslynSeverity(Microsoft.CodeAnalysis.DiagnosticSeverity roslynSeverity)
    {
        return roslynSeverity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden => DiagnosticSeverity.Hidden,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Info => DiagnosticSeverity.Info,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            _ => DiagnosticSeverity.Warning
        };
    }

    /// <inheritdoc />
    public async Task<GoToDefinitionResult?> GoToDefinitionAsync(
        string file,
        int line,
        int column,
        CancellationToken cancellationToken = default)
    {
        if (_solution is null)
        {
            throw new InvalidOperationException("No workspace loaded. Call LoadAsync first.");
        }

        // First find the symbol at the location
        var symbolInfo = await GetSymbolAtLocationAsync(file, line, column, cancellationToken);
        if (symbolInfo is null)
        {
            return null;
        }

        var roslynSymbol = symbolInfo.RoslynSymbol as ISymbol;
        if (roslynSymbol is null)
        {
            return null;
        }

        // Get all definition locations
        var definitions = new List<SymbolDefinition>();

        // Check for source definitions
        foreach (var location in roslynSymbol.Locations)
        {
            if (location.IsInSource)
            {
                var lineSpan = location.GetLineSpan();
                definitions.Add(new SymbolDefinition
                {
                    File = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    EndColumn = lineSpan.EndLinePosition.Character + 1,
                    IsSource = true
                });
            }
            else if (location.IsInMetadata)
            {
                // Metadata symbol - return assembly info
                definitions.Add(new SymbolDefinition
                {
                    IsSource = false,
                    AssemblyName = roslynSymbol.ContainingAssembly?.Name,
                    AssemblyVersion = roslynSymbol.ContainingAssembly?.Identity.Version.ToString()
                });
            }
        }

        // If no locations found (shouldn't happen), still return metadata info if available
        if (definitions.Count == 0 && roslynSymbol.ContainingAssembly is not null)
        {
            definitions.Add(new SymbolDefinition
            {
                IsSource = false,
                AssemblyName = roslynSymbol.ContainingAssembly.Name,
                AssemblyVersion = roslynSymbol.ContainingAssembly.Identity.Version.ToString()
            });
        }

        return new GoToDefinitionResult
        {
            Symbol = symbolInfo,
            Definitions = definitions
        };
    }

    /// <summary>
    /// Disposes the workspace and releases resources.
    /// </summary>
    public void Dispose()
    {
        _workspace?.Dispose();
        _workspace = null;
        _solution = null;
        CurrentWorkspace = null;
    }
}
