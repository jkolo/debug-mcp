namespace DebugMcp.Models.ReSharper;

/// <summary>
/// Native ReSharper inspection severity, exposed verbatim (no remap to Roslyn's scale).
/// Ordered by descending importance so a minimum-severity threshold filter is a simple
/// comparison: Error (highest) &gt; Warning &gt; Suggestion &gt; Hint (lowest).
/// </summary>
public enum ReSharperSeverity
{
    /// <summary>Lowest — ReSharper HINT.</summary>
    Hint = 0,

    /// <summary>ReSharper SUGGESTION.</summary>
    Suggestion = 1,

    /// <summary>ReSharper WARNING.</summary>
    Warning = 2,

    /// <summary>Highest — ReSharper ERROR.</summary>
    Error = 3
}
