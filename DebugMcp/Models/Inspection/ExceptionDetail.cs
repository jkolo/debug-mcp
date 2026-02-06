namespace DebugMcp.Models.Inspection;

/// <summary>
/// Core exception information from the current exception.
/// </summary>
/// <param name="Type">Full exception type name (e.g., "System.NullReferenceException").</param>
/// <param name="Message">Exception message.</param>
/// <param name="IsFirstChance">True if first-chance exception, false if unhandled.</param>
/// <param name="StackTraceString">Runtime's StackTrace property value (may be null if unavailable).</param>
public sealed record ExceptionDetail(
    string Type,
    string Message,
    bool IsFirstChance,
    string? StackTraceString = null);
