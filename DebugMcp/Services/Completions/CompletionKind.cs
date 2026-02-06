namespace DebugMcp.Services.Completions;

/// <summary>
/// Categorizes the type of completion being requested based on expression context.
/// </summary>
public enum CompletionKind
{
    /// <summary>Variable names in current scope (locals, parameters, this)</summary>
    Variable,

    /// <summary>Instance members of an object (properties, fields, methods)</summary>
    Member,

    /// <summary>Static members of a type (e.g., DateTime.Now, Math.PI)</summary>
    StaticMember,

    /// <summary>Types or namespaces (e.g., System.Collections.)</summary>
    Namespace
}
