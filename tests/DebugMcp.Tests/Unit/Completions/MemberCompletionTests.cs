using DebugMcp.Models;
using DebugMcp.Models.Inspection;
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
/// Unit tests for object member completion (US2).
/// Tests that object members are returned and filtered correctly after a dot.
/// </summary>
public class MemberCompletionTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly ExpressionCompletionProvider _provider;

    public MemberCompletionTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _provider = new ExpressionCompletionProvider(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            NullLogger<ExpressionCompletionProvider>.Instance);
    }

    [Fact]
    public async Task GetCompletionsAsync_ObjectDot_ReturnsMembers()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("user", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "User { Name = \"John\" }", "TestApp.User"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.User", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.User",
                methods: [CreateMethod("GetFullName", "string")],
                properties: [CreateProperty("Name", "string"), CreateProperty("Email", "string")],
                fields: []));
        var request = CreateCompleteRequest("user.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "Name", "Email", "GetFullName" });
    }

    [Fact]
    public async Task GetCompletionsAsync_ObjectDotWithPrefix_FiltersByPrefix()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("user", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "User { }", "TestApp.User"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.User", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.User",
                methods: [],
                properties: [CreateProperty("Name", "string"), CreateProperty("NamePrefix", "string"), CreateProperty("Email", "string")],
                fields: []));
        var request = CreateCompleteRequest("user.Na");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "Name", "NamePrefix" });
        result.Completion.Values.Should().NotContain("Email");
    }

    [Fact]
    public async Task GetCompletionsAsync_IncludesPublicAndNonPublicMembers()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("obj", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "MyClass { }", "MyClass"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("MyClass", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("MyClass",
                methods: [CreateMethod("ProtectedMethod", "void", Visibility.Protected)],
                properties: [CreateProperty("PublicProp", "string")],
                fields: [CreateField("_privateField", "int", Visibility.Private)]));
        var request = CreateCompleteRequest("obj.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "PublicProp", "_privateField", "ProtectedMethod" });
    }

    [Fact]
    public async Task GetCompletionsAsync_ObjectEvaluationFails_ReturnsEmpty()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("nonexistent", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(false, "Error: 'nonexistent' not found", null));
        var request = CreateCompleteRequest("nonexistent.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_TypeIsNull_ReturnsEmpty()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("nullValue", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "null", null)); // Type is null
        var request = CreateCompleteRequest("nullValue.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_NestedPropertyAccess_WorksCorrectly()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("customer.Address", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "Address { }", "TestApp.Address"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.Address", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.Address",
                methods: [],
                properties: [CreateProperty("City", "string"), CreateProperty("ZipCode", "string")],
                fields: []));
        var request = CreateCompleteRequest("customer.Address.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "City", "ZipCode" });
    }

    [Fact]
    public async Task GetCompletionsAsync_MethodCallResult_WorksCorrectly()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("GetUser()", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "User { }", "TestApp.User"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.User", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.User",
                methods: [],
                properties: [CreateProperty("Id", "int")],
                fields: []));
        var request = CreateCompleteRequest("GetUser().");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain("Id");
    }

    [Fact]
    public async Task GetCompletionsAsync_IndexerAccess_WorksCorrectly()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("list[0]", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "Item { }", "TestApp.Item"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.Item", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.Item",
                methods: [],
                properties: [CreateProperty("Value", "object")],
                fields: []));
        var request = CreateCompleteRequest("list[0].");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain("Value");
    }

    [Fact]
    public async Task GetCompletionsAsync_MemberPrefixIsCaseInsensitive()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("user", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "User { }", "TestApp.User"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("TestApp.User", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("TestApp.User",
                methods: [],
                properties: [CreateProperty("Name", "string")],
                fields: [CreateField("namespace", "string", Visibility.Private)]));
        var request = CreateCompleteRequest("user.NAME");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "Name", "namespace" });
    }

    [Fact]
    public async Task GetCompletionsAsync_ReturnsDistinctMembers()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _sessionManagerMock.Setup(m => m.EvaluateAsync("obj", null, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(true, "MyClass { }", "MyClass"));
        _processDebuggerMock.Setup(m => m.GetMembersAsync("MyClass", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("MyClass",
                methods: [
                    CreateMethod("ToString", "string"),
                    CreateMethod("ToString", "string") // Overload (same name)
                ],
                properties: [],
                fields: []));
        var request = CreateCompleteRequest("obj.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().ContainSingle(v => v == "ToString");
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

    private static TypeMembersResult CreateTypeMembersResult(
        string typeName,
        MethodMemberInfo[] methods,
        PropertyMemberInfo[] properties,
        FieldMemberInfo[] fields)
    {
        return new TypeMembersResult(
            TypeName: typeName,
            Methods: methods,
            Properties: properties,
            Fields: fields,
            Events: [],
            IncludesInherited: false,
            MethodCount: methods.Length,
            PropertyCount: properties.Length,
            FieldCount: fields.Length,
            EventCount: 0);
    }

    private static MethodMemberInfo CreateMethod(string name, string returnType, Visibility visibility = Visibility.Public)
    {
        return new MethodMemberInfo(
            Name: name,
            Signature: $"{returnType} {name}()",
            ReturnType: returnType,
            Parameters: [],
            Visibility: visibility,
            IsStatic: false,
            IsVirtual: false,
            IsAbstract: false,
            IsGeneric: false,
            GenericParameters: null,
            DeclaringType: "TestType");
    }

    private static PropertyMemberInfo CreateProperty(string name, string type, Visibility visibility = Visibility.Public)
    {
        return new PropertyMemberInfo(
            Name: name,
            Type: type,
            Visibility: visibility,
            IsStatic: false,
            HasGetter: true,
            HasSetter: true,
            GetterVisibility: visibility,
            SetterVisibility: visibility,
            IsIndexer: false,
            IndexerParameters: null);
    }

    private static FieldMemberInfo CreateField(string name, string type, Visibility visibility = Visibility.Public)
    {
        return new FieldMemberInfo(
            Name: name,
            Type: type,
            Visibility: visibility,
            IsStatic: false,
            IsReadOnly: false,
            IsConst: false,
            ConstValue: null);
    }
}
