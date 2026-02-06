namespace DebugMcp.Services.Completions;

/// <summary>
/// Parsed context from a partial expression, used to determine what completions to provide.
/// </summary>
/// <param name="Kind">What type of completion is needed</param>
/// <param name="Prefix">Partial text to filter completions (e.g., "cust" for "customer")</param>
/// <param name="ObjectExpression">For Member kind: the expression before the dot (e.g., "user" in "user.Na")</param>
/// <param name="TypeName">For StaticMember/Namespace kind: the type name (e.g., "DateTime")</param>
public sealed record CompletionContext(
    CompletionKind Kind,
    string Prefix,
    string? ObjectExpression = null,
    string? TypeName = null);
