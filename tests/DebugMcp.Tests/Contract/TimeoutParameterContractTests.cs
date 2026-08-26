using System.ComponentModel;
using System.Reflection;
using DebugMcp.Services.Timeouts;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-031, SC-011: every tool classified as blocking in <see cref="TimeoutPolicy"/> exposes its
/// documented optional timeout parameter; every tool classified as in-memory-only does not.
/// </summary>
public sealed class TimeoutParameterContractTests
{
    private static List<(string Name, MethodInfo Method)> DiscoverAllTools()
    {
        var toolAssembly = typeof(DebugMcp.Tools.DebugLaunchTool).Assembly;

        return toolAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .Select(m => (m.GetCustomAttribute<McpServerToolAttribute>()!.Name!, m))
            .ToList();
    }

    public static IEnumerable<object[]> BlockingToolData() =>
        TimeoutPolicy.Specs.Where(kv => kv.Value.IsBlocking).Select(kv => new object[] { kv.Key });

    public static IEnumerable<object[]> InMemoryToolData() =>
        TimeoutPolicy.Specs.Where(kv => !kv.Value.IsBlocking).Select(kv => new object[] { kv.Key });

    [Theory]
    [MemberData(nameof(BlockingToolData))]
    public void BlockingTool_ExposesItsDocumentedTimeoutParameter(string toolName)
    {
        var spec = TimeoutPolicy.Specs[toolName];
        var tool = DiscoverAllTools().SingleOrDefault(t => t.Name == toolName);
        tool.Method.Should().NotBeNull(because: $"'{toolName}' must be a registered tool");

        var parameter = tool.Method.GetParameters().SingleOrDefault(p => p.Name == spec.ParameterName);
        parameter.Should().NotBeNull(because: $"'{toolName}' is blocking (FR-031) and must expose an optional '{spec.ParameterName}' parameter");
        parameter!.HasDefaultValue.Should().BeTrue(because: "the default must be documented and applied when omitted, not required");

        // Nullable params defaulting to null (e.g. the ReSharper tools, which defer to
        // ReSharperOptions.InspectionTimeoutSeconds when omitted) express their real default
        // through the [Description] text below rather than as a literal C# default value.
        if (parameter.DefaultValue is not null)
        {
            Convert.ToInt32(parameter.DefaultValue).Should().Be(spec.DefaultValue,
                because: "FR-032: 30s for ordinary tools, the tool's own longer/shorter pre-existing documented default otherwise");
        }

        var description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
        description.Should().ContainEquivalentOf("default",
            because: "SC-011 requires the default to be documented in the parameter's own description, not just in code");
    }

    [Theory]
    [MemberData(nameof(InMemoryToolData))]
    public void InMemoryTool_HasNoTimeoutParameter(string toolName)
    {
        var tool = DiscoverAllTools().SingleOrDefault(t => t.Name == toolName);
        tool.Method.Should().NotBeNull(because: $"'{toolName}' must be a registered tool");

        tool.Method.GetParameters().Should().NotContain(
            p => p.Name != null && p.Name.Contains("timeout", StringComparison.OrdinalIgnoreCase),
            because: $"'{toolName}' only reads in-memory server state (FR-031) and MUST NOT gain a timeout parameter");
    }

    [Fact]
    public void EveryRegisteredTool_HasATimeoutPolicyEntry()
    {
        var registeredNames = DiscoverAllTools().Select(t => t.Name).ToHashSet();
        var policyNames = TimeoutPolicy.Specs.Keys.ToHashSet();

        registeredNames.Except(policyNames).Should().BeEmpty(because: "every registered tool must be classified in TimeoutPolicy");
        policyNames.Except(registeredNames).Should().BeEmpty(because: "TimeoutPolicy must not classify a tool that no longer exists");
    }
}
