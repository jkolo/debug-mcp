using System.Text.Json;
using AwesomeAssertions;
using DebugMcp.Models.CodeAnalysis;
using DebugMcp.Services.CodeAnalysis;
using DebugMcp.Services.Progress;
using DebugMcp.Services.Snapshots;
using DebugMcp.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-021/FR-017: a client that reads only <c>content[0].text</c>, as every client does today,
/// must see the same field names and meanings after US3's typed-result migration. Verified by
/// serializing each tool's returned record the way the SDK does (camelCase, nulls omitted — see
/// data-model.md §1), then parsing and comparing fields, not by string equality — the contract's
/// own wire example (contracts/tool-result-contract.md) is compact JSON, while today's
/// pre-migration hand-rolled tools used <c>WriteIndented = true</c>; indentation was never part
/// of the contract, field presence and values are. One case per migrated tool, added as each tool
/// moves off its hand-rolled <c>JsonSerializer.Serialize(new {...})</c> (T044–T052); this file
/// starts with the pilot (<c>snapshot_delete</c>) as the worked example the rest follows.
/// </summary>
public class LegacyTextContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public async Task SnapshotDelete_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-1")).Returns(true);
        storeMock.Setup(s => s.Count).Returns(3);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-1");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("deleted").GetString().Should().Be("snap-1");
        parsed.GetProperty("remaining").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SnapshotDelete_Failure_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ISnapshotService>();
        var storeMock = new Mock<ISnapshotStore>();
        serviceMock.Setup(s => s.DeleteSnapshot("snap-missing")).Returns(false);
        var tool = new SnapshotDeleteTool(serviceMock.Object, storeMock.Object, Mock.Of<ILogger<SnapshotDeleteTool>>());

        var result = await tool.DeleteSnapshotAsync("snap-missing");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("SNAPSHOT_NOT_FOUND");
        parsed.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CodeLoad_Success_PreservesLegacyFieldNames()
    {
        // CodeLoadTool checks File.Exists(path) directly (not via the service), so the path
        // must be a real file on disk for the success path to be reachable.
        var slnPath = Path.Combine(Path.GetTempPath(), $"LegacyTextContractTests_{Guid.NewGuid():N}.sln");
        File.WriteAllText(slnPath, string.Empty);
        try
        {
            var serviceMock = new Mock<ICodeAnalysisService>();
            var workspaceInfo = new WorkspaceInfo
            {
                Path = slnPath,
                Type = WorkspaceType.Solution,
                Projects = [],
                Diagnostics = [],
                LoadedAt = DateTimeOffset.UtcNow
            };
            serviceMock.Setup(s => s.LoadAsync(slnPath, It.IsAny<CancellationToken>(), It.IsAny<IProgressReporter?>()))
                .ReturnsAsync(workspaceInfo);
            var tool = new CodeLoadTool(serviceMock.Object, Mock.Of<ILogger<CodeLoadTool>>());

            var result = await tool.LoadAsync(slnPath);
            var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

            parsed.GetProperty("success").GetBoolean().Should().BeTrue();
            parsed.GetProperty("data").GetProperty("path").GetString().Should().Be(slnPath);
            parsed.GetProperty("data").GetProperty("type").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        }
        finally
        {
            File.Delete(slnPath);
        }
    }

    [Fact]
    public async Task CodeLoad_InvalidPath_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        var tool = new CodeLoadTool(serviceMock.Object, Mock.Of<ILogger<CodeLoadTool>>());

        var result = await tool.LoadAsync("");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("INVALID_PATH");
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CodeGoToDefinition_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns(new WorkspaceInfo
        {
            Path = "/repo/Solution.sln",
            Type = WorkspaceType.Solution,
            Projects = [],
            Diagnostics = [],
            LoadedAt = DateTimeOffset.UtcNow
        });
        var goToDefinitionResult = new GoToDefinitionResult
        {
            Symbol = new SymbolInfo
            {
                Name = "DoWork",
                FullyQualifiedName = "My.Namespace.MyClass.DoWork",
                Kind = SymbolKind.Method,
                ContainingType = "MyClass",
                ContainingNamespace = "My.Namespace"
            },
            Definitions =
            [
                new SymbolDefinition { File = "/repo/MyClass.cs", Line = 10, Column = 5, EndLine = 10, EndColumn = 20, IsSource = true }
            ]
        };
        serviceMock.Setup(s => s.GoToDefinitionAsync("/repo/MyClass.cs", 3, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(goToDefinitionResult);
        var tool = new CodeGoToDefinitionTool(serviceMock.Object, Mock.Of<ILogger<CodeGoToDefinitionTool>>());

        var result = await tool.GoToDefinitionAsync("/repo/MyClass.cs", 3, 7);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var symbol = parsed.GetProperty("data").GetProperty("symbol");
        symbol.GetProperty("name").GetString().Should().Be("DoWork");
        symbol.GetProperty("fully_qualified_name").GetString().Should().Be("My.Namespace.MyClass.DoWork");
        symbol.GetProperty("kind").GetString().Should().Be("Method");
        symbol.GetProperty("containing_type").GetString().Should().Be("MyClass");
        symbol.GetProperty("containing_namespace").GetString().Should().Be("My.Namespace");
        symbol.TryGetProperty("declaration_file", out _).Should().BeFalse();
        parsed.GetProperty("data").GetProperty("definitions_count").GetInt32().Should().Be(1);
        parsed.GetProperty("data").GetProperty("definitions")[0].GetProperty("file").GetString().Should().Be("/repo/MyClass.cs");
    }

    [Fact]
    public async Task CodeGoToDefinition_NoWorkspace_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns((WorkspaceInfo?)null);
        var tool = new CodeGoToDefinitionTool(serviceMock.Object, Mock.Of<ILogger<CodeGoToDefinitionTool>>());

        var result = await tool.GoToDefinitionAsync("/repo/MyClass.cs", 3, 7);
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_WORKSPACE");
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CodeFindUsages_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns(new WorkspaceInfo
        {
            Path = "/repo/Solution.sln",
            Type = WorkspaceType.Solution,
            Projects = [],
            Diagnostics = [],
            LoadedAt = DateTimeOffset.UtcNow
        });
        var symbol = new SymbolInfo
        {
            Name = "Count",
            FullyQualifiedName = "My.Namespace.MyClass.Count",
            Kind = SymbolKind.Property,
            ContainingType = "MyClass",
            ContainingNamespace = "My.Namespace",
            DeclarationFile = "/repo/MyClass.cs",
            DeclarationLine = 5,
            DeclarationColumn = 12
        };
        serviceMock.Setup(s => s.FindSymbolByNameAsync("My.Namespace.MyClass.Count", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(symbol);
        serviceMock.Setup(s => s.FindUsagesAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SymbolUsage>)
            [
                new SymbolUsage { File = "/repo/Other.cs", Line = 1, Column = 1, EndLine = 1, EndColumn = 5, Kind = UsageKind.Read }
            ]);
        var tool = new CodeFindUsagesTool(serviceMock.Object, Mock.Of<ILogger<CodeFindUsagesTool>>());

        var result = await tool.FindUsagesAsync(name: "My.Namespace.MyClass.Count");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var symbolElement = parsed.GetProperty("data").GetProperty("symbol");
        symbolElement.GetProperty("name").GetString().Should().Be("Count");
        symbolElement.GetProperty("kind").GetString().Should().Be("Property");
        symbolElement.GetProperty("declaration_file").GetString().Should().Be("/repo/MyClass.cs");
        symbolElement.GetProperty("declaration_line").GetInt32().Should().Be(5);
        symbolElement.GetProperty("declaration_column").GetInt32().Should().Be(12);
        parsed.GetProperty("data").GetProperty("usages_count").GetInt32().Should().Be(1);
        parsed.GetProperty("data").GetProperty("usages")[0].GetProperty("file").GetString().Should().Be("/repo/Other.cs");
    }

    [Fact]
    public async Task CodeFindUsages_NoWorkspace_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns((WorkspaceInfo?)null);
        var tool = new CodeFindUsagesTool(serviceMock.Object, Mock.Of<ILogger<CodeFindUsagesTool>>());

        var result = await tool.FindUsagesAsync(name: "My.Namespace.MyClass.Count");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_WORKSPACE");
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CodeFindAssignments_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns(new WorkspaceInfo
        {
            Path = "/repo/Solution.sln",
            Type = WorkspaceType.Solution,
            Projects = [],
            Diagnostics = [],
            LoadedAt = DateTimeOffset.UtcNow
        });
        var symbol = new SymbolInfo
        {
            Name = "_count",
            FullyQualifiedName = "My.Namespace.MyClass._count",
            Kind = SymbolKind.Field,
            ContainingType = "MyClass",
            ContainingNamespace = "My.Namespace",
            DeclarationFile = "/repo/MyClass.cs",
            DeclarationLine = 5,
            DeclarationColumn = 12
        };
        serviceMock.Setup(s => s.FindSymbolByNameAsync("My.Namespace.MyClass._count", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(symbol);
        serviceMock.Setup(s => s.FindAssignmentsAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SymbolAssignment>)
            [
                new SymbolAssignment { File = "/repo/Other.cs", Line = 2, Column = 3, EndLine = 2, EndColumn = 9, Kind = AssignmentKind.Simple, Operator = "=" }
            ]);
        var tool = new CodeFindAssignmentsTool(serviceMock.Object, Mock.Of<ILogger<CodeFindAssignmentsTool>>());

        var result = await tool.FindAssignmentsAsync(name: "My.Namespace.MyClass._count");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        var symbolElement = parsed.GetProperty("data").GetProperty("symbol");
        symbolElement.GetProperty("name").GetString().Should().Be("_count");
        symbolElement.GetProperty("kind").GetString().Should().Be("Field");
        symbolElement.GetProperty("declaration_file").GetString().Should().Be("/repo/MyClass.cs");
        symbolElement.GetProperty("declaration_line").GetInt32().Should().Be(5);
        symbolElement.TryGetProperty("containing_namespace", out _).Should().BeFalse();
        symbolElement.TryGetProperty("declaration_column", out _).Should().BeFalse();
        parsed.GetProperty("data").GetProperty("assignments_count").GetInt32().Should().Be(1);
        parsed.GetProperty("data").GetProperty("assignments")[0].GetProperty("operator").GetString().Should().Be("=");
    }

    [Fact]
    public async Task CodeFindAssignments_NoWorkspace_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns((WorkspaceInfo?)null);
        var tool = new CodeFindAssignmentsTool(serviceMock.Object, Mock.Of<ILogger<CodeFindAssignmentsTool>>());

        var result = await tool.FindAssignmentsAsync(name: "My.Namespace.MyClass._count");
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_WORKSPACE");
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CodeGetDiagnostics_Success_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns(new WorkspaceInfo
        {
            Path = "/repo/Solution.sln",
            Type = WorkspaceType.Solution,
            Projects = [],
            Diagnostics = [],
            LoadedAt = DateTimeOffset.UtcNow
        });
        serviceMock.Setup(s => s.GetDiagnosticsAsync(null, DiagnosticSeverity.Warning, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<DiagnosticInfo>)
            [
                new DiagnosticInfo { Id = "CS0168", Message = "Variable declared but never used", Severity = DiagnosticSeverity.Warning }
            ]);
        var tool = new CodeGetDiagnosticsTool(serviceMock.Object, Mock.Of<ILogger<CodeGetDiagnosticsTool>>());

        var result = await tool.GetDiagnosticsAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeTrue();
        parsed.GetProperty("data").GetProperty("total_count").GetInt32().Should().Be(1);
        parsed.GetProperty("data").GetProperty("limited_to").GetInt32().Should().Be(100);
        parsed.GetProperty("data").GetProperty("summary").GetProperty("warning").GetInt32().Should().Be(1);
        parsed.GetProperty("data").GetProperty("diagnostics")[0].GetProperty("id").GetString().Should().Be("CS0168");
    }

    [Fact]
    public async Task CodeGetDiagnostics_NoWorkspace_PreservesLegacyFieldNames()
    {
        var serviceMock = new Mock<ICodeAnalysisService>();
        serviceMock.Setup(s => s.CurrentWorkspace).Returns((WorkspaceInfo?)null);
        var tool = new CodeGetDiagnosticsTool(serviceMock.Object, Mock.Of<ILogger<CodeGetDiagnosticsTool>>());

        var result = await tool.GetDiagnosticsAsync();
        var parsed = JsonSerializer.SerializeToElement(result, WireOptions);

        parsed.GetProperty("success").GetBoolean().Should().BeFalse();
        parsed.GetProperty("error").GetProperty("code").GetString().Should().Be("NO_WORKSPACE");
        parsed.TryGetProperty("data", out _).Should().BeFalse();
    }
}
