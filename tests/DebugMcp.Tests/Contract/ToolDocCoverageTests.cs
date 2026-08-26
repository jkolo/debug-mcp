using System.Text.RegularExpressions;
using AwesomeAssertions;
using DebugMcp.Tests.Support;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// contracts/tool-result-contract.md build-enforcement conditions 3 and 4: every tool must be
/// named somewhere in <c>website/docs/tools/*.md</c>, and no documented tool may be stale. Docs
/// are thematic prose grouped by area, each tool introduced with a <c>### tool_name</c> heading —
/// matched by name only, per the contract.
/// </summary>
public partial class ToolDocCoverageTests
{
    [GeneratedRegex(@"^###\s+([a-z][a-z0-9_]*)\s*$", RegexOptions.Multiline)]
    private static partial Regex ToolHeadingPattern();

    [Fact]
    public void EveryTool_IsDocumented_AndNoDocumentedToolIsStale()
    {
        var docsDir = FindDocsToolsDirectory();
        var documented = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(docsDir, "*.md")
                     .Where(f => !Path.GetFileName(f).Equals("index.md", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (System.Text.RegularExpressions.Match m in ToolHeadingPattern().Matches(File.ReadAllText(file)))
            {
                documented.Add(m.Groups[1].Value);
            }
        }

        var registered = McpToolDiscovery.GetAllToolMethods().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var undocumented = registered.Except(documented).OrderBy(n => n).ToList();
        var stale = documented.Except(registered).OrderBy(n => n).ToList();

        undocumented.Should().BeEmpty(because: "every registered tool must be named in website/docs/tools/*.md");
        stale.Should().BeEmpty(because: "no documented tool may name a tool that no longer exists");
    }

    private static string FindDocsToolsDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "website", "docs", "tools");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("Could not locate website/docs/tools from the test output directory.");
    }
}
