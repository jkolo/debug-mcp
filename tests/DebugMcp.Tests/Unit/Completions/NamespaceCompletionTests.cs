using DebugMcp.Models;
using DebugMcp.Models.Modules;
using DebugMcp.Services;
using DebugMcp.Services.Completions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Unit.Completions;

/// <summary>
/// Unit tests for namespace-qualified type completion (US4).
/// Tests that well-known namespace contents are returned correctly.
///
/// Note: Full namespace enumeration from loaded modules is complex and left for future enhancement.
/// For MVP, we support well-known namespaces (System, System.Collections, System.IO, etc.)
/// </summary>
public class NamespaceCompletionTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly ExpressionCompletionProvider _provider;

    public NamespaceCompletionTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _provider = new ExpressionCompletionProvider(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            NullLogger<ExpressionCompletionProvider>.Instance);
    }

    [Fact]
    public async Task GetCompletionsAsync_SystemDot_ReturnsWellKnownChildNamespaces()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("System.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should return well-known child namespaces
        result.Completion.Values.Should().Contain(new[] { "Collections", "IO", "Text", "Threading", "Linq", "Net", "Reflection" });
    }

    [Fact]
    public async Task GetCompletionsAsync_SystemCollectionsDot_ReturnsChildNamespace()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("System.Collections.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should return Generic child namespace
        result.Completion.Values.Should().Contain("Generic");
    }

    [Fact]
    public async Task GetCompletionsAsync_SystemDotWithPrefix_FiltersByPrefix()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("System.Col");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should only return items starting with "Col"
        result.Completion.Values.Should().Contain("Collections");
        result.Completion.Values.Should().NotContain(new[] { "IO", "Text", "Threading" });
    }

    [Fact]
    public async Task GetCompletionsAsync_UnknownNamespace_ReturnsEmpty()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("UnknownNamespace.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_MicrosoftDot_ReturnsWellKnownChildNamespaces()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("Microsoft.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should return common Microsoft child namespaces
        result.Completion.Values.Should().Contain("Extensions");
    }

    [Fact]
    public async Task GetCompletionsAsync_SystemThreadingDot_ReturnsChildNamespace()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("System.Threading.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should return Tasks child namespace
        result.Completion.Values.Should().Contain("Tasks");
    }

    [Fact]
    public async Task GetCompletionsAsync_NamespacePrefixIsCaseInsensitive()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        var request = CreateCompleteRequest("System.COL");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain("Collections");
    }

    private static CompleteRequestParams CreateCompleteRequest(string expression)
    {
        return new CompleteRequestParams
        {
            Ref = new PromptReference { Name = "evaluate" },
            Argument = new Argument
            {
                Name = "expression",
                Value = expression
            }
        };
    }

    private static DebugSession CreatePausedSession()
    {
        return new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "TestProcess",
            ExecutablePath = "/path/to/test",
            RuntimeVersion = "8.0.0",
            State = SessionState.Paused,
            LaunchMode = LaunchMode.Attach,
            AttachedAt = DateTimeOffset.UtcNow
        };
    }
}
