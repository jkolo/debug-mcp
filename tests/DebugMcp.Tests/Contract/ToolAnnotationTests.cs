using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// Annotation verification tests ensuring every tool's [McpServerTool] metadata
/// matches the spec classification table (spec.md §Tool Annotation Classification Table).
/// </summary>
public class ToolAnnotationTests
{
    /// <summary>
    /// Expected annotation values for all 40 tools, matching the spec classification table.
    /// </summary>
    private static readonly Dictionary<string, ExpectedAnnotation> ExpectedAnnotations = new()
    {
        // Read-Only Tools (21): ReadOnly=true, Destructive=false
        ["breakpoint_list"] = new("List Breakpoints", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["breakpoint_wait"] = new("Wait for Breakpoint Hit", ReadOnly: true, Destructive: false, Idempotent: false, OpenWorld: false),
        ["debug_state"] = new("Get Debug State", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["evaluate"] = new("Evaluate Expression", ReadOnly: true, Destructive: false, Idempotent: false, OpenWorld: false),
        ["object_inspect"] = new("Inspect Object", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["variables_get"] = new("Get Variables", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["stacktrace_get"] = new("Get Stack Trace", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["threads_list"] = new("List Threads", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["modules_list"] = new("List Modules", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["modules_search"] = new("Search Modules", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["types_get"] = new("Get Types", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["members_get"] = new("Get Type Members", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["layout_get"] = new("Get Memory Layout", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["memory_read"] = new("Read Memory", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["references_get"] = new("Get Object References", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["exception_get_context"] = new("Get Exception Context", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["process_read_output"] = new("Read Process Output", ReadOnly: true, Destructive: false, Idempotent: false, OpenWorld: false),
        ["code_goto_definition"] = new("Go to Definition", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["code_find_usages"] = new("Find Usages", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["code_find_assignments"] = new("Find Assignments", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["code_get_diagnostics"] = new("Get Diagnostics", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["collection_analyze"] = new("Analyze Collection", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["object_summarize"] = new("Summarize Object", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),

        // State-Modifying Non-Destructive Tools (8): ReadOnly=false, Destructive=false
        ["breakpoint_set"] = new("Set Breakpoint", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["breakpoint_enable"] = new("Enable/Disable Breakpoint", ReadOnly: false, Destructive: false, Idempotent: true, OpenWorld: false),
        ["breakpoint_set_exception"] = new("Set Exception Breakpoint", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["tracepoint_set"] = new("Set Tracepoint", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["debug_continue"] = new("Continue Execution", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["debug_step"] = new("Step Through Code", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["debug_pause"] = new("Pause Execution", ReadOnly: false, Destructive: false, Idempotent: true, OpenWorld: false),
        ["code_load"] = new("Load Workspace", ReadOnly: false, Destructive: false, Idempotent: true, OpenWorld: false),

        // Destructive Tools (5): ReadOnly=false, Destructive=true
        ["debug_launch"] = new("Launch Process", ReadOnly: false, Destructive: true, Idempotent: false, OpenWorld: false),
        ["debug_attach"] = new("Attach to Process", ReadOnly: false, Destructive: true, Idempotent: false, OpenWorld: false),
        ["debug_disconnect"] = new("Disconnect Debug Session", ReadOnly: false, Destructive: true, Idempotent: true, OpenWorld: false),
        ["breakpoint_remove"] = new("Remove Breakpoint", ReadOnly: false, Destructive: true, Idempotent: false, OpenWorld: false),
        ["process_write_input"] = new("Write Process Input", ReadOnly: false, Destructive: true, Idempotent: false, OpenWorld: false),

        // Snapshot Tools (4)
        ["snapshot_create"] = new("Create State Snapshot", ReadOnly: false, Destructive: false, Idempotent: false, OpenWorld: false),
        ["snapshot_diff"] = new("Compare Two Snapshots", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["snapshot_list"] = new("List Snapshots", ReadOnly: true, Destructive: false, Idempotent: true, OpenWorld: false),
        ["snapshot_delete"] = new("Delete Snapshot(s)", ReadOnly: false, Destructive: true, Idempotent: false, OpenWorld: false),
    };

    /// <summary>
    /// The 10 tools that must have enhanced descriptions with JSON response examples.
    /// </summary>
    private static readonly HashSet<string> EnhancedDescriptionTools =
    [
        "debug_launch", "breakpoint_set", "breakpoint_wait", "debug_continue", "debug_step",
        "variables_get", "evaluate", "stacktrace_get", "exception_get_context", "debug_disconnect"
    ];

    /// <summary>
    /// Discovers all tool methods in the DebugMcp assembly via reflection.
    /// Returns (toolName, McpServerToolAttribute, DescriptionAttribute) for each tool.
    /// </summary>
    private static List<DiscoveredTool> DiscoverAllTools()
    {
        var toolAssembly = typeof(DebugMcp.Tools.DebugLaunchTool).Assembly;

        return toolAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .Select(m =>
            {
                var attr = m.GetCustomAttribute<McpServerToolAttribute>()!;
                var desc = m.GetCustomAttribute<DescriptionAttribute>();
                return new DiscoveredTool(attr.Name!, attr, desc);
            })
            .ToList();
    }

    public static IEnumerable<object[]> GetAllToolData()
    {
        return DiscoverAllTools().Select(t => new object[] { t.Name, t.Attribute });
    }

    public static IEnumerable<object[]> GetEnhancedToolData()
    {
        return DiscoverAllTools()
            .Where(t => EnhancedDescriptionTools.Contains(t.Name))
            .Select(t => new object[] { t.Name, t.Description?.Description ?? "" });
    }

    // ── Per-tool annotation assertions (FR-010, FR-012) ────────────────────

    [Theory]
    [MemberData(nameof(GetAllToolData))]
    public void Tool_Title_MatchesSpec(string toolName, McpServerToolAttribute attr)
    {
        ExpectedAnnotations.Should().ContainKey(toolName,
            $"Tool '{toolName}' is registered but has no expected annotation entry");

        var expected = ExpectedAnnotations[toolName];
        attr.Title.Should().Be(expected.Title,
            $"Tool '{toolName}': expected Title='{expected.Title}', got Title='{attr.Title}'");
    }

    [Theory]
    [MemberData(nameof(GetAllToolData))]
    public void Tool_ReadOnly_MatchesSpec(string toolName, McpServerToolAttribute attr)
    {
        var expected = ExpectedAnnotations[toolName];
        attr.ReadOnly.Should().Be(expected.ReadOnly,
            $"Tool '{toolName}': expected ReadOnly={expected.ReadOnly}, got ReadOnly={attr.ReadOnly}");
    }

    [Theory]
    [MemberData(nameof(GetAllToolData))]
    public void Tool_Destructive_MatchesSpec(string toolName, McpServerToolAttribute attr)
    {
        var expected = ExpectedAnnotations[toolName];
        attr.Destructive.Should().Be(expected.Destructive,
            $"Tool '{toolName}': expected Destructive={expected.Destructive}, got Destructive={attr.Destructive}");
    }

    [Theory]
    [MemberData(nameof(GetAllToolData))]
    public void Tool_Idempotent_MatchesSpec(string toolName, McpServerToolAttribute attr)
    {
        var expected = ExpectedAnnotations[toolName];
        attr.Idempotent.Should().Be(expected.Idempotent,
            $"Tool '{toolName}': expected Idempotent={expected.Idempotent}, got Idempotent={attr.Idempotent}");
    }

    [Theory]
    [MemberData(nameof(GetAllToolData))]
    public void Tool_OpenWorld_MatchesSpec(string toolName, McpServerToolAttribute attr)
    {
        var expected = ExpectedAnnotations[toolName];
        attr.OpenWorld.Should().Be(expected.OpenWorld,
            $"Tool '{toolName}': expected OpenWorld={expected.OpenWorld}, got OpenWorld={attr.OpenWorld}");
    }

    // ── Coverage check (FR-011) ─────────────────────────────────────────────

    [Fact]
    public void AllRegisteredTools_HaveExpectedAnnotationEntries()
    {
        var discoveredTools = DiscoverAllTools().Select(t => t.Name).Order().ToList();
        var expectedTools = ExpectedAnnotations.Keys.Order().ToList();

        discoveredTools.Should().BeEquivalentTo(expectedTools,
            "Every registered tool must have a corresponding annotation test entry, " +
            "and every test entry must correspond to a registered tool. " +
            $"Discovered: [{string.Join(", ", discoveredTools)}], " +
            $"Expected: [{string.Join(", ", expectedTools)}]");
    }

    [Fact]
    public void ExpectedAnnotations_Covers34Tools()
    {
        ExpectedAnnotations.Should().HaveCount(40,
            "The spec defines exactly 40 tools (23 read-only + 2 collection/object + 9 state-modifying + 6 destructive)");
    }

    // ── Description content tests for 10 enhanced tools (FR-008, FR-009) ──

    [Theory]
    [MemberData(nameof(GetEnhancedToolData))]
    public void EnhancedTool_Description_ContainsResponseExample(string toolName, string description)
    {
        description.Should().NotBeNullOrEmpty(
            $"Tool '{toolName}' is designated for enhanced description but has no [Description] attribute");

        description.Should().Contain("\"success\"",
            $"Tool '{toolName}': enhanced description must contain a JSON response example with a \"success\" field");
    }

    [Theory]
    [MemberData(nameof(GetEnhancedToolData))]
    public void EnhancedTool_Description_IsSubstantial(string toolName, string description)
    {
        // Enhanced descriptions must be at least 2 sentences (contain at least one period followed by content)
        description.Length.Should().BeGreaterThan(100,
            $"Tool '{toolName}': enhanced description should be substantial (>100 chars), got {description.Length} chars");
    }

    // ── Helper types ────────────────────────────────────────────────────────

    private record ExpectedAnnotation(
        string Title,
        bool ReadOnly,
        bool Destructive,
        bool Idempotent,
        bool OpenWorld);

    private record DiscoveredTool(
        string Name,
        McpServerToolAttribute Attribute,
        DescriptionAttribute? Description);
}
