using DebugMcp.Models.ReSharper;
using DebugMcp.Services.ReSharper;

namespace DebugMcp.Tests.Unit.ReSharper;

public sealed class InspectionReportParserTests
{
    private readonly InspectionReportParser _parser = new();
    private const string SolutionDir = "/repo/sln";

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReSharper", name));

    [Fact]
    public void Parse_RealRecordedFixture_FindsRedundantCastWarning()
    {
        var findings = _parser.Parse(Fixture("sample-inspection.xml"), SolutionDir);

        findings.Should().Contain(f =>
            f.Id == "RedundantCast" && f.Severity == ReSharperSeverity.Warning && f.Line == 12);
        findings.Should().OnlyContain(f => !string.IsNullOrEmpty(f.Id) && !string.IsNullOrEmpty(f.Message));
    }

    [Fact]
    public void Parse_PreservesNativeSeverity_SuggestionDistinctFromHint()
    {
        var findings = _parser.Parse(Fixture("parser-fixture.xml"), SolutionDir);

        // The crux of R5: suggestion and hint must NOT collapse together.
        findings.Should().Contain(f => f.Id == "UnusedMember.Global" && f.Severity == ReSharperSeverity.Suggestion);
        findings.Should().Contain(f => f.Id == "ArrangeThisQualifier" && f.Severity == ReSharperSeverity.Hint);
        findings.Should().Contain(f => f.Id == "CSharpErrors" && f.Severity == ReSharperSeverity.Error);
        findings.Should().Contain(f => f.Id == "RedundantCast" && f.Severity == ReSharperSeverity.Warning);

        findings.Select(f => f.Severity).Distinct().Should()
            .Contain(new[] { ReSharperSeverity.Suggestion, ReSharperSeverity.Hint });
    }

    [Fact]
    public void Parse_ExtractsCategoryHelpLinkAndAbsoluteFile()
    {
        var findings = _parser.Parse(Fixture("parser-fixture.xml"), SolutionDir);

        var cast = findings.First(f => f.Id == "RedundantCast" && f.Project == "ReSharperSampleApp");
        cast.Category.Should().Be("Redundancies in Code");
        cast.HelpLink.Should().Be("https://www.jetbrains.com/help/resharper/RedundantCast.html");
        cast.File.Should().Be(Path.GetFullPath(Path.Combine(SolutionDir, "Calculator.cs")));
        cast.Line.Should().Be(12);
        cast.Column.Should().BeNull(); // XML report has no column
    }

    [Fact]
    public void Parse_SolutionLevelFinding_HasNullFileAndLine()
    {
        var findings = _parser.Parse(Fixture("parser-fixture.xml"), SolutionDir);

        var solutionLevel = findings.Single(f => f.Id == "SolutionWideAnalysisDisabled");
        solutionLevel.File.Should().BeNull();
        solutionLevel.Line.Should().BeNull();
        solutionLevel.Project.Should().Be("OtherProject");
    }

    [Fact]
    public void Parse_OrdersByFileThenLineThenId_NullFilesLast()
    {
        var findings = _parser.Parse(Fixture("parser-fixture.xml"), SolutionDir);

        // Findings with a file come before the file-less solution-level finding.
        findings[^1].File.Should().BeNull();
        var withFile = findings.Where(f => f.File != null).ToList();
        withFile.Should().BeInAscendingOrder(f => f.File!);
    }

    [Fact]
    public void Parse_CountsAllIssuesAcrossProjects()
    {
        var findings = _parser.Parse(Fixture("parser-fixture.xml"), SolutionDir);
        findings.Should().HaveCount(7);
    }

    [Fact]
    public void Parse_MalformedXml_Throws()
    {
        Action act = () => _parser.Parse("<Report><Issues>", SolutionDir);
        act.Should().Throw<InspectionReportParseException>();
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        Action act = () => _parser.Parse("   ", SolutionDir);
        act.Should().Throw<InspectionReportParseException>();
    }

    [Fact]
    public void Parse_NonReportRoot_Throws()
    {
        Action act = () => _parser.Parse("<NotAReport/>", SolutionDir);
        act.Should().Throw<InspectionReportParseException>();
    }
}
