using DebugMcp.Services.Resources;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.Resources;

/// <summary>
/// Tests for AllowedSourcePaths (T005).
/// Verifies PDB-referenced path tracking for source file security boundary.
/// </summary>
public class AllowedSourcePathsTests
{
    private readonly AllowedSourcePaths _paths = new();

    [Fact]
    public void IsAllowed_EmptySet_ReturnsFalse()
    {
        _paths.IsAllowed("/src/Program.cs").Should().BeFalse();
    }

    [Fact]
    public void AddModule_ThenIsAllowed_ReturnsTrue()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs", "/src/Utils.cs" });

        _paths.IsAllowed("/src/Program.cs").Should().BeTrue();
        _paths.IsAllowed("/src/Utils.cs").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_NonRegisteredPath_ReturnsFalse()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs" });

        _paths.IsAllowed("/etc/passwd").Should().BeFalse();
    }

    [Fact]
    public void RemoveModule_RemovesItsPaths()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs", "/src/Utils.cs" });
        _paths.AddModule("/lib/Other.dll", new[] { "/src/Other.cs" });

        _paths.RemoveModule("/lib/MyApp.dll");

        _paths.IsAllowed("/src/Program.cs").Should().BeFalse();
        _paths.IsAllowed("/src/Utils.cs").Should().BeFalse();
        _paths.IsAllowed("/src/Other.cs").Should().BeTrue();
    }

    [Fact]
    public void RemoveModule_NotFound_DoesNothing()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs" });

        _paths.RemoveModule("/lib/Unknown.dll");

        _paths.IsAllowed("/src/Program.cs").Should().BeTrue();
    }

    [Fact]
    public void PathNormalization_CaseInsensitive()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs" });

        // On Linux file paths are case-sensitive, but we normalize slashes
        _paths.IsAllowed("/src/Program.cs").Should().BeTrue();
    }

    [Fact]
    public void PathNormalization_BackslashToForwardSlash()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { @"C:\src\Program.cs" });

        _paths.IsAllowed("C:/src/Program.cs").Should().BeTrue();
    }

    [Fact]
    public void AddModule_DuplicatePaths_HandledGracefully()
    {
        // Same path from two modules
        _paths.AddModule("/lib/A.dll", new[] { "/src/Shared.cs" });
        _paths.AddModule("/lib/B.dll", new[] { "/src/Shared.cs" });

        _paths.IsAllowed("/src/Shared.cs").Should().BeTrue();

        // Remove one module — path still allowed because other module references it
        _paths.RemoveModule("/lib/A.dll");
        _paths.IsAllowed("/src/Shared.cs").Should().BeTrue();

        // Remove second module — path no longer allowed
        _paths.RemoveModule("/lib/B.dll");
        _paths.IsAllowed("/src/Shared.cs").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllPaths()
    {
        _paths.AddModule("/lib/MyApp.dll", new[] { "/src/Program.cs" });
        _paths.AddModule("/lib/Other.dll", new[] { "/src/Other.cs" });

        _paths.Clear();

        _paths.IsAllowed("/src/Program.cs").Should().BeFalse();
        _paths.IsAllowed("/src/Other.cs").Should().BeFalse();
    }

    [Fact]
    public void AddModule_EmptyPaths_DoesNothing()
    {
        _paths.AddModule("/lib/MyApp.dll", Array.Empty<string>());

        // Should not throw, should not add any paths
        _paths.IsAllowed("/src/Program.cs").Should().BeFalse();
    }
}
