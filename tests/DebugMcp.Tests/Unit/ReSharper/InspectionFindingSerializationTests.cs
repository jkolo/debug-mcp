using System.Text.Json;
using DebugMcp.Models.ReSharper;

namespace DebugMcp.Tests.Unit.ReSharper;

public sealed class InspectionFindingSerializationTests
{
    [Theory]
    [InlineData(ReSharperSeverity.Error, "error")]
    [InlineData(ReSharperSeverity.Warning, "warning")]
    [InlineData(ReSharperSeverity.Suggestion, "suggestion")]
    [InlineData(ReSharperSeverity.Hint, "hint")]
    public void Severity_SerializesAsLowerCaseString(ReSharperSeverity severity, string expected)
    {
        var finding = new InspectionFinding { Id = "X", Message = "m", Severity = severity };

        var json = JsonSerializer.Serialize(finding);

        json.Should().Contain($"\"severity\":\"{expected}\"");
    }

    [Fact]
    public void Severity_RoundTripsCaseInsensitively()
    {
        var json = "{\"id\":\"X\",\"message\":\"m\",\"severity\":\"WARNING\"}";
        var finding = JsonSerializer.Deserialize<InspectionFinding>(json);
        finding!.Severity.Should().Be(ReSharperSeverity.Warning);
    }
}
