using AwesomeAssertions;
using DebugMcp.Models.Results;
using Xunit;

namespace DebugMcp.Tests.Unit.Results;

public class ResultTruncationTests
{
    [Fact]
    public void Bound_WhenUnderBudget_ReturnsAllItemsAndNoTruncation()
    {
        var items = Enumerable.Range(0, 5).Select(i => $"item-{i}").ToList();

        var (bounded, truncation) = ResultTruncation.Bound(items, "test budget", budgetBytes: 1024);

        bounded.Should().BeEquivalentTo(items, options => options.WithStrictOrdering());
        truncation.Should().BeNull();
    }

    [Fact]
    public void Bound_WhenEmpty_ReturnsEmptyAndNoTruncation()
    {
        var items = new List<string>();

        var (bounded, truncation) = ResultTruncation.Bound(items, "test budget", budgetBytes: 1024);

        bounded.Should().BeEmpty();
        truncation.Should().BeNull();
    }

    [Fact]
    public void Bound_WhenOverBudget_ReturnsPrefixWithinBudgetAndPopulatesTruncation()
    {
        var items = Enumerable.Range(0, 500).Select(i => new string('x', 50) + i).ToList();

        var (bounded, truncation) = ResultTruncation.Bound(items, "size cap exceeded", budgetBytes: 2048);

        bounded.Should().HaveCountLessThan(items.Count);
        bounded.Should().BeEquivalentTo(items.Take(bounded.Count), options => options.WithStrictOrdering());
        truncation.Should().NotBeNull();
        truncation!.Returned.Should().Be(bounded.Count);
        truncation.Available.Should().Be(items.Count);
        truncation.Reason.Should().Be("size cap exceeded");
    }

    [Fact]
    public void Bound_ResultNeverExceedsBudget_WhenSerialized()
    {
        var items = Enumerable.Range(0, 500).Select(i => new string('y', 100) + i).ToList();
        const int budget = 4096;

        var (bounded, _) = ResultTruncation.Bound(items, "reason", budgetBytes: budget);

        var serializedSize = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(bounded).Length;
        serializedSize.Should().BeLessThanOrEqualTo(budget);
    }
}
