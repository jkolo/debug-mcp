namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Kind of code symbol.
/// </summary>
public enum SymbolKind
{
    /// <summary>Namespace declaration.</summary>
    Namespace,

    /// <summary>Type (class, struct, interface, enum, delegate).</summary>
    Type,

    /// <summary>Method or function.</summary>
    Method,

    /// <summary>Property accessor.</summary>
    Property,

    /// <summary>Field member.</summary>
    Field,

    /// <summary>Event member.</summary>
    Event,

    /// <summary>Local variable.</summary>
    Local,

    /// <summary>Method parameter.</summary>
    Parameter,

    /// <summary>Type parameter (generic).</summary>
    TypeParameter
}
