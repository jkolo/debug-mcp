using DebugMcp.Models.Results;

namespace DebugMcp.Tests.Unit.Models;

/// <summary>
/// Invariants from data-model.md §1: Success ⟺ (Data non-null, Error null), and the converse.
/// </summary>
public class ToolResultTests
{
    [Fact]
    public void Constructor_SuccessWithDataAndNoError_Succeeds()
    {
        var result = new ToolResult<string>(Success: true, Data: "ok", Error: null);

        result.Success.Should().BeTrue();
        result.Data.Should().Be("ok");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Constructor_FailureWithErrorAndNoData_Succeeds()
    {
        var error = new ToolError("SOME_CODE", "message");

        var result = new ToolResult<string>(Success: false, Data: null, Error: error);

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_SuccessWithError_Throws()
    {
        var error = new ToolError("SOME_CODE", "message");

        var act = () => new ToolResult<string>(Success: true, Data: "ok", Error: error);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_SuccessWithNullData_Throws()
    {
        var act = () => new ToolResult<string>(Success: true, Data: null, Error: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_FailureWithNullError_Throws()
    {
        var act = () => new ToolResult<string>(Success: false, Data: null, Error: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_FailureWithNonNullData_Throws()
    {
        var error = new ToolError("SOME_CODE", "message");

        var act = () => new ToolResult<string>(Success: false, Data: "unexpected", Error: error);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WarningsOmitted_DefaultsToEmpty()
    {
        var result = new ToolResult<string>(Success: true, Data: "ok", Error: null);

        result.Warnings.Should().BeEmpty();
    }
}
