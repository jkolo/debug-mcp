using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DebugMcp.Models.Results;

/// <summary>
/// Sets the protocol-level <c>isError</c> flag from a migrated tool's own <c>success</c> field,
/// centrally, so no individual tool sets it (FR-018, T053). Registered once via
/// <c>IMcpServerBuilder.WithRequestFilters</c> in <c>Program.cs</c>. Verified against SDK 2.2.0
/// (data-model.md §1) that <c>UseStructuredContent</c> alone does not set <c>isError</c>, and
/// that this filter composes correctly with MCP Tasks deferral — a deferred call's stored task
/// result carries the same <c>isError</c> the synchronous path would have.
/// </summary>
public static class ToolResultSerializer
{
    public static McpRequestFilter<CallToolRequestParams, CallToolResult> IsErrorFilter { get; } =
        next => async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);
            if (result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
                && structured.TryGetProperty("success", out var successProperty)
                && successProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result.IsError = !successProperty.GetBoolean();
            }
            return result;
        };
}
