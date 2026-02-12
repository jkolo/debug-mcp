using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using DebugMcp.Services;
using DebugMcp.Services.Inspection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DebugMcp.Tests.Unit.Inspection;

public class CollectionAnalyzerTests
{
    private readonly Mock<IDebugSessionManager> _sessionManager = new();
    private readonly CollectionAnalyzer _sut;

    public CollectionAnalyzerTests()
    {
        _sut = new CollectionAnalyzer(_sessionManager.Object, NullLogger<CollectionAnalyzer>.Instance);
    }

    // === Collection Type Detection (T007) ===

    [Theory]
    [InlineData("System.Int32[]", CollectionKind.Array)]
    [InlineData("System.String[]", CollectionKind.Array)]
    [InlineData("System.Collections.Generic.List`1[System.String]", CollectionKind.List)]
    [InlineData("System.Collections.Generic.Dictionary`2[System.String,System.Int32]", CollectionKind.Dictionary)]
    [InlineData("System.Collections.Generic.HashSet`1[System.Int32]", CollectionKind.Set)]
    [InlineData("System.Collections.Generic.SortedSet`1[System.String]", CollectionKind.Set)]
    [InlineData("System.Collections.Generic.Queue`1[System.Int32]", CollectionKind.Queue)]
    [InlineData("System.Collections.Generic.Stack`1[System.Int32]", CollectionKind.Queue)]
    [InlineData("System.Collections.Immutable.ImmutableArray`1[System.Int32]", CollectionKind.List)]
    [InlineData("System.Collections.Immutable.ImmutableList`1[System.Int32]", CollectionKind.List)]
    [InlineData("System.Collections.Concurrent.ConcurrentDictionary`2[System.String,System.Int32]", CollectionKind.Dictionary)]
    public void ClassifyCollection_KnownTypes_ReturnsCorrectKind(string typeName, CollectionKind expected)
    {
        CollectionAnalyzer.ClassifyCollection(typeName).Should().Be(expected);
    }

    [Theory]
    [InlineData("System.String")]
    [InlineData("MyApp.Customer")]
    [InlineData("System.Int32")]
    public void ClassifyCollection_NonCollectionTypes_ReturnsNull(string typeName)
    {
        CollectionAnalyzer.ClassifyCollection(typeName).Should().BeNull();
    }

    // === Array Analysis (T008) ===

    [Fact]
    public async Task AnalyzeAsync_IntArray_ReturnsCountAndElements()
    {
        SetupEvaluation("myArray", "System.Int32[]", "{System.Int32[]}");
        SetupEvaluation("myArray.Length", "System.Int32", "10");
        for (int i = 0; i < 5; i++)
            SetupEvaluation($"myArray[{i}]", "System.Int32", (i + 1).ToString());
        for (int i = 5; i < 10; i++)
            SetupEvaluation($"myArray[{i}]", "System.Int32", (i + 1).ToString());

        var result = await _sut.AnalyzeAsync("myArray");

        result.Count.Should().Be(10);
        result.Kind.Should().Be(CollectionKind.Array);
        result.ElementType.Should().Be("System.Int32");
        result.FirstElements.Should().HaveCount(5);
        result.LastElements.Should().HaveCount(5);
        result.IsSampled.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyArray_ReturnsZeroCount()
    {
        SetupEvaluation("emptyArr", "System.String[]", "{System.String[]}");
        SetupEvaluation("emptyArr.Length", "System.Int32", "0");

        var result = await _sut.AnalyzeAsync("emptyArr");

        result.Count.Should().Be(0);
        result.FirstElements.Should().BeEmpty();
        result.LastElements.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ListWithNulls_ReportsNullCount()
    {
        SetupEvaluation("myList", "System.Collections.Generic.List`1[System.String]", "{List}");
        SetupEvaluation("myList.Count", "System.Int32", "3");
        SetupEvaluation("myList[0]", "System.String", "\"hello\"");
        SetupEvaluation("myList[1]", "null", "null");
        SetupEvaluation("myList[2]", "System.String", "\"world\"");

        var result = await _sut.AnalyzeAsync("myList");

        result.Count.Should().Be(3);
        result.NullCount.Should().Be(1);
    }

    // === Numeric Statistics (T009) ===

    [Fact]
    public async Task AnalyzeAsync_NumericArray_ReturnsMinMaxAvg()
    {
        SetupEvaluation("nums", "System.Int32[]", "{System.Int32[]}");
        SetupEvaluation("nums.Length", "System.Int32", "5");
        for (int i = 0; i < 5; i++)
            SetupEvaluation($"nums[{i}]", "System.Int32", ((i + 1) * 10).ToString());

        var result = await _sut.AnalyzeAsync("nums");

        result.NumericStats.Should().NotBeNull();
        result.NumericStats!.Min.Should().Be("10");
        result.NumericStats!.Max.Should().Be("50");
        result.NumericStats!.Average.Should().Be("30");
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyNumericArray_ReturnsNoStats()
    {
        SetupEvaluation("empty", "System.Int32[]", "{System.Int32[]}");
        SetupEvaluation("empty.Length", "System.Int32", "0");

        var result = await _sut.AnalyzeAsync("empty");

        result.NumericStats.Should().BeNull();
    }

    // === Dictionary Analysis (T010) ===

    [Fact]
    public async Task AnalyzeAsync_Dictionary_ReturnsKeyValuePairs()
    {
        var dictType = "System.Collections.Generic.Dictionary`2[System.String,System.Int32]";
        SetupEvaluation("dict", dictType, "{Dictionary}");
        SetupEvaluation("dict.Count", "System.Int32", "2");

        // Setup LINQ ElementAt for dictionary entries
        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 0)", "System.Collections.Generic.KeyValuePair`2", "{KVP}");
        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 0).Key", "System.String", "\"alpha\"");
        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 0).Value", "System.Int32", "1");

        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 1)", "System.Collections.Generic.KeyValuePair`2", "{KVP}");
        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 1).Key", "System.String", "\"beta\"");
        SetupEvaluation("System.Linq.Enumerable.ElementAt(dict, 1).Value", "System.Int32", "2");

        var result = await _sut.AnalyzeAsync("dict");

        result.Count.Should().Be(2);
        result.Kind.Should().Be(CollectionKind.Dictionary);
        result.KeyValuePairs.Should().HaveCount(2);
        result.KeyValuePairs![0].Key.Should().Be("\"alpha\"");
        result.KeyValuePairs![0].Value.Should().Be("1");
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyDictionary_ReturnsEmptyPairs()
    {
        var dictType = "System.Collections.Generic.Dictionary`2[System.String,System.Int32]";
        SetupEvaluation("dict", dictType, "{Dictionary}");
        SetupEvaluation("dict.Count", "System.Int32", "0");

        // ElementAt(0) fails for empty dict
        SetupEvaluationFailure("System.Linq.Enumerable.ElementAt(dict, 0)");

        var result = await _sut.AnalyzeAsync("dict");

        result.Count.Should().Be(0);
        result.KeyValuePairs.Should().BeEmpty();
    }

    // === Error Cases (T011) ===

    [Fact]
    public async Task AnalyzeAsync_NonCollection_ThrowsWithNotCollectionMessage()
    {
        SetupEvaluation("customer", "MyApp.Customer", "{MyApp.Customer}");

        var act = () => _sut.AnalyzeAsync("customer");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a recognized collection*");
    }

    [Fact]
    public async Task AnalyzeAsync_EvalFails_ThrowsWithMessage()
    {
        SetupEvaluationFailure("badVar");

        var act = () => _sut.AnalyzeAsync("badVar");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // === Type Distribution (T017 verification) ===

    [Fact]
    public async Task AnalyzeAsync_MixedTypeList_ReturnsTypeDistribution()
    {
        SetupEvaluation("mixed", "System.Collections.Generic.List`1[System.Object]", "{List}");
        SetupEvaluation("mixed.Count", "System.Int32", "3");
        SetupEvaluation("mixed[0]", "System.String", "\"hello\"");
        SetupEvaluation("mixed[1]", "System.Int32", "42");
        SetupEvaluation("mixed[2]", "System.String", "\"world\"");

        var result = await _sut.AnalyzeAsync("mixed");

        result.TypeDistribution.Should().NotBeNull();
        result.TypeDistribution.Should().Contain(td => td.TypeName == "System.String" && td.Count == 2);
        result.TypeDistribution.Should().Contain(td => td.TypeName == "System.Int32" && td.Count == 1);
    }

    // === Helpers ===

    private void SetupEvaluation(string expression, string type, string value)
    {
        _sessionManager
            .Setup(m => m.EvaluateAsync(expression, It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: value, Type: type, HasChildren: false));
    }

    private void SetupEvaluationFailure(string expression)
    {
        _sessionManager
            .Setup(m => m.EvaluateAsync(expression, It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(
                Success: false,
                Error: new EvaluationError("variable_unavailable", $"Variable '{expression}' not found")));
    }
}
