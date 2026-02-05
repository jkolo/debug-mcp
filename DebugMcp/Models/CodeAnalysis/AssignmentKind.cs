namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Kind of assignment operation.
/// </summary>
public enum AssignmentKind
{
    /// <summary>Simple assignment (=).</summary>
    Simple,

    /// <summary>Compound assignment (+=, -=, *=, /=, etc.).</summary>
    Compound,

    /// <summary>Increment (++x or x++).</summary>
    Increment,

    /// <summary>Decrement (--x or x--).</summary>
    Decrement,

    /// <summary>Out parameter assignment.</summary>
    OutParameter,

    /// <summary>Ref parameter (potential assignment).</summary>
    RefParameter,

    /// <summary>Object/collection initializer.</summary>
    Initializer,

    /// <summary>Declaration with initialization.</summary>
    Declaration
}
