using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Contract;

public class SnapshotToolContractTests
{
    private static MethodInfo GetToolMethod(Type toolType, string toolName)
    {
        var methods = toolType.GetMethods()
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == toolName)
            .ToList();

        methods.Should().ContainSingle($"tool '{toolName}' should have exactly one method");
        return methods[0];
    }

    [Fact]
    public void SnapshotCreate_HasCorrectAnnotations()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotCreateTool), "snapshot_create");
        var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;

        attr.Title.Should().Be("Create State Snapshot");
        attr.ReadOnly.Should().BeFalse();
        attr.Destructive.Should().BeFalse();
        attr.Idempotent.Should().BeFalse();
    }

    [Fact]
    public void SnapshotCreate_HasOptionalParameters()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotCreateTool), "snapshot_create");
        var parameters = method.GetParameters();

        parameters.Should().Contain(p => p.Name == "label" && p.HasDefaultValue);
        parameters.Should().Contain(p => p.Name == "thread_id" && p.HasDefaultValue);
        parameters.Should().Contain(p => p.Name == "frame_index" && p.HasDefaultValue);
        parameters.Should().Contain(p => p.Name == "depth" && p.HasDefaultValue);
    }

    [Fact]
    public void SnapshotDiff_HasCorrectAnnotations()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotDiffTool), "snapshot_diff");
        var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;

        attr.Title.Should().Be("Compare Two Snapshots");
        attr.ReadOnly.Should().BeTrue();
        attr.Destructive.Should().BeFalse();
        attr.Idempotent.Should().BeTrue();
    }

    [Fact]
    public void SnapshotDiff_HasRequiredParameters()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotDiffTool), "snapshot_diff");
        var parameters = method.GetParameters();

        parameters.Should().Contain(p => p.Name == "snapshot_id_1" && !p.HasDefaultValue);
        parameters.Should().Contain(p => p.Name == "snapshot_id_2" && !p.HasDefaultValue);
    }

    [Fact]
    public void SnapshotList_HasCorrectAnnotations()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotListTool), "snapshot_list");
        var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;

        attr.Title.Should().Be("List Snapshots");
        attr.ReadOnly.Should().BeTrue();
        attr.Destructive.Should().BeFalse();
        attr.Idempotent.Should().BeTrue();
    }

    [Fact]
    public void SnapshotDelete_HasCorrectAnnotations()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotDeleteTool), "snapshot_delete");
        var attr = method.GetCustomAttribute<McpServerToolAttribute>()!;

        attr.Title.Should().Be("Delete Snapshot(s)");
        attr.ReadOnly.Should().BeFalse();
        attr.Destructive.Should().BeTrue();
    }

    [Fact]
    public void SnapshotDelete_HasOptionalSnapshotId()
    {
        var method = GetToolMethod(typeof(DebugMcp.Tools.SnapshotDeleteTool), "snapshot_delete");
        var parameters = method.GetParameters();

        parameters.Should().Contain(p => p.Name == "snapshot_id" && p.HasDefaultValue);
    }

    [Fact]
    public void AllSnapshotTools_HaveDescriptionAttributes()
    {
        var toolTypes = new[]
        {
            typeof(DebugMcp.Tools.SnapshotCreateTool),
            typeof(DebugMcp.Tools.SnapshotDiffTool),
            typeof(DebugMcp.Tools.SnapshotListTool),
            typeof(DebugMcp.Tools.SnapshotDeleteTool)
        };

        foreach (var toolType in toolTypes)
        {
            toolType.GetCustomAttribute<McpServerToolTypeAttribute>().Should().NotBeNull(
                $"{toolType.Name} should have [McpServerToolType]");

            var toolMethods = toolType.GetMethods()
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in toolMethods)
            {
                method.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull(
                    $"{toolType.Name}.{method.Name} should have [Description]");
            }
        }
    }
}
