using DebugMcp.Models.Inspection;
using DebugMcp.Models.Memory;
using DebugMcp.Services;
using DebugMcp.Services.Inspection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DebugMcp.Tests.Unit.Inspection;

public class ObjectSummarizerTests
{
    private readonly Mock<IDebugSessionManager> _sessionManager = new();
    private readonly ObjectSummarizer _sut;

    public ObjectSummarizerTests()
    {
        _sut = new ObjectSummarizer(_sessionManager.Object, NullLogger<ObjectSummarizer>.Instance);
    }

    // === Field Enumeration and Categorization (T021) ===

    [Fact]
    public async Task SummarizeAsync_ObjectWithMixedFields_CategorizedCorrectly()
    {
        SetupInspection("customer", "MyApp.Customer", 128, new[]
        {
            Field("Id", "System.Int32", "42"),
            Field("Name", "System.String", "\"John\""),
            Field("Phone", "System.String", "null"),
            Field("Address", "System.String", "null"),
        });

        var result = await _sut.SummarizeAsync("customer");

        result.TypeName.Should().Be("MyApp.Customer");
        result.Size.Should().Be(128);
        result.IsNull.Should().BeFalse();
        result.Fields.Should().HaveCount(2); // Id and Name (non-null)
        result.NullFields.Should().HaveCount(2); // Phone and Address
        result.NullFields.Should().Contain("Phone");
        result.NullFields.Should().Contain("Address");
        result.TotalFieldCount.Should().Be(4);
    }

    // === Interesting Field Detection (T022) ===

    [Fact]
    public void DetectInteresting_EmptyString_ReturnsFlagReason()
    {
        ObjectSummarizer.DetectInteresting("\"\"", "System.String").Should().Be("empty_string");
    }

    [Fact]
    public void DetectInteresting_NaN_ReturnsFlagReason()
    {
        ObjectSummarizer.DetectInteresting("NaN", "System.Double").Should().Be("nan");
        ObjectSummarizer.DetectInteresting("NaN", "System.Single").Should().Be("nan");
    }

    [Fact]
    public void DetectInteresting_Infinity_ReturnsFlagReason()
    {
        ObjectSummarizer.DetectInteresting("Infinity", "System.Double").Should().Be("infinity");
        ObjectSummarizer.DetectInteresting("-Infinity", "System.Single").Should().Be("infinity");
    }

    [Fact]
    public void DetectInteresting_DefaultDateTime_ReturnsFlagReason()
    {
        ObjectSummarizer.DetectInteresting("0001-01-01T00:00:00", "System.DateTime").Should().Be("default_datetime");
        ObjectSummarizer.DetectInteresting("0001-01-01T00:00:00+00:00", "System.DateTimeOffset").Should().Be("default_datetime");
    }

    [Fact]
    public void DetectInteresting_DefaultGuid_ReturnsFlagReason()
    {
        ObjectSummarizer.DetectInteresting("00000000-0000-0000-0000-000000000000", "System.Guid").Should().Be("default_guid");
    }

    [Fact]
    public void DetectInteresting_NormalZero_ReturnsNull()
    {
        ObjectSummarizer.DetectInteresting("0", "System.Int32").Should().BeNull();
    }

    [Fact]
    public void DetectInteresting_NormalString_ReturnsNull()
    {
        ObjectSummarizer.DetectInteresting("\"hello\"", "System.String").Should().BeNull();
    }

    [Fact]
    public async Task SummarizeAsync_ObjectWithInterestingFields_FlagsAnomalies()
    {
        SetupInspection("obj", "MyApp.Data", 64, new[]
        {
            Field("Email", "System.String", "\"\""),
            Field("Score", "System.Double", "NaN"),
            Field("LastLogin", "System.DateTimeOffset", "0001-01-01T00:00:00+00:00"),
            Field("Name", "System.String", "\"Alice\""),
        });

        var result = await _sut.SummarizeAsync("obj");

        result.InterestingFields.Should().HaveCount(3);
        result.InterestingFields.Should().Contain(f => f.Name == "Email" && f.Reason == "empty_string");
        result.InterestingFields.Should().Contain(f => f.Name == "Score" && f.Reason == "nan");
        result.InterestingFields.Should().Contain(f => f.Name == "LastLogin" && f.Reason == "default_datetime");
    }

    // === Null and Simple Objects (T023) ===

    [Fact]
    public async Task SummarizeAsync_NullObject_ReturnsIsNullTrue()
    {
        _sessionManager
            .Setup(m => m.InspectObjectAsync("obj", 1, null, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectInspection
            {
                Address = "0x0",
                TypeName = "MyApp.Customer",
                Size = 0,
                Fields = [],
                IsNull = true,
            });

        var result = await _sut.SummarizeAsync("obj");

        result.IsNull.Should().BeTrue();
        result.TypeName.Should().Be("MyApp.Customer");
        result.TotalFieldCount.Should().Be(0);
    }

    [Fact]
    public async Task SummarizeAsync_SimpleObject_ReturnsAllFieldsNoAnomalies()
    {
        SetupInspection("point", "System.Drawing.Point", 16, new[]
        {
            Field("X", "System.Int32", "10"),
            Field("Y", "System.Int32", "20"),
        });

        var result = await _sut.SummarizeAsync("point");

        result.Fields.Should().HaveCount(2);
        result.NullFields.Should().BeEmpty();
        result.InterestingFields.Should().BeEmpty();
    }

    // === Nested Collection Fields (T028 — US3) ===

    [Fact]
    public async Task SummarizeAsync_CollectionField_ShowsInlineCount()
    {
        SetupInspection("order", "MyApp.Order", 256, new[]
        {
            Field("Id", "System.Int32", "1"),
            Field("LineItems", "System.Collections.Generic.List`1[MyApp.LineItem]", "{System.Collections.Generic.List`1[MyApp.LineItem]}"),
        });

        // Setup Count eval for the collection field
        _sessionManager
            .Setup(m => m.EvaluateAsync("order.LineItems.Count", It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluationResult(Success: true, Value: "12", Type: "System.Int32"));

        var result = await _sut.SummarizeAsync("order");

        var lineItemsField = result.Fields.Should().Contain(f => f.Name == "LineItems").Which;
        lineItemsField.CollectionCount.Should().Be(12);
        lineItemsField.CollectionElementType.Should().Be("MyApp.LineItem");
    }

    [Fact]
    public async Task SummarizeAsync_NullCollectionField_InNullList()
    {
        SetupInspection("order", "MyApp.Order", 256, new[]
        {
            Field("Id", "System.Int32", "1"),
            Field("LineItems", "System.Collections.Generic.List`1[MyApp.LineItem]", "null"),
        });

        var result = await _sut.SummarizeAsync("order");

        result.NullFields.Should().Contain("LineItems");
    }

    // === Helpers ===

    private void SetupInspection(string expression, string typeName, int size, FieldDetail[] fields)
    {
        _sessionManager
            .Setup(m => m.InspectObjectAsync(expression, 1, It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectInspection
            {
                Address = "0x00007FF800000000",
                TypeName = typeName,
                Size = size,
                Fields = fields,
                IsNull = false,
            });
    }

    private static FieldDetail Field(string name, string type, string value)
    {
        return new FieldDetail
        {
            Name = name,
            TypeName = type,
            Value = value,
            Offset = 0,
            Size = 4,
            HasChildren = false,
        };
    }
}
