namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Severity of a compilation diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Hidden diagnostic (not visible to user).</summary>
    Hidden,

    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Warning that doesn't prevent compilation.</summary>
    Warning,

    /// <summary>Error that prevents compilation.</summary>
    Error
}
