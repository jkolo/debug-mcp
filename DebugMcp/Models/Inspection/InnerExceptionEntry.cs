namespace DebugMcp.Models.Inspection;

/// <summary>
/// One level of the inner exception chain.
/// </summary>
/// <param name="Type">Full type name of the inner exception.</param>
/// <param name="Message">Inner exception message.</param>
/// <param name="Depth">Nesting depth (1 = first InnerException, 2 = InnerException.InnerException, ...).</param>
public sealed record InnerExceptionEntry(
    string Type,
    string Message,
    int Depth);
