namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Kind of symbol usage.
/// </summary>
public enum UsageKind
{
    /// <summary>Symbol value is read.</summary>
    Read,

    /// <summary>Symbol value is written.</summary>
    Write,

    /// <summary>Symbol is declared here.</summary>
    Declaration,

    /// <summary>Type/method reference (not read/write).</summary>
    Reference
}
