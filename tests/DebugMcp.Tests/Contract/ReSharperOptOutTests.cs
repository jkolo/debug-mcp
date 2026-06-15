using System.Reflection;
using DebugMcp.Services.ReSharper;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// Verifies the opt-out registration filter (Program.cs) excludes the ReSharper tool classes
/// as a group when the integration is disabled, while leaving other tools (e.g. Code*) intact.
/// Mirrors the production filter expression:
///   .Where(t => resharperOptions.Enabled || !t.Name.StartsWith("ReSharper", StringComparison.Ordinal))
/// </summary>
public sealed class ReSharperOptOutTests
{
    private static IEnumerable<Type> AllToolTypes() =>
        typeof(DebugMcp.Tools.DebugLaunchTool).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

    private static List<Type> ApplyFilter(bool enabled) =>
        AllToolTypes()
            .Where(t => enabled || !t.Name.StartsWith("ReSharper", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void Enabled_IncludesBothReSharperTools()
    {
        var names = ApplyFilter(enabled: true).Select(t => t.Name).ToList();
        names.Should().Contain("ReSharperInspectSolutionTool");
        names.Should().Contain("ReSharperInspectProjectTool");
    }

    [Fact]
    public void Disabled_ExcludesAllReSharperTools_ButKeepsOthers()
    {
        var filtered = ApplyFilter(enabled: false);
        filtered.Should().NotContain(t => t.Name.StartsWith("ReSharper", StringComparison.Ordinal));
        // Other tool groups remain available.
        filtered.Select(t => t.Name).Should().Contain("CodeGetDiagnosticsTool");
        filtered.Select(t => t.Name).Should().Contain("DebugLaunchTool");
    }

    [Fact]
    public void OptionsDisabled_FromFlag_DrivesTheFilter()
    {
        var disabled = ReSharperOptions.Create(noResharper: true);
        ApplyFilter(disabled.Enabled).Should().NotContain(t => t.Name.StartsWith("ReSharper", StringComparison.Ordinal));
    }
}
