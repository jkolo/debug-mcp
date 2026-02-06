using DebugMcp.Models;
using DebugMcp.Services;
using DebugMcp.Services.Completions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Unit.Completions;

/// <summary>
/// Unit tests for ExpressionCompletionProvider base behavior.
/// Tests session state handling and basic completion flow.
/// </summary>
public class ExpressionCompletionProviderTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly ExpressionCompletionProvider _provider;

    public ExpressionCompletionProviderTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _provider = new ExpressionCompletionProvider(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            NullLogger<ExpressionCompletionProvider>.Instance);
    }

    [Fact]
    public async Task GetCompletionsAsync_NoSession_ReturnsEmptyCompletions()
    {
        // Arrange
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns((DebugSession?)null);
        var request = CreateCompleteRequest("expression", "user");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
        result.Completion.Total.Should().Be(0);
        result.Completion.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetCompletionsAsync_SessionRunning_ReturnsEmptyCompletions()
    {
        // Arrange
        var session = CreateSession(SessionState.Running);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("expression", "user");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
        result.Completion.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetCompletionsAsync_UnknownArgumentName_ReturnsEmptyCompletions()
    {
        // Arrange
        var session = CreateSession(SessionState.Paused);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("unknown_arg", "user");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_ValidRequest_ReturnsCompleteResult()
    {
        // Arrange
        var session = CreateSession(SessionState.Paused);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Models.Inspection.Variable>());
        var request = CreateCompleteRequest("expression", "");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Completion.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCompletionsAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var session = CreateSession(SessionState.Paused);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("expression", "user");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _provider.GetCompletionsAsync(request, cts.Token));
    }

    [Fact]
    public async Task GetCompletionsAsync_DisconnectedState_ReturnsEmptyCompletions()
    {
        // Arrange
        var session = CreateSession(SessionState.Disconnected);
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("expression", "user");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    private static CompleteRequestParams CreateCompleteRequest(string argumentName, string argumentValue)
    {
        // Create a PromptReference for the request (MCP supports prompts/resources, not tools directly)
        return new CompleteRequestParams
        {
            Ref = new PromptReference { Name = "evaluate" },
            Argument = new Argument
            {
                Name = argumentName,
                Value = argumentValue
            }
        };
    }

    private static DebugSession CreateSession(SessionState state)
    {
        return new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestProcess",
            ExecutablePath = "/path/to/test",
            RuntimeVersion = "8.0.0",
            State = state,
            LaunchMode = LaunchMode.Attach,
            AttachedAt = DateTimeOffset.UtcNow
        };
    }
}
