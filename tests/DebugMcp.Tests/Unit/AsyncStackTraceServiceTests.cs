using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit;

/// <summary>
/// Unit tests for AsyncStackTraceService — continuation chain walking logic.
/// Uses mock field readers to simulate ICorDebug value traversal.
/// </summary>
public class AsyncStackTraceServiceTests
{
    /// <summary>
    /// T017: Given a continuation chain of depth 3, service produces 3 logical frames
    /// with frame_kind "async_continuation" and correct method names.
    /// </summary>
    [Fact]
    public void BuildLogicalFrames_ContinuationChainDepth3_Produces3AdditionalFrames()
    {
        // Arrange: physical stack has one async frame (top), chain has 3 callers
        var physicalFrames = new List<StackFrame>
        {
            new(0, "MyApp.Service.GetDataAsync()", "MyApp.dll", false,
                FrameKind: "async", LogicalFunction: "GetDataAsync")
        };

        // Mock continuation chain: GetDataAsync <- ProcessAsync <- Main
        // Chain structure:
        //   Task(GetDataAsync) → m_continuationObject → delegate → _target → StateMachine(ProcessAsync)
        //     → <>t__builder.m_task → Task(ProcessAsync) → m_continuationObject → delegate → _target → StateMachine(Main)
        //       → <>t__builder.m_task → Task(Main) → m_continuationObject → null
        var chain = new MockContinuationChain();
        chain.AddLink("GetDataAsync", "ProcessAsync", "<ProcessAsync>d__2");
        chain.AddLink("ProcessAsync", "Main", "<Main>d__0");
        chain.SetTerminator("Main"); // null continuation

        var service = new AsyncStackTraceService(NullLogger<AsyncStackTraceService>.Instance);

        // Act
        var result = service.BuildLogicalFrames(
            physicalFrames,
            chain.ReadField,
            chain.GetTypeName);

        // Assert: original frame + 2 continuation frames (ProcessAsync and Main)
        result.Should().HaveCountGreaterThanOrEqualTo(3);

        // First frame is the original async frame
        result[0].FrameKind.Should().Be("async");
        result[0].LogicalFunction.Should().Be("GetDataAsync");

        // Continuation frames
        var continuationFrames = result.Where(f => f.FrameKind == "async_continuation").ToList();
        continuationFrames.Should().HaveCount(2);
        continuationFrames.Should().AllSatisfy(f => f.IsAwaiting.Should().BeTrue());

        // Verify method names in order
        var asyncFrames = result.Where(f => f.FrameKind is "async" or "async_continuation").ToList();
        asyncFrames.Select(f => f.LogicalFunction).Should().ContainInOrder("GetDataAsync", "ProcessAsync", "Main");
    }

    /// <summary>
    /// T018: Null m_continuationObject stops chain walking.
    /// </summary>
    [Fact]
    public void BuildLogicalFrames_NullContinuation_StopsChainWalking()
    {
        var physicalFrames = new List<StackFrame>
        {
            new(0, "MyApp.Service.RunAsync()", "MyApp.dll", false,
                FrameKind: "async", LogicalFunction: "RunAsync")
        };

        var chain = new MockContinuationChain();
        chain.SetTerminator("RunAsync"); // No continuation

        var service = new AsyncStackTraceService(NullLogger<AsyncStackTraceService>.Instance);

        var result = service.BuildLogicalFrames(
            physicalFrames,
            chain.ReadField,
            chain.GetTypeName);

        // Should only have the original frame — no continuation frames added
        result.Where(f => f.FrameKind == "async_continuation").Should().BeEmpty();
    }

    /// <summary>
    /// T018: Depth limit (50) stops infinite continuation chains.
    /// </summary>
    [Fact]
    public void BuildLogicalFrames_DepthLimit_StopsInfiniteChain()
    {
        var physicalFrames = new List<StackFrame>
        {
            new(0, "MyApp.Start()", "MyApp.dll", false,
                FrameKind: "async", LogicalFunction: "Start")
        };

        // Create a circular chain to simulate infinite loop
        var chain = new MockContinuationChain();
        for (int i = 0; i < 100; i++)
        {
            var current = $"Method{i}";
            var next = $"Method{i + 1}";
            chain.AddLink(current, next, $"<{next}>d__{i + 1}");
        }
        // First link from Start
        chain.AddLink("Start", "Method0", "<Method0>d__0");

        var service = new AsyncStackTraceService(NullLogger<AsyncStackTraceService>.Instance);

        var result = service.BuildLogicalFrames(
            physicalFrames,
            chain.ReadField,
            chain.GetTypeName);

        // Should be capped at the depth limit (original + max 50 continuations)
        var continuationFrames = result.Where(f => f.FrameKind == "async_continuation").ToList();
        continuationFrames.Should().HaveCountLessOrEqualTo(50);
    }

    /// <summary>
    /// T019: Unresolvable continuation type produces partial stack without error.
    /// </summary>
    [Fact]
    public void BuildLogicalFrames_UnresolvableContinuation_GracefulDegradation()
    {
        var physicalFrames = new List<StackFrame>
        {
            new(0, "MyApp.Service.ProcessAsync()", "MyApp.dll", false,
                FrameKind: "async", LogicalFunction: "ProcessAsync")
        };

        // Chain has an unresolvable continuation (non-delegate, non-Task)
        var chain = new MockContinuationChain();
        chain.AddUnresolvableLink("ProcessAsync");

        var service = new AsyncStackTraceService(NullLogger<AsyncStackTraceService>.Instance);

        // Act — should not throw
        var result = service.BuildLogicalFrames(
            physicalFrames,
            chain.ReadField,
            chain.GetTypeName);

        // Should have at least the original physical frame
        result.Should().NotBeEmpty();
        result[0].LogicalFunction.Should().Be("ProcessAsync");
    }

    /// <summary>
    /// Non-async physical frames are passed through unchanged.
    /// </summary>
    [Fact]
    public void BuildLogicalFrames_SyncFrames_PassedThrough()
    {
        var physicalFrames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main()", "MyApp.dll", false, FrameKind: "sync"),
            new(1, "System.Threading.Thread.Start()", "System.Private.CoreLib.dll", true, FrameKind: "sync")
        };

        var chain = new MockContinuationChain();
        var service = new AsyncStackTraceService(NullLogger<AsyncStackTraceService>.Instance);

        var result = service.BuildLogicalFrames(
            physicalFrames,
            chain.ReadField,
            chain.GetTypeName);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(f => f.FrameKind.Should().Be("sync"));
    }

    /// <summary>
    /// T030: Field name mapping: compiler-generated names → original source names.
    /// </summary>
    [Theory]
    [InlineData("<result>5__2", "result")]
    [InlineData("<response>5__1", "response")]
    [InlineData("<data>5__3", "data")]
    public void StripStateMachineFieldName_HoistedLocal_ReturnsOriginalName(string fieldName, string expected)
    {
        AsyncStackTraceService.StripStateMachineFieldName(fieldName).Should().Be(expected);
    }

    /// <summary>
    /// T030: Internal state machine fields get stripped display names.
    /// </summary>
    [Theory]
    [InlineData("<>1__state", "__state")]
    [InlineData("<>t__builder", "__builder")]
    public void StripStateMachineFieldName_InternalFields_ReturnsDisplayName(string fieldName, string expected)
    {
        AsyncStackTraceService.StripStateMachineFieldName(fieldName).Should().Be(expected);
    }

    /// <summary>
    /// T030: Wrap fields get stripped to a display name.
    /// </summary>
    [Theory]
    [InlineData("<>7__wrap1", "__wrap1")]
    [InlineData("<>7__wrap2", "__wrap2")]
    public void StripStateMachineFieldName_WrapFields_ReturnsStrippedName(string fieldName, string expected)
    {
        AsyncStackTraceService.StripStateMachineFieldName(fieldName).Should().Be(expected);
    }

    /// <summary>
    /// T030: Regular field names pass through unchanged.
    /// </summary>
    [Theory]
    [InlineData("normalField", "normalField")]
    [InlineData("_backingField", "_backingField")]
    [InlineData("Count", "Count")]
    public void StripStateMachineFieldName_RegularField_PassesThrough(string fieldName, string expected)
    {
        AsyncStackTraceService.StripStateMachineFieldName(fieldName).Should().Be(expected);
    }

    /// <summary>
    /// Mock continuation chain for testing chain walking logic without ICorDebug.
    /// Values are identified by string keys internally.
    /// </summary>
    private class MockContinuationChain
    {
        private readonly Dictionary<string, Dictionary<string, object?>> _objects = new();
        private readonly Dictionary<string, string> _typeNames = new();

        /// <summary>
        /// Adds a continuation link: current async method → next caller via state machine.
        /// </summary>
        public void AddLink(string currentMethod, string nextMethod, string nextStateMachineType)
        {
            var taskKey = $"task:{currentMethod}";
            var continuationKey = $"continuation:{currentMethod}";
            var targetKey = $"target:{nextMethod}";
            var builderKey = $"builder:{nextMethod}";
            var builderTaskKey = $"task:{nextMethod}";

            // Task.m_continuationObject → continuation delegate
            SetField(taskKey, "m_continuationObject", continuationKey);
            _typeNames[taskKey] = "System.Threading.Tasks.Task`1";

            // Continuation delegate._target → state machine
            SetField(continuationKey, "_target", targetKey);
            _typeNames[continuationKey] = "System.Action";

            // State machine fields
            SetField(targetKey, "<>1__state", -1); // awaiting
            SetField(targetKey, "<>t__builder", builderKey);
            _typeNames[targetKey] = nextStateMachineType;

            // Builder.m_task → next Task in chain
            SetField(builderKey, "m_task", builderTaskKey);
            _typeNames[builderKey] = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1";
        }

        /// <summary>
        /// Marks a method as the chain terminator (null continuation).
        /// </summary>
        public void SetTerminator(string method)
        {
            var taskKey = $"task:{method}";
            SetField(taskKey, "m_continuationObject", null);
            _typeNames.TryAdd(taskKey, "System.Threading.Tasks.Task`1");
        }

        /// <summary>
        /// Adds an unresolvable continuation (non-delegate, non-Task type).
        /// </summary>
        public void AddUnresolvableLink(string currentMethod)
        {
            var taskKey = $"task:{currentMethod}";
            var unresolvableKey = $"unresolvable:{currentMethod}";

            SetField(taskKey, "m_continuationObject", unresolvableKey);
            _typeNames[taskKey] = "System.Threading.Tasks.Task`1";

            // Unresolvable type — no _target, no m_continuationObject
            _typeNames[unresolvableKey] = "SomeUnknownCompletionCallback";
        }

        private void SetField(string objectKey, string fieldName, object? value)
        {
            if (!_objects.ContainsKey(objectKey))
                _objects[objectKey] = new Dictionary<string, object?>();
            _objects[objectKey][fieldName] = value;
        }

        public object? ReadField(object value, string fieldName)
        {
            if (value is string key && _objects.TryGetValue(key, out var fields))
            {
                if (fields.TryGetValue(fieldName, out var result))
                    return result;
            }
            return null;
        }

        public string? GetTypeName(object value)
        {
            if (value is string key && _typeNames.TryGetValue(key, out var name))
                return name;
            return null;
        }
    }
}
