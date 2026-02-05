using System.Diagnostics;
using DebugMcp.Models.CodeAnalysis;
using DebugMcp.Services.CodeAnalysis;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DebugMcp.Tests.Unit.CodeAnalysis;

/// <summary>
/// Performance tests for CodeAnalysisService.
/// Validates SC-001 (<2s symbol search) and SC-002 (<30s solution load).
/// </summary>
public class CodeAnalysisPerformanceTests : IDisposable
{
    private readonly CodeAnalysisService _service;
    private readonly ITestOutputHelper _output;

    public CodeAnalysisPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _service = new CodeAnalysisService();
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    /// <summary>
    /// SC-002: Solution/project load should complete within 30 seconds.
    /// </summary>
    [Fact]
    public async Task LoadProject_CompletesWithin30Seconds()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _service.LoadAsync(projectPath);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Project load time: {stopwatch.ElapsedMilliseconds}ms");

        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "SC-002: Project load should complete within 30 seconds");
    }

    /// <summary>
    /// SC-001: Symbol search should complete within 2 seconds.
    /// </summary>
    [Fact]
    public async Task FindSymbolByName_CompletesWithin2Seconds()
    {
        // Arrange - Load project first
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.Person", SymbolKind.Type);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Symbol search time: {stopwatch.ElapsedMilliseconds}ms");

        symbol.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "SC-001: Symbol search should complete within 2 seconds");
    }

    /// <summary>
    /// SC-001: Find usages should complete within 2 seconds.
    /// </summary>
    [Fact]
    public async Task FindUsages_CompletesWithin2Seconds()
    {
        // Arrange - Load project first
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.Person", SymbolKind.Type);
        symbol.Should().NotBeNull();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var usages = await _service.FindUsagesAsync(symbol!);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Find usages time: {stopwatch.ElapsedMilliseconds}ms");

        usages.Should().NotBeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "SC-001: Find usages should complete within 2 seconds");
    }

    /// <summary>
    /// SC-001: Find assignments should complete within 2 seconds.
    /// </summary>
    [Fact]
    public async Task FindAssignments_CompletesWithin2Seconds()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var symbol = await _service.FindSymbolByNameAsync("TestTargetApp.ObjectTarget._currentUser", SymbolKind.Field);
        symbol.Should().NotBeNull();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var assignments = await _service.FindAssignmentsAsync(symbol!);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Find assignments time: {stopwatch.ElapsedMilliseconds}ms");

        assignments.Should().NotBeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "SC-001: Find assignments should complete within 2 seconds");
    }

    /// <summary>
    /// SC-001: Go to definition should complete within 2 seconds.
    /// Note: First call includes semantic model construction (cold start).
    /// Subsequent calls (warm) should be much faster.
    /// </summary>
    [Fact]
    public async Task GoToDefinition_CompletesWithin2Seconds()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var objectTargetPath = Path.Combine(repoRoot, "tests", "TestTargetApp", "ObjectTarget.cs");

        // Warm-up: First call builds the semantic model (expensive)
        var warmupStopwatch = Stopwatch.StartNew();
        await _service.GoToDefinitionAsync(objectTargetPath, 25, 37);
        warmupStopwatch.Stop();
        _output.WriteLine($"Go to definition (cold): {warmupStopwatch.ElapsedMilliseconds}ms");

        // Measure: Second call uses cached semantic model
        var stopwatch = Stopwatch.StartNew();
        var result = await _service.GoToDefinitionAsync(objectTargetPath, 26, 24);
        stopwatch.Stop();
        _output.WriteLine($"Go to definition (warm): {stopwatch.ElapsedMilliseconds}ms");

        // Assert - warm call should be fast
        result.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "SC-001: Go to definition should complete within 2 seconds (warm)");

        // Cold start can be up to 5s for first semantic model build
        warmupStopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "Cold start should complete within 5 seconds");
    }

    /// <summary>
    /// SC-001: Get diagnostics should complete within 2 seconds.
    /// </summary>
    [Fact]
    public async Task GetDiagnostics_CompletesWithin2Seconds()
    {
        // Arrange
        var projectPath = GetTestProjectPath();
        await _service.LoadAsync(projectPath);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var diagnostics = await _service.GetDiagnosticsAsync();

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Get diagnostics time: {stopwatch.ElapsedMilliseconds}ms");

        diagnostics.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "SC-001: Get diagnostics should complete within 2 seconds");
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
}
