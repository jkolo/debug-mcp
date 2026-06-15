using System.Xml.Linq;
using DebugMcp.Models.ReSharper;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Pure parser for the ReSharper InspectCode XML report. Preserves native severity
/// (suggestion ≠ hint) via the <c>&lt;IssueType Severity="…"/&gt;</c> attribute. See R5: the
/// SARIF output collapses suggestion/hint to <c>note</c>, so XML is the source of truth.
/// </summary>
public sealed class InspectionReportParser : IInspectionReportParser
{
    public IReadOnlyList<InspectionFinding> Parse(string reportXml, string solutionDir)
    {
        if (string.IsNullOrWhiteSpace(reportXml))
        {
            throw new InspectionReportParseException("Inspection report was empty.");
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(reportXml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InspectionReportParseException("Inspection report is not valid XML.", inner: ex);
        }

        var report = doc.Root;
        if (report is null || report.Name.LocalName != "Report")
        {
            throw new InspectionReportParseException("Inspection report is missing the <Report> root element.");
        }

        // Build the issue-type lookup: id → (severity, category, help link).
        var issueTypes = new Dictionary<string, IssueTypeInfo>(StringComparer.Ordinal);
        foreach (var it in report.Element("IssueTypes")?.Elements("IssueType") ?? [])
        {
            var id = (string?)it.Attribute("Id");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            issueTypes[id] = new IssueTypeInfo(
                Severity: ParseSeverity((string?)it.Attribute("Severity")),
                Category: (string?)it.Attribute("Category"),
                HelpLink: (string?)it.Attribute("WikiUrl"));
        }

        var findings = new List<InspectionFinding>();
        foreach (var project in report.Element("Issues")?.Elements("Project") ?? [])
        {
            var projectName = (string?)project.Attribute("Name");
            foreach (var issue in project.Elements("Issue"))
            {
                var typeId = (string?)issue.Attribute("TypeId");
                if (string.IsNullOrEmpty(typeId))
                {
                    continue;
                }

                var info = issueTypes.TryGetValue(typeId, out var ti) ? ti : IssueTypeInfo.Unknown;
                var file = NormalizeFile((string?)issue.Attribute("File"), solutionDir);

                findings.Add(new InspectionFinding
                {
                    Id = typeId,
                    Message = (string?)issue.Attribute("Message") ?? string.Empty,
                    Severity = info.Severity,
                    Category = info.Category,
                    File = file,
                    Line = ParseLine((string?)issue.Attribute("Line")),
                    Column = null,      // XML report carries Offset (char range), not column
                    EndLine = null,
                    EndColumn = null,
                    Project = projectName,
                    HelpLink = info.HelpLink
                });
            }
        }

        // Deterministic ordering: file (nulls last), then line, then id.
        return findings
            .OrderBy(f => f.File is null)
            .ThenBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line ?? int.MaxValue)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static ReSharperSeverity ParseSeverity(string? raw) => raw?.ToUpperInvariant() switch
    {
        "ERROR" => ReSharperSeverity.Error,
        "WARNING" => ReSharperSeverity.Warning,
        "SUGGESTION" => ReSharperSeverity.Suggestion,
        "HINT" => ReSharperSeverity.Hint,
        // ReSharper only emits the four levels above; treat anything else as Warning so an
        // unexpected value is never silently dropped below a typical threshold.
        _ => ReSharperSeverity.Warning
    };

    private static int? ParseLine(string? raw) =>
        int.TryParse(raw, out var line) && line > 0 ? line : null;

    private static string? NormalizeFile(string? file, string solutionDir)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        var normalized = file.Replace('\\', '/');
        return Path.GetFullPath(Path.Combine(solutionDir, normalized));
    }

    private readonly record struct IssueTypeInfo(ReSharperSeverity Severity, string? Category, string? HelpLink)
    {
        public static readonly IssueTypeInfo Unknown = new(ReSharperSeverity.Warning, null, null);
    }
}
