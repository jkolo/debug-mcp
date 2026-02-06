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
/// Unit tests for static type member completion (US3).
/// Tests that static members of types like DateTime, Math are returned correctly.
/// </summary>
public class StaticMemberCompletionTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _processDebuggerMock;
    private readonly ExpressionCompletionProvider _provider;

    public StaticMemberCompletionTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _processDebuggerMock = new Mock<IProcessDebugger>();
        _provider = new ExpressionCompletionProvider(
            _sessionManagerMock.Object,
            _processDebuggerMock.Object,
            NullLogger<ExpressionCompletionProvider>.Instance);
    }

    [Fact]
    public async Task GetCompletionsAsync_MathDot_ReturnsStaticMembers()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        // Note: "Math." is resolved to "System.Math" by ResolveTypeName
        _processDebuggerMock.Setup(m => m.GetMembersAsync("System.Math", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("System.Math",
                methods: [
                    CreateMethod("Abs", "double", isStatic: true),
                    CreateMethod("Sin", "double", isStatic: true),
                    CreateMethod("Cos", "double", isStatic: true)
                ],
                properties: [],
                fields: [
                    CreateField("PI", "double", isStatic: true, isConst: true),
                    CreateField("E", "double", isStatic: true, isConst: true)
                ]));
        var request = CreateCompleteRequest("Math.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "Abs", "Sin", "Cos", "PI", "E" });
    }

    [Fact]
    public async Task GetCompletionsAsync_DateTimeDotN_FiltersByPrefix()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        // Note: "DateTime." is resolved to "System.DateTime" by ResolveTypeName
        _processDebuggerMock.Setup(m => m.GetMembersAsync("System.DateTime", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("System.DateTime",
                methods: [],
                properties: [
                    CreateProperty("Now", "DateTime", isStatic: true),
                    CreateProperty("UtcNow", "DateTime", isStatic: true),
                    CreateProperty("Today", "DateTime", isStatic: true),
                    CreateProperty("MinValue", "DateTime", isStatic: true)
                ],
                fields: []));
        var request = CreateCompleteRequest("DateTime.N");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "Now" });
        result.Completion.Values.Should().NotContain(new[] { "UtcNow", "Today", "MinValue" });
    }

    [Fact]
    public async Task GetCompletionsAsync_TypeNotFound_ReturnsEmpty()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        _processDebuggerMock.Setup(m => m.GetMembersAsync("UnknownType", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Type 'UnknownType' not found"));
        var request = CreateCompleteRequest("UnknownType.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompletionsAsync_ConsoleDot_ReturnsStaticMembers()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        // Note: "Console." is resolved to "System.Console" by ResolveTypeName
        _processDebuggerMock.Setup(m => m.GetMembersAsync("System.Console", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("System.Console",
                methods: [
                    CreateMethod("WriteLine", "void", isStatic: true),
                    CreateMethod("ReadLine", "string", isStatic: true)
                ],
                properties: [
                    CreateProperty("Out", "TextWriter", isStatic: true),
                    CreateProperty("In", "TextReader", isStatic: true)
                ],
                fields: []));
        var request = CreateCompleteRequest("Console.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().Contain(new[] { "WriteLine", "ReadLine", "Out", "In" });
    }

    [Fact]
    public async Task GetCompletionsAsync_StaticMemberPrefixIsCaseInsensitive()
    {
        // Arrange
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        // Note: "Math." is resolved to "System.Math" by ResolveTypeName
        _processDebuggerMock.Setup(m => m.GetMembersAsync("System.Math", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("System.Math",
                methods: [],
                properties: [],
                fields: [
                    CreateField("PI", "double", isStatic: true, isConst: true),
                    CreateField("E", "double", isStatic: true, isConst: true)
                ]));
        var request = CreateCompleteRequest("Math.pi");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert
        result.Completion.Values.Should().BeEquivalentTo(new[] { "PI" });
    }

    [Fact]
    public async Task GetCompletionsAsync_OnlyReturnsStaticMembers()
    {
        // Arrange - type has both static and instance members
        var session = CreatePausedSession();
        _sessionManagerMock.Setup(m => m.CurrentSession).Returns(session);
        // Note: "String." is resolved to "System.String" by ResolveTypeName
        _processDebuggerMock.Setup(m => m.GetMembersAsync("System.String", null, false, null, null, true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTypeMembersResult("System.String",
                methods: [
                    CreateMethod("IsNullOrEmpty", "bool", isStatic: true),
                    CreateMethod("Join", "string", isStatic: true),
                    CreateMethod("ToUpper", "string", isStatic: false) // Instance method
                ],
                properties: [
                    CreateProperty("Empty", "string", isStatic: true),
                    CreateProperty("Length", "int", isStatic: false) // Instance property
                ],
                fields: []));
        var request = CreateCompleteRequest("String.");

        // Act
        var result = await _provider.GetCompletionsAsync(request, CancellationToken.None);

        // Assert - should only have static members
        result.Completion.Values.Should().Contain(new[] { "IsNullOrEmpty", "Join", "Empty" });
        result.Completion.Values.Should().NotContain(new[] { "ToUpper", "Length" });
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

    private static MethodMemberInfo CreateMethod(string name, string returnType, bool isStatic, Visibility visibility = Visibility.Public)
    {
        return new MethodMemberInfo(
            Name: name,
            Signature: $"{returnType} {name}()",
            ReturnType: returnType,
            Parameters: [],
            Visibility: visibility,
            IsStatic: isStatic,
            IsVirtual: false,
            IsAbstract: false,
            IsGeneric: false,
            GenericParameters: null,
            DeclaringType: "TestType");
    }

    private static PropertyMemberInfo CreateProperty(string name, string type, bool isStatic, Visibility visibility = Visibility.Public)
    {
        return new PropertyMemberInfo(
            Name: name,
            Type: type,
            Visibility: visibility,
            IsStatic: isStatic,
            HasGetter: true,
            HasSetter: false,
            GetterVisibility: visibility,
            SetterVisibility: null,
            IsIndexer: false,
            IndexerParameters: null);
    }

    private static FieldMemberInfo CreateField(string name, string type, bool isStatic, bool isConst = false, Visibility visibility = Visibility.Public)
    {
        return new FieldMemberInfo(
            Name: name,
            Type: type,
            Visibility: visibility,
            IsStatic: isStatic,
            IsReadOnly: false,
            IsConst: isConst,
            ConstValue: null);
    }
}
