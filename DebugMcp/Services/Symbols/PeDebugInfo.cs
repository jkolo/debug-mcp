using System.Collections.Immutable;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// Debug information extracted from a PE assembly's debug directory.
/// Contains everything needed to locate and validate a PDB file.
/// </summary>
public sealed record PeDebugInfo(
    string PdbFileName,
    Guid PdbGuid,
    int Age,
    uint Stamp,
    bool IsPortablePdb,
    string SymbolServerKey,
    string? ChecksumAlgorithm,
    ImmutableArray<byte> Checksum,
    bool HasEmbeddedPdb);
