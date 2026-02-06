using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit.Inspection;

public class ExceptionAutopsyServiceTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly Mock<ILogger<ExceptionAutopsyService>> _loggerMock;
    private readonly ExceptionAutopsyService _sut;

    public ExceptionAutopsyServiceTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _loggerMock = new Mock<ILogger<ExceptionAutopsyService>>();
        _sut = new ExceptionAutopsyService(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetExceptionContext_WhenPausedAtException_ReturnsBundledContext()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);

        // Mock EvaluateAsync for exception type, message, stack trace
        SetupExceptionEvaluation(
            type: "System.InvalidOperationException",
            message: "Something went wrong",
            stackTrace: "   at MyApp.Service.DoWork() in /src/Service.cs:line 42");

        // Mock GetStackFrames - 3 frames
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Service.DoWork", "MyApp.dll", false,
                new SourceLocation("/src/Service.cs", 42, 5, "DoWork", "MyApp.dll")),
            new(1, "MyApp.Controller.Handle", "MyApp.dll", false,
                new SourceLocation("/src/Controller.cs", 100, 9, "Handle", "MyApp.dll")),
            new(2, "System.Runtime.CompilerServices.TaskAwaiter.GetResult", "System.Runtime.dll", true)
        };
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 3));

        // Mock GetVariables for frame 0 (includeVariablesForFrames=1 by default)
        var locals = new List<Variable>
        {
            new("input", "string", "\"hello\"", VariableScope.Local, false),
            new("count", "int", "42", VariableScope.Local, false)
        };
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "locals", null))
            .Returns(locals.AsReadOnly());

        var args = new List<Variable>
        {
            new("request", "HttpRequest", "HttpRequest { Method = GET }", VariableScope.Argument, true)
        };
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "arguments", null))
            .Returns(args.AsReadOnly());

        // Mock no inner exceptions
        SetupNoInnerException();

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert
        result.ThreadId.Should().Be(threadId);
        result.Exception.Type.Should().Be("System.InvalidOperationException");
        result.Exception.Message.Should().Be("Something went wrong");
        result.Exception.IsFirstChance.Should().BeTrue();
        result.Exception.StackTraceString.Should().Contain("DoWork");
        result.Frames.Should().HaveCount(3);
        result.Frames[0].Function.Should().Be("MyApp.Service.DoWork");
        result.Frames[0].Variables.Should().NotBeNull();
        result.Frames[0].Variables!.Locals.Should().HaveCount(2);
        result.Frames[0].Arguments.Should().HaveCount(1);
        result.Frames[1].Variables.Should().BeNull();
        result.TotalFrames.Should().Be(3);
        result.InnerExceptions.Should().BeEmpty();
        result.InnerExceptionsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task GetExceptionContext_WhenNotPausedAtException_ThrowsInvalidOperationException()
    {
        // Arrange — paused at breakpoint, NOT at exception
        _processDebuggerMock.Setup(p => p.CurrentPauseReason).Returns(PauseReason.Breakpoint);
        _processDebuggerMock.Setup(p => p.CurrentState).Returns(SessionState.Paused);
        _processDebuggerMock.Setup(p => p.ActiveThreadId).Returns(1);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(CreatePausedSession(PauseReason.Breakpoint));

        // Act & Assert
        var act = () => _sut.GetExceptionContextAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not paused at*exception*");
    }

    [Fact]
    public async Task GetExceptionContext_WithInnerExceptions_ReturnsChain()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation(
            type: "System.AggregateException",
            message: "One or more errors occurred.",
            stackTrace: "   at MyApp.Program.Main()");

        SetupMinimalFrames(threadId);

        // Mock inner exception chain: depth 1 = ArgumentException, depth 2 = FormatException, depth 3 = null
        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.GetType().FullName",
                threadId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "System.ArgumentException", "System.String"));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.Message",
                threadId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "Invalid argument", "System.String"));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.InnerException.GetType().FullName",
                threadId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "System.FormatException", "System.String"));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.InnerException.Message",
                threadId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "Input string was not in a correct format.", "System.String"));

        // 3rd level returns failure (no more inner exceptions)
        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.InnerException.InnerException.GetType().FullName",
                threadId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, null, null, false,
                new EvaluationError("EVAL_FAIL", "Object reference is null")));

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert
        result.InnerExceptions.Should().HaveCount(2);
        result.InnerExceptions[0].Type.Should().Be("System.ArgumentException");
        result.InnerExceptions[0].Message.Should().Be("Invalid argument");
        result.InnerExceptions[0].Depth.Should().Be(1);
        result.InnerExceptions[1].Type.Should().Be("System.FormatException");
        result.InnerExceptions[1].Message.Should().Be("Input string was not in a correct format.");
        result.InnerExceptions[1].Depth.Should().Be(2);
        result.InnerExceptionsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task GetExceptionContext_WhenVariableInspectionFails_ReturnsPartialResultWithErrors()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation(
            type: "System.NullReferenceException",
            message: "Object reference not set to an instance of an object.",
            stackTrace: "   at MyApp.Handler.Process()");

        // Stack frames - 2 frames, request variables for first 2 (includeVariablesForFrames=2)
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Handler.Process", "MyApp.dll", false,
                new SourceLocation("/src/Handler.cs", 25, 1, "Process", "MyApp.dll")),
            new(1, "MyApp.Pipeline.Execute", "MyApp.dll", false,
                new SourceLocation("/src/Pipeline.cs", 80, 1, "Execute", "MyApp.dll"))
        };
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 2));

        // Frame 0: variables succeed
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "locals", null))
            .Returns(new List<Variable>
            {
                new("data", "object", "null", VariableScope.Local, false)
            }.AsReadOnly());
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "arguments", null))
            .Returns(new List<Variable>().AsReadOnly());

        // Frame 1: variables throw (e.g., optimized code)
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 1, "locals", null))
            .Throws(new InvalidOperationException("Cannot inspect optimized frame"));
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 1, "arguments", null))
            .Throws(new InvalidOperationException("Cannot inspect optimized frame"));

        SetupNoInnerException();

        // Act
        var result = await _sut.GetExceptionContextAsync(includeVariablesForFrames: 2);

        // Assert
        result.Frames.Should().HaveCount(2);

        // Frame 0 should have variables
        result.Frames[0].Variables.Should().NotBeNull();
        result.Frames[0].Variables!.Locals.Should().HaveCount(1);
        result.Frames[0].Variables!.Errors.Should().BeNullOrEmpty();

        // Frame 1 should have error info (partial result, not exception)
        result.Frames[1].Variables.Should().NotBeNull();
        result.Frames[1].Variables!.Errors.Should().NotBeEmpty();
        result.Frames[1].Variables!.Errors![0].Error.Should().Contain("optimized");
    }

    [Fact]
    public async Task GetExceptionContext_WhenSymbolsMissing_ReturnsFramesWithNullLocation()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation(
            type: "System.IO.FileNotFoundException",
            message: "Could not find file 'data.txt'.",
            stackTrace: "   at System.IO.File.Open()");

        // Frames with external code (no symbols)
        var frames = new List<StackFrame>
        {
            new(0, "System.IO.File.Open", "System.IO.dll", true),
            new(1, "System.IO.FileStream..ctor", "System.IO.dll", true),
            new(2, "MyApp.DataLoader.Load", "MyApp.dll", false,
                new SourceLocation("/src/DataLoader.cs", 15, 1, "Load", "MyApp.dll"))
        };
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 3));

        // Only collect variables for frame 0, but it's external — variables not available
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "locals", null))
            .Throws(new InvalidOperationException("No symbols loaded for module"));
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "arguments", null))
            .Throws(new InvalidOperationException("No symbols loaded for module"));

        SetupNoInnerException();

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert
        result.Frames.Should().HaveCount(3);

        // External frames: no source location
        result.Frames[0].IsExternal.Should().BeTrue();
        result.Frames[0].Location.Should().BeNull();
        result.Frames[1].IsExternal.Should().BeTrue();
        result.Frames[1].Location.Should().BeNull();

        // User frame: has source location
        result.Frames[2].IsExternal.Should().BeFalse();
        result.Frames[2].Location.Should().NotBeNull();
        result.Frames[2].Location!.File.Should().Be("/src/DataLoader.cs");
    }

    // --- Phase 4: US2 — Configurable depth and scope ---

    [Fact]
    public async Task GetExceptionContext_WithMaxFrames3_ReturnsOnly3Frames()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation("System.Exception", "test", "at Test()");
        SetupNoInnerException();

        // 10 frames available
        var frames = Enumerable.Range(0, 10)
            .Select(i => new StackFrame(i, $"Namespace.Class.Method{i}", "App.dll", false,
                new SourceLocation($"/src/File{i}.cs", i + 1)))
            .ToList();
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 3))
            .Returns((frames.Take(3).ToList().AsReadOnly(), 10));

        SetupVariablesForFrame(threadId, 0);

        // Act
        var result = await _sut.GetExceptionContextAsync(maxFrames: 3);

        // Assert
        result.Frames.Should().HaveCount(3);
        result.TotalFrames.Should().Be(10);
    }

    [Fact]
    public async Task GetExceptionContext_WithVariablesFor3Frames_ReturnsVariablesForTop3()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation("System.Exception", "test", "at Test()");
        SetupNoInnerException();

        var frames = Enumerable.Range(0, 5)
            .Select(i => new StackFrame(i, $"Namespace.Class.Method{i}", "App.dll", false,
                new SourceLocation($"/src/File{i}.cs", i + 1)))
            .ToList();
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 5));

        // Setup variables for frames 0-2
        for (var i = 0; i < 3; i++)
            SetupVariablesForFrame(threadId, i);

        // Act
        var result = await _sut.GetExceptionContextAsync(includeVariablesForFrames: 3);

        // Assert
        result.Frames.Should().HaveCount(5);
        result.Frames[0].Variables.Should().NotBeNull();
        result.Frames[1].Variables.Should().NotBeNull();
        result.Frames[2].Variables.Should().NotBeNull();
        result.Frames[3].Variables.Should().BeNull();
        result.Frames[4].Variables.Should().BeNull();
    }

    [Fact]
    public async Task GetExceptionContext_WithMaxInnerExceptions0_SkipsInnerChain()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation("System.Exception", "test", "at Test()");
        SetupMinimalFrames(threadId);

        // Act
        var result = await _sut.GetExceptionContextAsync(maxInnerExceptions: 0);

        // Assert
        result.InnerExceptions.Should().BeEmpty();
        result.InnerExceptionsTruncated.Should().BeFalse();

        // Verify EvaluateAsync for InnerException was never called
        _sessionManagerMock.Verify(
            m => m.EvaluateAsync(
                It.Is<string>(s => s.Contains("InnerException")),
                It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetExceptionContext_WithDefaultParameters_Uses10Frames1Variable5Inner()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation("System.Exception", "test", "at Test()");
        SetupNoInnerException();

        var frames = Enumerable.Range(0, 10)
            .Select(i => new StackFrame(i, $"Namespace.Class.Method{i}", "App.dll", false,
                new SourceLocation($"/src/File{i}.cs", i + 1)))
            .ToList();
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 10));

        SetupVariablesForFrame(threadId, 0);

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert: defaults — 10 max frames, variables for frame 0 only
        _sessionManagerMock.Verify(m => m.GetStackFrames(threadId, 0, 10), Times.Once);
        result.Frames[0].Variables.Should().NotBeNull();

        // Frames 1+ should NOT have variables
        for (var i = 1; i < result.Frames.Count; i++)
            result.Frames[i].Variables.Should().BeNull();
    }

    // --- BUG-3: $exception eval fallback to LastExceptionInfo ---

    [Fact]
    public async Task GetExceptionContext_WhenEvalFails_FallsBackToLastExceptionInfo()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);

        // Mock eval returning failure for all $exception calls
        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.GetType().FullName",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, null, null, false,
                new EvaluationError("EVAL_FAIL", "Cannot evaluate expression")));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.Message",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, null, null, false,
                new EvaluationError("EVAL_FAIL", "Cannot evaluate expression")));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.StackTrace",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, null, null, false,
                new EvaluationError("EVAL_FAIL", "Cannot evaluate expression")));

        // Provide stored exception info via LastExceptionInfo
        _processDebuggerMock.Setup(p => p.LastExceptionInfo)
            .Returns(("System.InvalidOperationException", "Operation failed", false));

        SetupMinimalFrames(threadId);
        SetupNoInnerException();

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert — should use fallback data
        result.Exception.Type.Should().Be("System.InvalidOperationException");
        result.Exception.Message.Should().Be("Operation failed");
        result.Exception.IsFirstChance.Should().BeFalse();
    }

    [Fact]
    public async Task GetExceptionContext_WhenEvalSucceeds_UsesEvalResult()
    {
        // Arrange
        const int threadId = 1;
        SetupPausedAtException(threadId);
        SetupExceptionEvaluation(
            type: "System.ArgumentNullException",
            message: "Value cannot be null",
            stackTrace: "   at MyApp.Service.DoWork()");

        // Also provide LastExceptionInfo — should NOT be used
        _processDebuggerMock.Setup(p => p.LastExceptionInfo)
            .Returns(("FallbackType", "Fallback message", true));

        SetupMinimalFrames(threadId);
        SetupNoInnerException();

        // Act
        var result = await _sut.GetExceptionContextAsync();

        // Assert — should use eval results, not fallback
        result.Exception.Type.Should().Be("System.ArgumentNullException");
        result.Exception.Message.Should().Be("Value cannot be null");
        // IsFirstChance comes from LastExceptionInfo since it's always used for that
        result.Exception.IsFirstChance.Should().BeTrue();
    }

    #region Helpers

    private void SetupPausedAtException(int threadId)
    {
        _processDebuggerMock.Setup(p => p.CurrentPauseReason).Returns(PauseReason.Exception);
        _processDebuggerMock.Setup(p => p.CurrentState).Returns(SessionState.Paused);
        _processDebuggerMock.Setup(p => p.ActiveThreadId).Returns(threadId);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(CreatePausedSession(PauseReason.Exception));
    }

    private void SetupExceptionEvaluation(string type, string message, string stackTrace)
    {
        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.GetType().FullName",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, type, "System.String"));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.Message",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, message, "System.String"));

        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.StackTrace",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, stackTrace, "System.String"));

        // IsFirstChance — check via ProcessDebugger's last exception event (default: true)
    }

    private void SetupNoInnerException()
    {
        _sessionManagerMock
            .Setup(m => m.EvaluateAsync(
                "$exception.InnerException.GetType().FullName",
                It.IsAny<int?>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, null, null, false,
                new EvaluationError("EVAL_FAIL", "Object reference is null")));
    }

    private void SetupMinimalFrames(int threadId)
    {
        var frames = new List<StackFrame>
        {
            new(0, "MyApp.Program.Main", "MyApp.dll", false,
                new SourceLocation("/src/Program.cs", 10, 1, "Main", "MyApp.dll"))
        };
        _sessionManagerMock
            .Setup(m => m.GetStackFrames(threadId, 0, 10))
            .Returns((frames.AsReadOnly(), 1));

        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "locals", null))
            .Returns(new List<Variable>().AsReadOnly());
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, 0, "arguments", null))
            .Returns(new List<Variable>().AsReadOnly());
    }

    private void SetupVariablesForFrame(int threadId, int frameIndex)
    {
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, frameIndex, "locals", null))
            .Returns(new List<Variable>
            {
                new($"local{frameIndex}", "int", frameIndex.ToString(), VariableScope.Local, false)
            }.AsReadOnly());
        _sessionManagerMock
            .Setup(m => m.GetVariables(threadId, frameIndex, "arguments", null))
            .Returns(new List<Variable>().AsReadOnly());
    }

    private static DebugSession CreatePausedSession(PauseReason reason)
    {
        return new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestApp",
            ExecutablePath = "/path/to/TestApp.dll",
            RuntimeVersion = ".NET 10.0",
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Attach,
            AttachedAt = DateTimeOffset.UtcNow,
            PauseReason = reason,
            ActiveThreadId = 1
        };
    }

    #endregion
}
