using DebugMcp.Models;
using DebugMcp.Models.Inspection;
using DebugMcp.Services;
using DebugMcp.Services.Completions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Unit.Completions;

/// <summary>
/// Unit tests for variable name completion (US1).
/// Tests that variable names from current scope are returned and filtered correctly.
/// </summary>
public class VariableCompletionTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly ExpressionCompletionProvider _provider;

    public VariableCompletionTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _provider = new ExpressionCompletionProvider(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            NullLogger<ExpressionCompletionProvider>.Instance);
    }

    [Fact]
    public async Task GetCompletionsAsync_EmptyPrefix_ReturnsAllVariablesInScope()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("customer", "Customer", "Customer { Name = \"John\" }"),
                CreateVar("orderId", "int", "42"),
                CreateVar("items", "List<Item>", "Count = 3")
            });
        var request = CreateCompleteRequest("");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "customer", "orderId", "items" });
        result.Completion.Total.Should().Be(3);
    }

    [Fact]
    public async Task GetCompletionsAsync_WithPrefix_FiltersVariablesByPrefix()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("customer", "Customer", "Customer { Name = \"John\" }"),
                CreateVar("customerId", "int", "123"),
                CreateVar("orderId", "int", "42"),
                CreateVar("count", "int", "5")
            });
        var request = CreateCompleteRequest("cust");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "customer", "customerId" });
        result.Completion.Values.Should().NotContain(new[] { "orderId", "count" });
    }

    [Fact]
    public async Task GetCompletionsAsync_PrefixIsCaseInsensitive()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("Customer", "Customer", "value"),
                CreateVar("customerName", "string", "value")
            });
        var request = CreateCompleteRequest("CUST");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "Customer", "customerName" });
    }

    [Fact]
    public async Task GetCompletionsAsync_IncludesThisInInstanceMethods()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("this", "MyClass", "MyClass { }", VariableScope.This),
                CreateVar("localVar", "int", "10")
            });
        var request = CreateCompleteRequest("th");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain("this");
    }

    [Fact]
    public async Task GetCompletionsAsync_NoVariables_ReturnsEmptyList()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>());
        var request = CreateCompleteRequest("");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
        result.Completion.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetCompletionsAsync_NoPrefixMatch_ReturnsEmptyList()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("customer", "Customer", "value"),
                CreateVar("order", "Order", "value")
            });
        var request = CreateCompleteRequest("xyz");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_ExcludesNullOrEmptyNames()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("validName", "string", "value"),
                CreateVar("", "string", "empty name"),
                CreateVar(null!, "string", "null name")
            });
        var request = CreateCompleteRequest("");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "validName" });
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsSortedAlphabetically()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("zebra", "string", "value"),
                CreateVar("apple", "string", "value"),
                CreateVar("mango", "string", "value")
            });
        var request = CreateCompleteRequest("");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(
            new[] { "apple", "mango", "zebra" },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetCompletionsAsync_ExcludesDuplicateNames()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.GetVariables(null, 0, "all", null))
            .Returns(new List<Variable>
            {
                CreateVar("name", "string", "value1"),
                CreateVar("name", "string", "value2"), // Duplicate
                CreateVar("other", "int", "42")
            });
        var request = CreateCompleteRequest("");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "name", "other" });
        result.Completion.Total.Should().Be(2);
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

    private static Variable CreateVar(string name, string type, string value, VariableScope scope = VariableScope.Local)
    {
        return new Variable(name, type, value, scope, HasChildren: false);
    }
}
