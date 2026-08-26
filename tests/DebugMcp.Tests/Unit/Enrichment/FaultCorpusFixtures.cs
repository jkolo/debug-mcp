using DebugMcp.Models;
using DebugMcp.Models.Inspection;

namespace DebugMcp.Tests.Unit.Enrichment;

/// <summary>
/// Hand-built <see cref="AutopsyFrame"/> lists mirroring the 10 fault-scenario fixtures in
/// <c>tests/DebugTestApp/FaultScenarios/</c> (FR-030). Frame index 0 is always the innermost
/// frame (top of stack), matching <see cref="AutopsyFrame.Index"/>'s own convention.
///
/// These are NOT captured from a live debugger run — <c>SuspicionRanker</c> operates on already-
/// captured domain models, so there is nothing live-debugger-specific to reproduce here; only the
/// shape of what a real run would plausibly hand it. The human-identified fault frame for each is
/// recorded in <c>tests/DebugTestApp/FaultScenarios/expected-answers.json</c>, which this file's
/// data must stay consistent with.
/// </summary>
internal static class FaultCorpusFixtures
{
    private static Variable Local(string name, string type, string value, bool hasChildren = false, int? childrenCount = null) =>
        new(name, type, value, VariableScope.Local, hasChildren, childrenCount);

    private static Variable Argument(string name, string type, string value, bool hasChildren = false, int? childrenCount = null) =>
        new(name, type, value, VariableScope.Argument, hasChildren, childrenCount);

    private static AutopsyFrame Frame(
        int index, string function, bool isExternal = false,
        IReadOnlyList<Variable>? arguments = null, IReadOnlyList<Variable>? locals = null) =>
        new(
            Index: index,
            Function: function,
            Module: isExternal ? "System.Private.CoreLib" : "DebugTestApp",
            IsExternal: isExternal,
            Location: isExternal ? null : new SourceLocation($"FaultScenarios/{function.Split('.')[^2]}.cs", 10 + index),
            Arguments: arguments,
            Variables: locals is null ? null : new FrameVariables(locals));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) NullDereference = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.NullDereference.Run",
                locals: new[] { Local("name", "string", "null") }),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) NestedCallChain = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.NestedCallChain.Deepest",
                arguments: new[] { Argument("customerId", "string", "null") }),
            Frame(1, "DebugTestApp.FaultScenarios.NestedCallChain.Middle",
                arguments: new[] { Argument("customerId", "string", "null") }),
            Frame(2, "DebugTestApp.FaultScenarios.NestedCallChain.Outer"),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) AsyncBoundary = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.AsyncBoundary.LoadOrderAsync",
                arguments: new[] { Argument("orderId", "string", "null") }),
            Frame(1, "DebugTestApp.FaultScenarios.AsyncBoundary.RunAsync"),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) AggregateInnerException = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.AggregateInnerException.ProcessItem",
                arguments: new[] { Argument("sku", "string", "null") }),
            Frame(1, "System.Threading.Tasks.Task.InnerInvoke", isExternal: true),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) NoSymbolsAvailable = (
        new[]
        {
            Frame(0, "0x00007ff8a1234560", isExternal: true),
        },
        new ExceptionDetail("System.InvalidOperationException", "Simulated fault in a symbol-less module.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) ExternalFrameDemotion = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.ExternalFrameDemotion.ParseConfig",
                arguments: new[] { Argument("raw", "string", "null") }),
            Frame(1, "System.Collections.Generic.List.ForEach", isExternal: true),
            Frame(2, "DebugTestApp.FaultScenarios.ExternalFrameDemotion.Run"),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) ExceptionMessageReference = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.ExceptionMessageReference.Validate",
                arguments: new[] { Argument("orderId", "string", "null"), Argument("sessionToken", "string", "null") }),
            Frame(1, "DebugTestApp.FaultScenarios.ExceptionMessageReference.Run"),
        },
        new ExceptionDetail("System.ArgumentNullException", "Value cannot be null. (Parameter 'orderId')", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) EmptyCollectionFault = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.EmptyCollectionFault.PickFirst",
                arguments: new[] { Argument("items", "System.Collections.Generic.List<int>", "Count = 0", hasChildren: true, childrenCount: 0) }),
            Frame(1, "DebugTestApp.FaultScenarios.EmptyCollectionFault.Run"),
        },
        new ExceptionDetail("System.InvalidOperationException", "Sequence contains no elements", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) MultipleNullCandidates = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.MultipleNullCandidates.Innermost",
                locals: new[] { Local("result", "string", "null") }),
            Frame(1, "DebugTestApp.FaultScenarios.MultipleNullCandidates.Middle",
                locals: new[] { Local("cachedValue", "string", "null") }),
            Frame(2, "DebugTestApp.FaultScenarios.MultipleNullCandidates.Run"),
        },
        new ExceptionDetail("System.NullReferenceException", "Object reference not set to an instance of an object.", IsFirstChance: false));

    public static readonly (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) DeepChainManyExternalFrames = (
        new[]
        {
            Frame(0, "DebugTestApp.FaultScenarios.DeepChainManyExternalFrames.Step3"),
            Frame(1, "DebugTestApp.FaultScenarios.DeepChainManyExternalFrames.Step2"),
            Frame(2, "DebugTestApp.FaultScenarios.DeepChainManyExternalFrames.Step1"),
            Frame(3, "DebugTestApp.FaultScenarios.DeepChainManyExternalFrames.Run"),
        },
        new ExceptionDetail("System.InvalidOperationException", "Unexpected state reached after several steps.", IsFirstChance: false));

    /// <summary>All 10 fixtures paired with their human-identified fault frame index, null for
    /// <see cref="NoSymbolsAvailable"/> where no ranking is expected at all — matches
    /// expected-answers.json.</summary>
    public static IReadOnlyList<(string Name, (IReadOnlyList<AutopsyFrame> Frames, ExceptionDetail? Exception) Scenario, int? ExpectedFrameIndex)> All { get; } = new (string, (IReadOnlyList<AutopsyFrame>, ExceptionDetail?), int?)[]
    {
        (nameof(NullDereference), NullDereference, 0),
        (nameof(NestedCallChain), NestedCallChain, 0),
        (nameof(AsyncBoundary), AsyncBoundary, 0),
        (nameof(AggregateInnerException), AggregateInnerException, 0),
        (nameof(NoSymbolsAvailable), NoSymbolsAvailable, null),
        (nameof(ExternalFrameDemotion), ExternalFrameDemotion, 0),
        (nameof(ExceptionMessageReference), ExceptionMessageReference, 0),
        (nameof(EmptyCollectionFault), EmptyCollectionFault, 0),
        (nameof(MultipleNullCandidates), MultipleNullCandidates, 0),
        (nameof(DeepChainManyExternalFrames), DeepChainManyExternalFrames, 0),
    };
}
