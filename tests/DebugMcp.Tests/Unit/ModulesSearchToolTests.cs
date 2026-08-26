using DebugMcp.Models;
using DebugMcp.Models.Modules;
using DebugMcp.Services;
using DebugMcp.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DebugMcp.Tests.Unit;

public class ModulesSearchToolTests
{
    private readonly Mock<IDebugSessionManager> _sessionManagerMock;
    private readonly Mock<IProcessDebugger> _debuggerMock;
    private readonly ModulesSearchTool _tool;

    public ModulesSearchToolTests()
    {
        _sessionManagerMock = new Mock<IDebugSessionManager>();
        _debuggerMock = new Mock<IProcessDebugger>();
        var logger = new Mock<ILogger<ModulesSearchTool>>();
        _tool = new ModulesSearchTool(_sessionManagerMock.Object, _debuggerMock.Object, logger.Object);

        _sessionManagerMock.Setup(s => s.CurrentSession).Returns(new DebugSession
        {
            ProcessId = 1,
            ProcessName = "test",
            ExecutablePath = "/bin/test",
            RuntimeVersion = ".NET 10.0",
            AttachedAt = DateTimeOffset.UtcNow,
            State = SessionState.Running,
            LaunchMode = LaunchMode.Launch,
        });
    }

    [Fact]
    public async Task SearchModules_Success_MapsTypesAndMethodsAndLowercasesEnums()
    {
        var typeInfo = new TypeInfo("MyApp.Customer", "Customer", "MyApp", TypeKind.Class,
            Visibility.Public, false, null, false, null, "MyApp", "System.Object", null);
        var method = new MethodMemberInfo("GetCustomer", "Customer GetCustomer(int id)", "Customer",
            [], Visibility.Public, false, false, false, false, null, "MyApp.Services.CustomerService");
        var methodMatch = new MethodSearchMatch("MyApp.Services.CustomerService", "MyApp", method, "name");
        var searchResult = new SearchResult("*Customer*", SearchType.Both, [typeInfo], [methodMatch],
            2, 2, false, null);
        _debuggerMock
            .Setup(d => d.SearchModulesAsync("*Customer*", SearchType.Both, null, false, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        var result = await _tool.SearchModules("*Customer*");

        result.Success.Should().BeTrue();
        result.Query.Should().Be("*Customer*");
        result.SearchType.Should().Be("both");
        result.Types.Should().ContainSingle();
        result.Types![0].Kind.Should().Be("class");
        result.Types[0].Visibility.Should().Be("public");
        result.Methods.Should().ContainSingle();
        result.Methods![0].Method.Visibility.Should().Be("public");
        result.TotalMatches.Should().Be(2);
        result.ReturnedMatches.Should().Be(2);
        result.Truncated.Should().Be(false);
        result.ContinuationToken.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task SearchModules_EmptyPattern_ReturnsInvalidPatternError()
    {
        var result = await _tool.SearchModules(" ");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.InvalidPattern);
    }

    [Fact]
    public async Task SearchModules_InvalidSearchType_ReturnsInvalidParameterError()
    {
        var result = await _tool.SearchModules("*Customer*", search_type: "bogus");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.InvalidParameter);
    }

    [Fact]
    public async Task SearchModules_NoSession_ReturnsNoSessionError()
    {
        _sessionManagerMock.Setup(s => s.CurrentSession).Returns((DebugSession?)null);

        var result = await _tool.SearchModules("*Customer*");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.NoSession);
    }

    [Fact]
    public async Task SearchModules_DebuggerThrows_ReturnsSearchFailedError()
    {
        _debuggerMock
            .Setup(d => d.SearchModulesAsync("*Customer*", SearchType.Both, null, false, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var result = await _tool.SearchModules("*Customer*");

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ErrorCodes.SearchFailed);
    }
}
