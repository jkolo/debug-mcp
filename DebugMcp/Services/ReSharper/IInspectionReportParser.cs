using DebugMcp.Models.ReSharper;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Pure parser: ReSharper InspectCode report text (XML) → findings, with NATIVE severity
/// preserved (suggestion ≠ hint). No I/O, no clock. XML is used rather than SARIF because the
/// SARIF output collapses suggestion and hint into the coarse <c>note</c> level (verified
/// against real engine output — see research R5); the XML <c>&lt;IssueType Severity="…"/&gt;</c>
/// attribute carries the native value unambiguously.
/// </summary>
public interface IInspectionReportParser
{
    /// <summary>
    /// Parses the report. The <paramref name="solutionDir"/> resolves relative file paths to
    /// absolute. Findings are returned ordered by file, then line, then id.
    /// </summary>
    /// <exception cref="InspectionReportParseException">document is not a valid report.</exception>
    IReadOnlyList<InspectionFinding> Parse(string reportXml, string solutionDir);
}
