using DebugMcp.Services;
using FluentAssertions;

namespace DebugMcp.Tests.Unit;

/// <summary>
/// T009: Unit tests for async state machine frame detection.
/// Tests ProcessDebugger.TryParseAsyncStateMachineFrame for recognizing MoveNext on compiler-generated types.
/// </summary>
public class AsyncFrameDetectionTests
{
    /// <summary>
    /// Standard async state machine: type name matches pattern, method is MoveNext.
    /// </summary>
    [Theory]
    [InlineData("<GetUserAsync>d__5", "MoveNext", true, "GetUserAsync")]
    [InlineData("<ProcessDataAsync>d__12", "MoveNext", true, "ProcessDataAsync")]
    [InlineData("<Main>d__0", "MoveNext", true, "Main")]
    [InlineData("<RunAsync>d__100", "MoveNext", true, "RunAsync")]
    public void TryParse_AsyncStateMachine_DetectsCorrectly(
        string typeName, string methodName, bool expectedIsAsync, string expectedOriginalName)
    {
        var (isAsync, originalName) = ProcessDebugger.TryParseAsyncStateMachineFrame(typeName, methodName);

        isAsync.Should().Be(expectedIsAsync);
        originalName.Should().Be(expectedOriginalName);
    }

    /// <summary>
    /// Regular synchronous methods should NOT be detected as async.
    /// </summary>
    [Theory]
    [InlineData("Program", "Main")]
    [InlineData("MyService", "ProcessData")]
    [InlineData("System.String", "Concat")]
    public void TryParse_SyncMethod_NotDetectedAsAsync(string typeName, string methodName)
    {
        var (isAsync, originalName) = ProcessDebugger.TryParseAsyncStateMachineFrame(typeName, methodName);

        isAsync.Should().BeFalse();
        originalName.Should().BeNull();
    }

    /// <summary>
    /// Lambda display class methods should NOT be detected as async.
    /// </summary>
    [Theory]
    [InlineData("<>c", "<Main>b__0_0")]
    [InlineData("<>c__DisplayClass5_0", "<ProcessAsync>b__0")]
    public void TryParse_LambdaDisplayClass_NotDetectedAsAsync(string typeName, string methodName)
    {
        var (isAsync, originalName) = ProcessDebugger.TryParseAsyncStateMachineFrame(typeName, methodName);

        isAsync.Should().BeFalse();
        originalName.Should().BeNull();
    }

    /// <summary>
    /// MoveNext on a non-state-machine type should NOT be detected as async.
    /// </summary>
    [Theory]
    [InlineData("MyEnumerator", "MoveNext")]
    [InlineData("List`1+Enumerator", "MoveNext")]
    public void TryParse_MoveNextOnNonStateMachine_NotDetectedAsAsync(string typeName, string methodName)
    {
        var (isAsync, originalName) = ProcessDebugger.TryParseAsyncStateMachineFrame(typeName, methodName);

        isAsync.Should().BeFalse();
        originalName.Should().BeNull();
    }

    /// <summary>
    /// State machine type with method other than MoveNext should NOT be detected.
    /// </summary>
    [Fact]
    public void TryParse_StateMachineTypeNonMoveNext_NotDetected()
    {
        var (isAsync, originalName) = ProcessDebugger.TryParseAsyncStateMachineFrame(
            "<GetUserAsync>d__5", "SetStateMachine");

        isAsync.Should().BeFalse();
        originalName.Should().BeNull();
    }
}
