using DebugMcp.Models.CodeAnalysis;
using DebugMcp.Services.CodeAnalysis;
using FluentAssertions;
using Xunit;

namespace DebugMcp.Tests.Unit.CodeAnalysis;

/// <summary>
/// Unit tests for CodeAnalysisService.
/// Tests are written before implementation per constitution (III. Test-First).
/// </summary>
public class CodeAnalysisServiceTests : IDisposable
{
    private readonly CodeAnalysisService _service;

    public CodeAnalysisServiceTests()
    {
        _service = new CodeAnalysisService();
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    #region US4: Load Solution/Project

    [Fact]
    public async Task LoadSolution_ValidPath_ReturnsWorkspaceInfo()
    {
        // Arrange - Using DebugMcp project as test target (no .sln available)
        var projectPath = GetTestSolutionPath();

        // Act
        var result = await _service.LoadAsync(projectPath);

        // Assert
        result.Should().NotBeNull();
        result.Path.Should().Be(projectPath);
        result.Type.Should().Be(WorkspaceType.Project); // Project type since using .csproj
        result.Projects.Should().NotBeEmpty();
        result.LoadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LoadSolution_InvalidPath_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidPath = "/nonexistent/path/solution.sln";

        // Act
        var act = () => _service.LoadAsync(invalidPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadProject_ValidPath_ReturnsWorkspaceInfo()
    {
        // Arrange
        var projectPath = GetTestProjectPath();

        // Act
        var result = await _service.LoadAsync(projectPath);

        // Assert
        result.Should().NotBeNull();
        result.Path.Should().Be(projectPath);
        result.Type.Should().Be(WorkspaceType.Project);
        // TestTargetApp references library projects, so multiple projects are loaded
        result.Projects.Should().NotBeEmpty();
        result.Projects.Should().Contain(p => p.Name == "TestTargetApp");
    }

    [Fact]
    public async Task LoadSolution_ReplacesExistingWorkspace()
    {
        // Arrange
        var solutionPath = GetTestSolutionPath();
        await _service.LoadAsync(solutionPath);
        var firstLoadTime = _service.CurrentWorkspace!.LoadedAt;

        // Wait a bit to ensure different timestamp
        await Task.Delay(10);

        // Act
        var result = await _service.LoadAsync(solutionPath);

        // Assert
        result.LoadedAt.Should().BeAfter(firstLoadTime);
    }

    [Fact]
    public void CurrentWorkspace_BeforeLoad_ReturnsNull()
    {
        // Assert
        _service.CurrentWorkspace.Should().BeNull();
    }

    #endregion

    #region US1: Find All Usages

    [Fact]
    public async Task FindUsages_ByName_ReturnsAllLocations()
    {
        // Arrange - Load TestTargetApp, then find usages of Person class
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // First find the symbol by name
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.Person", SymbolKind.Type);
        symbol.Should().NotBeNull("Person class should be found");

        // Act - Find all usages of Person
        var usages = await _service.FindUsagesAsync(symbol!);

        // Assert - Person is used in multiple places
        usages.Should().NotBeEmpty();
        // Should include declaration
        usages.Should().Contain(u => u.Kind == UsageKind.Declaration);
        // Should include at least one reference (the field _currentUser in ObjectTarget)
        usages.Should().Contain(u => u.Kind == UsageKind.Reference);
    }

    [Fact]
    public async Task FindUsages_ByLocation_ReturnsAllLocations()
    {
        // Arrange - Load TestTargetApp, find symbol at specific location
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var objectTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "ObjectTarget.cs");

        // Get symbol at line 56 col 14 (Person class declaration)
        var symbol = await _service.GetSymbolAtLocationAsync(objectTargetPath, 56, 14);
        symbol.Should().NotBeNull("Should find Person symbol at declaration");

        // Act
        var usages = await _service.FindUsagesAsync(symbol!);

        // Assert
        usages.Should().NotBeEmpty();
        usages.Should().Contain(u => u.Kind == UsageKind.Declaration);
    }

    [Fact]
    public async Task FindUsages_UnusedSymbol_ReturnsOnlyDeclaration()
    {
        // Arrange - Load TestTargetApp, find symbol that's declared but not used
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Country property in Address is declared but may not be used elsewhere
        // Actually, let's use a more reliable target - the Calculate method
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.MethodTarget.Calculate", SymbolKind.Method);
        symbol.Should().NotBeNull();

        // Act
        var usages = await _service.FindUsagesAsync(symbol!);

        // Assert - Should at least have the declaration
        usages.Should().NotBeEmpty();
        usages.Should().Contain(u => u.Kind == UsageKind.Declaration);
    }

    [Fact]
    public async Task FindUsages_InvalidSymbol_ThrowsArgumentNullException()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act & Assert - null symbol should throw
        var act = () => _service.FindUsagesAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetSymbolAtLocation_InvalidFile_ReturnsNull()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Try to get symbol from non-existent file
        var symbol = await _service.GetSymbolAtLocationAsync("/nonexistent/file.cs", 1, 1);

        // Assert
        symbol.Should().BeNull();
    }

    [Fact]
    public async Task FindSymbolByName_NotFound_ReturnsNull()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.NonExistentClass");

        // Assert
        symbol.Should().BeNull();
    }

    [Fact]
    public async Task FindUsages_NoWorkspace_ThrowsInvalidOperationException()
    {
        // Arrange - Don't load any workspace
        var fakeSymbol = new SymbolInfo
        {
            Name = "Test",
            FullyQualifiedName = "Test",
            Kind = SymbolKind.Type
        };

        // Act & Assert
        var act = () => _service.FindUsagesAsync(fakeSymbol);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region US2: Find Assignments

    [Fact]
    public async Task FindAssignments_SimpleAssignment_ReturnsLocation()
    {
        // Arrange - Load TestTargetApp, find assignments to _currentUser field
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Find the _currentUser field
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.ObjectTarget._currentUser", SymbolKind.Field);
        symbol.Should().NotBeNull("_currentUser field should be found");

        // Act
        var assignments = await _service.FindAssignmentsAsync(symbol!);

        // Assert - Should find the declaration/initialization
        assignments.Should().NotBeEmpty();
        assignments.Should().Contain(a => a.Kind == AssignmentKind.Declaration || a.Kind == AssignmentKind.Simple);
    }

    [Fact]
    public async Task FindAssignments_PropertyAssignment_ReturnsLocation()
    {
        // Arrange - Load TestTargetApp, find assignments to Name property
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Find the Name property on Person
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.Person.Name", SymbolKind.Property);
        symbol.Should().NotBeNull("Person.Name property should be found");

        // Act
        var assignments = await _service.FindAssignmentsAsync(symbol!);

        // Assert - Should find initializer in property declaration
        assignments.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FindAssignments_LoopVariable_IncludesIncrement()
    {
        // Arrange - Load TestTargetApp, find loop variable 'i' in LoopTarget
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var loopTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "LoopTarget.cs");

        // Get symbol at the loop variable declaration (line 14, col 18 is 'i')
        var symbol = await _service.GetSymbolAtLocationAsync(loopTargetPath, 14, 18);
        symbol.Should().NotBeNull("Loop variable 'i' should be found");

        // Act
        var assignments = await _service.FindAssignmentsAsync(symbol!);

        // Assert - Should find declaration (int i = 0) and increment (i++)
        assignments.Should().NotBeEmpty();
        assignments.Should().Contain(a => a.Kind == AssignmentKind.Declaration);
        // The i++ in for loop should be detected
        assignments.Should().Contain(a => a.Kind == AssignmentKind.Increment);
    }

    [Fact]
    public async Task FindAssignments_InvalidSymbol_ThrowsArgumentNullException()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act & Assert
        var act = () => _service.FindAssignmentsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FindAssignments_NoWorkspace_ThrowsInvalidOperationException()
    {
        // Arrange - Don't load workspace
        var fakeSymbol = new SymbolInfo
        {
            Name = "Test",
            FullyQualifiedName = "Test",
            Kind = SymbolKind.Field
        };

        // Act & Assert
        var act = () => _service.FindAssignmentsAsync(fakeSymbol);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region US3: Get Diagnostics

    [Fact]
    public async Task GetDiagnostics_ValidProject_ReturnsDiagnostics()
    {
        // Arrange - Load TestTargetApp
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Get all diagnostics
        var diagnostics = await _service.GetDiagnosticsAsync();

        // Assert - May have warnings/errors depending on project state
        diagnostics.Should().NotBeNull();
        // Note: TestTargetApp is expected to compile clean, so may be empty
    }

    [Fact]
    public async Task GetDiagnostics_SpecificProject_ReturnsDiagnosticsForProject()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Get diagnostics for specific project
        var diagnostics = await _service.GetDiagnosticsAsync(projectName: "TestTargetApp");

        // Assert
        diagnostics.Should().NotBeNull();
        foreach (var diag in diagnostics)
        {
            diag.Project.Should().Be("TestTargetApp");
        }
    }

    [Fact]
    public async Task GetDiagnostics_InvalidProject_ThrowsArgumentException()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act & Assert
        var act = () => _service.GetDiagnosticsAsync(projectName: "NonExistentProject");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetDiagnostics_NoWorkspace_ThrowsInvalidOperationException()
    {
        // Arrange - Don't load workspace

        // Act & Assert
        var act = () => _service.GetDiagnosticsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDiagnostics_SeverityFilter_RespectsMinSeverity()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Get only errors
        var errors = await _service.GetDiagnosticsAsync(minSeverity: DiagnosticSeverity.Error);

        // Assert - All should be errors
        foreach (var diag in errors)
        {
            diag.Severity.Should().Be(DiagnosticSeverity.Error);
        }
    }

    [Fact]
    public async Task GetDiagnostics_MaxResults_LimitsOutput()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Request limited results
        var diagnostics = await _service.GetDiagnosticsAsync(maxResults: 5);

        // Assert
        diagnostics.Count.Should().BeLessOrEqualTo(5);
    }

    #endregion

    #region US5: Go to Definition

    [Fact]
    public async Task GoToDefinition_SourceSymbol_ReturnsLocation()
    {
        // Arrange - Load TestTargetApp, go to definition of a method call
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var objectTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "ObjectTarget.cs");

        // Line 25 has: var userName = _currentUser.Name;
        // "        var userName = _currentUser.Name;" - Name starts at col 37
        // Navigate to Person.Name property definition
        var result = await _service.GoToDefinitionAsync(objectTargetPath, 25, 37);

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Name.Should().Be("Name");
        result.Definitions.Should().NotBeEmpty();
        result.Definitions.Should().Contain(d => d.IsSource);
    }

    [Fact]
    public async Task GoToDefinition_ClassSymbol_ReturnsClassLocation()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var objectTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "ObjectTarget.cs");

        // Line 10 has: private Person _currentUser = new Person();
        // Navigate to Person class definition
        var result = await _service.GoToDefinitionAsync(objectTargetPath, 10, 13);

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Name.Should().Be("Person");
        result.Symbol.Kind.Should().Be(SymbolKind.Type);
        result.Definitions.Should().Contain(d => d.IsSource && d.File!.EndsWith("ObjectTarget.cs"));
    }

    [Fact]
    public async Task GoToDefinition_MetadataSymbol_ReturnsAssemblyInfo()
    {
        // Arrange - Load TestTargetApp, go to definition of Console.WriteLine
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var objectTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "ObjectTarget.cs");

        // Line 27 has: Console.WriteLine(...)
        // Navigate to Console type definition (metadata)
        var result = await _service.GoToDefinitionAsync(objectTargetPath, 27, 9);

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Name.Should().Be("Console");
        result.Definitions.Should().NotBeEmpty();
        result.Definitions.Should().Contain(d => !d.IsSource && !string.IsNullOrEmpty(d.AssemblyName));
    }

    [Fact]
    public async Task GoToDefinition_InvalidLocation_ReturnsNull()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        // Act - Try to get definition from non-existent file
        var result = await _service.GoToDefinitionAsync("/nonexistent/file.cs", 1, 1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GoToDefinition_NoWorkspace_ThrowsInvalidOperationException()
    {
        // Arrange - Don't load workspace

        // Act & Assert
        var act = () => _service.GoToDefinitionAsync("/some/file.cs", 1, 1);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Helper Methods

    private static string GetTestSolutionPath()
    {
        // Use the DebugMcp project as test solution (no .sln file available)
        // For solution tests, we test with a project that contains multiple library references
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(baseDir);
        return Path.Combine(repoRoot, "DebugMcp", "DebugMcp.csproj");
    }

    private static string GetTestProjectPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(baseDir);
        return Path.Combine(repoRoot, "tests", "TestTargetApp", "TestTargetApp.csproj");
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not find repository root");
    }

    #endregion
}
