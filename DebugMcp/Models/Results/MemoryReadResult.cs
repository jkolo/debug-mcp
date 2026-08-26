using DebugMcp.Models.Memory;

namespace DebugMcp.Models.Results;

/// <summary>
/// Wire shape for <c>memory_read</c>. Field names preserved from the pre-US3 hand-rolled JSON
/// (FR-021). <see cref="Memory"/> reuses <see cref="MemoryRegion"/> as-is — its
/// <c>[JsonPropertyName]</c>/<c>[JsonIgnore(WhenWritingNull)]</c> attributes already reproduce
/// the legacy nested <c>memory</c> object exactly (address, requestedSize, actualSize, bytes,
/// optional ascii, optional error).
/// </summary>
public sealed record MemoryReadResult(
    bool Success,
    MemoryRegion? Memory = null,
    ToolError? Error = null);
