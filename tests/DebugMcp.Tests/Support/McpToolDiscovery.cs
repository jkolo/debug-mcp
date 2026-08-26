using System.Reflection;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Enumerates every registered MCP tool method via reflection, for contract tests that must
/// hold across all 39 tools (async signatures, output schemas, error shape, timeout parameters,
/// doc coverage). Shared so each contract test discovers tools the same way
/// <see cref="DebugMcp.Tests.Contract.ToolAnnotationTests"/> already did for annotations.
/// </summary>
public static class McpToolDiscovery
{
    /// <summary>One discovered <c>[McpServerTool]</c>-attributed method and its declaring type.</summary>
    public sealed record DiscoveredToolMethod(Type ToolType, MethodInfo Method, McpServerToolAttribute Attribute)
    {
        public string Name => Attribute.Name!;
    }

    /// <summary>All 39 tool methods in the DebugMcp assembly.</summary>
    public static IReadOnlyList<DiscoveredToolMethod> GetAllToolMethods()
    {
        var toolAssembly = typeof(DebugMcp.Tools.DebugLaunchTool).Assembly;

        return toolAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => (Type: t, Method: m)))
            .Where(x => x.Method.GetCustomAttribute<McpServerToolAttribute>() != null)
            .Select(x => new DiscoveredToolMethod(
                x.Type, x.Method, x.Method.GetCustomAttribute<McpServerToolAttribute>()!))
            .ToList();
    }
}
