namespace DebugMcp.Services.Inspection;

/// <summary>
/// Names and documented constant weights for every rule <see cref="SuspicionRanker"/> runs
/// (FR-027). Each is individually tested in
/// <c>tests/DebugMcp.Tests/Unit/Enrichment/Heuristics/</c> and described in
/// <c>docs/enrichment-heuristics.md</c>.
/// </summary>
public static class SuspicionHeuristics
{
    /// <summary>An argument or local has a "null" value. Strong, direct evidence for reference-type faults.</summary>
    public const string NullValuedLocal = "NullValuedLocal";
    public const double NullValuedLocalWeight = 0.5;

    /// <summary>The frame has no symbols (no source location) — demoted, since nothing about it is actionable.</summary>
    public const string ExternalFrameNoSymbols = "ExternalFrameNoSymbols";
    public const double ExternalFrameNoSymbolsWeight = -1.0;

    /// <summary>The lowest-index frame that has symbols — a mild bonus reflecting that the innermost user code is usually most actionable.</summary>
    public const string InnermostUserFrame = "InnermostUserFrame";
    public const double InnermostUserFrameWeight = 0.2;

    /// <summary>The exception message names this variable by identifier — direct textual corroboration.</summary>
    public const string ExceptionMessageReferencesVariable = "ExceptionMessageReferencesVariable";
    public const double ExceptionMessageReferencesVariableWeight = 0.4;

    /// <summary>A collection-shaped argument/local reports zero children — a common precondition for "sequence contains no elements" style faults.</summary>
    public const string EmptyCollectionArgument = "EmptyCollectionArgument";
    public const double EmptyCollectionArgumentWeight = 0.5;
}
