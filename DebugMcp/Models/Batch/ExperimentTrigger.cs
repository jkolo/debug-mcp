namespace DebugMcp.Models.Batch;

/// <summary>Discriminated union: source location or exception type.</summary>
public abstract record ExperimentTrigger
{
    /// <summary>Trigger at a specific source file and line number.</summary>
    public sealed record SourceLocation(string File, int Line) : ExperimentTrigger;

    /// <summary>Trigger when an exception of the given type is thrown.</summary>
    public sealed record ExceptionType(string TypeName) : ExperimentTrigger;
}
