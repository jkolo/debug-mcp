# Research: Roslyn Code Analysis

**Feature**: 015-roslyn-code-analysis
**Date**: 2026-02-04
**Status**: Complete

## 1. NuGet Package Requirements

**Decision**: Use Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0 with Microsoft.Build.Locator

**Rationale**: Version 5.0.0 is the latest stable version compatible with .NET 10.0. MSBuildLocator is required to resolve MSBuild dependencies at runtime.

**Alternatives Considered**:
- Roslyn 4.9+ out-of-process approach - Rejected: adds process management complexity
- AdhocWorkspace - Rejected: doesn't support .sln/.csproj loading

**Package Configuration**:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.0.0" />
  <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.0.0" />
  <PackageReference Include="Microsoft.Build.Locator" Version="1.7.8" />

  <!-- CRITICAL: Exclude runtime to avoid assembly conflicts -->
  <PackageReference Include="Microsoft.Build" Version="17.*" ExcludeAssets="runtime" />
  <PackageReference Include="Microsoft.Build.Tasks.Core" Version="17.*" ExcludeAssets="runtime" />
</ItemGroup>
```

---

## 2. MSBuildWorkspace Setup

**Decision**: Use MSBuildLocator.RegisterDefaults() in static constructor, create single MSBuildWorkspace per session

**Rationale**: MSBuildLocator must register before JIT compiles any Microsoft.Build types. Single workspace allows solution replacement per FR-009.

**Critical Implementation Rules**:
1. Call `MSBuildLocator.RegisterDefaults()` before creating `MSBuildWorkspace`
2. Use `ExcludeAssets="runtime"` for Microsoft.Build packages
3. Subscribe to `WorkspaceFailed` event (MSBuildWorkspace fails silently otherwise)

**Code Pattern**:
```csharp
static CodeAnalysisService()
{
    if (!MSBuildLocator.IsRegistered)
    {
        MSBuildLocator.RegisterDefaults();
    }
}

public async Task<WorkspaceInfo> LoadAsync(string path, IProgress<ProjectLoadProgress>? progress)
{
    _workspace?.Dispose();
    _workspace = MSBuildWorkspace.Create();
    _workspace.WorkspaceFailed += (s, e) => { /* log failures */ };

    if (path.EndsWith(".sln"))
        _solution = await _workspace.OpenSolutionAsync(path, progress);
    else
        var project = await _workspace.OpenProjectAsync(path, progress);
        _solution = project.Solution;
}
```

**Sources**:
- [Using MSBuildWorkspace](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)
- [Analysing .NET with Roslyn](https://dev.to/mattjhosking/analysing-a-net-codebase-with-roslyn-5cn0)

---

## 3. Symbol Resolution

**Decision**: Support both fully qualified name AND file location (path + line + column)

**For Fully Qualified Names**:
```csharp
// Use Compilation.GetTypeByMetadataName for types
var symbol = compilation.GetTypeByMetadataName("MyNamespace.MyClass");

// For generic types, use backtick notation
var listSymbol = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

// For nested classes, use + separator
var nestedSymbol = compilation.GetTypeByMetadataName("OuterClass+NestedClass");
```

**For File Location**:
```csharp
var documentId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
var document = solution.GetDocument(documentId);
var text = await document.GetTextAsync();
var position = text.Lines.GetPosition(new LinePosition(line - 1, column - 1)); // 0-based

var root = await document.GetSyntaxRootAsync();
var token = root.FindToken(position);
var node = token.Parent;

var model = await document.GetSemanticModelAsync();
var symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
```

**Sources**:
- [Microsoft Learn - Semantic Model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-semantics)
- [Compilation.GetTypeByMetadataName](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.gettypebymetadataname)

---

## 4. Find All References

**Decision**: Use SymbolFinder.FindReferencesAsync from Microsoft.CodeAnalysis.FindSymbols

**Rationale**: This is the official API for finding symbol references, handles cross-project resolution.

**Implementation Pattern**:
```csharp
using Microsoft.CodeAnalysis.FindSymbols;

var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken);
foreach (var refSymbol in references)
{
    foreach (var location in refSymbol.Locations)
    {
        var lineSpan = location.Location.GetLineSpan();
        yield return new SymbolUsage
        {
            FilePath = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            IsDefinition = location.IsImplicit == false && location.Location == symbol.Locations.First()
        };
    }
}
```

**Performance Notes**:
- Use document filtering (`IImmutableSet<Document>`) to limit search scope
- Always pass CancellationToken for long-running operations

**Sources**:
- [Microsoft Learn - SymbolFinder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder)

---

## 5. Assignment Detection

**Decision**: Use CSharpSyntaxWalker to detect all write operations

**Rationale**: Syntax walker covers all assignment forms without needing data flow analysis for each statement.

**Write Operations to Detect**:
- Simple assignment: `x = value`
- Compound assignment: `x += value`, `x -= value`, etc.
- Increment/Decrement: `x++`, `++x`, `x--`, `--x`
- Out/Ref parameters: `Method(out x)`, `Method(ref x)`
- Deconstruction: `(x, y) = tuple`
- Property setters: `obj.Property = value`

**Implementation Pattern**:
```csharp
public class AssignmentWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _model;
    private readonly ISymbol _target;
    public List<Location> Assignments { get; } = new();

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        CheckTarget(node.Left);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PreIncrementExpression) ||
            node.IsKind(SyntaxKind.PreDecrementExpression))
            CheckTarget(node.Operand);
        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PostIncrementExpression) ||
            node.IsKind(SyntaxKind.PostDecrementExpression))
            CheckTarget(node.Operand);
        base.VisitPostfixUnaryExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        if (node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) ||
            node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
            CheckTarget(node.Expression);
        base.VisitArgument(node);
    }

    private void CheckTarget(ExpressionSyntax expr)
    {
        var symbol = _model.GetSymbolInfo(expr).Symbol;
        if (SymbolEqualityComparer.Default.Equals(symbol, _target))
            Assignments.Add(expr.GetLocation());
    }
}
```

**Sources**:
- [Learn Roslyn Now - Data Flow Analysis](https://joshvarty.com/2015/02/05/learn-roslyn-now-part-8-data-flow-analysis/)

---

## 6. Diagnostics Retrieval

**Decision**: Use Compilation.GetDiagnostics() with severity filtering

**Rationale**: Returns all compiler diagnostics matching `dotnet build` output (SC-003 requirement).

**Implementation Pattern**:
```csharp
var compilation = await project.GetCompilationAsync(cancellationToken);
var diagnostics = compilation.GetDiagnostics(cancellationToken);

foreach (var diag in diagnostics)
{
    var span = diag.Location.GetLineSpan();
    yield return new DiagnosticInfo
    {
        Id = diag.Id,
        Message = diag.GetMessage(),
        Severity = diag.Severity.ToString(),
        FilePath = span.Path,
        Line = span.StartLinePosition.Line + 1,
        Column = span.StartLinePosition.Character + 1
    };
}
```

**Notes**:
- `GetDiagnostics()` is expensive (near-full compilation)
- `GetDeclarationDiagnostics()` is lighter but misses method body errors
- Suppressed diagnostics (`#pragma`, `SuppressMessage`) are filtered by default

**Sources**:
- [Compilation.GetDiagnostics](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.getdiagnostics)

---

## 7. Go-to-Definition

**Decision**: Use DeclaringSyntaxReferences for source symbols, symbol.Locations for metadata

**Rationale**: Handles partial classes (multiple definitions) and external symbols without sources.

**Implementation Pattern**:
```csharp
public IEnumerable<SymbolDefinition> GetDefinitions(ISymbol symbol)
{
    // Source definitions
    foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
    {
        var node = syntaxRef.GetSyntax();
        var span = node.GetLocation().GetLineSpan();
        yield return new SymbolDefinition
        {
            FilePath = span.Path,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            IsFromSource = true
        };
    }

    // Metadata location (no source available)
    if (!symbol.DeclaringSyntaxReferences.Any())
    {
        foreach (var loc in symbol.Locations.Where(l => l.IsInMetadata))
        {
            yield return new SymbolDefinition
            {
                AssemblyName = loc.MetadataModule?.ContainingAssembly?.Name,
                IsFromSource = false
            };
        }
    }
}
```

**Sources**:
- [How to get SyntaxNode of ISymbol](https://jaylee.org/archive/2020/07/19/how-to-get-the-syntaxnode-for-a-symbol-using-roslyn.html)

---

## 8. Progress Reporting

**Decision**: Implement IProgress<ProjectLoadProgress> and convert to MCP progress notifications

**Rationale**: MSBuildWorkspace supports IProgress<ProjectLoadProgress>, can be bridged to MCP protocol.

**Implementation Pattern**:
```csharp
public class McpProgressReporter : IProgress<ProjectLoadProgress>
{
    private readonly IMcpServer _server;
    private readonly ProgressToken _token;
    private int _projectCount;
    private int _loaded;

    public void Report(ProjectLoadProgress value)
    {
        if (value.Operation == ProjectLoadOperation.Build)
        {
            _loaded++;
            var percent = _projectCount > 0 ? (double)_loaded / _projectCount * 100 : 0;

            // Send MCP progress notification
            _server.SendNotificationAsync(
                NotificationMethods.ProgressNotification,
                new ProgressNotificationParams
                {
                    ProgressToken = _token,
                    Progress = new { message = $"Loading {Path.GetFileName(value.FilePath)}", percentage = percent }
                });
        }
    }
}
```

---

## 9. Error Handling Strategy

**Decision**: Continue loading on failures, report in result with workspace diagnostics

**For Unresolved NuGet Packages**:
- MSBuildWorkspace continues loading
- Failed references reported via WorkspaceFailed event
- Returned in load result as warnings

**For Invalid Symbols**:
- Return structured error with code `SYMBOL_NOT_FOUND`
- Include attempted identifier in error details

**For Missing Workspace**:
- Return error with code `NO_WORKSPACE`
- Clear message: "No solution or project loaded. Use code_load first."

---

## Summary

All technical questions resolved. Ready to proceed with Phase 1 design artifacts.
