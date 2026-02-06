namespace DebugMcp.Models.Modules;

/// <summary>
/// Where a PDB was resolved from.
/// </summary>
public enum SymbolSource
{
    /// <summary>Not resolved.</summary>
    None,

    /// <summary>Found next to assembly on disk.</summary>
    Local,

    /// <summary>Extracted from PE debug directory.</summary>
    Embedded,

    /// <summary>Found in persistent symbol cache.</summary>
    Cache,

    /// <summary>Downloaded from a symbol server.</summary>
    Server
}
