using DebugMcp.Models.Modules;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// Result of resolving symbols for a module.
/// </summary>
public sealed record SymbolResolutionResult(
    SymbolStatus Status,
    string? PdbPath,
    SymbolSource Source,
    string? FailureReason);
