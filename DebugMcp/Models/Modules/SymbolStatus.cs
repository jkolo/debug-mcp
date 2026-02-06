namespace DebugMcp.Models.Modules;

/// <summary>
/// Per-module symbol resolution state.
/// </summary>
public enum SymbolStatus
{
    /// <summary>No symbol resolution attempted (dynamic/in-memory modules).</summary>
    None,

    /// <summary>Symbols successfully loaded (local, embedded, cached, or downloaded).</summary>
    Loaded,

    /// <summary>Queued for download from symbol server.</summary>
    PendingDownload,

    /// <summary>Active HTTP download in progress.</summary>
    Downloading,

    /// <summary>PDB not available from any source (all sources checked).</summary>
    NotFound,

    /// <summary>Download or validation error (network timeout, checksum mismatch, etc.).</summary>
    Failed
}
