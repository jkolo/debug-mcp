using System.Diagnostics;
using System.Text.RegularExpressions;
using DebugMcp.Models.ReSharper;
using DebugMcp.Services.Progress;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Orchestrates a ReSharper inspection: ensure engine (lazy install, acquisition budget) →
/// run inspectcode (inspection budget) → parse → filter by severity → sort, count, cap.
/// </summary>
public sealed partial class ReSharperInspectionService : IReSharperInspectionService
{
    private readonly IReSharperEngineProvider _engineProvider;
    private readonly IReSharperRunner _runner;
    private readonly IInspectionReportParser _parser;
    private readonly ReSharperOptions _options;
    private readonly ILogger<ReSharperInspectionService> _logger;

    public ReSharperInspectionService(
        IReSharperEngineProvider engineProvider,
        IReSharperRunner runner,
        IInspectionReportParser parser,
        ReSharperOptions options,
        ILogger<ReSharperInspectionService> logger)
    {
        _engineProvider = engineProvider;
        _runner = runner;
        _parser = parser;
        _options = options;
        _logger = logger;
    }

    public async Task<InspectionResult> InspectAsync(
        string target,
        string? severity,
        string? project,
        bool noBuild,
        int inspectionTimeoutSeconds,
        int maxResults,
        CancellationToken cancellationToken,
        IProgressReporter? progress = null)
    {
        var threshold = ParseSeverityThreshold(severity); // throws ArgumentException on bad value

        // Scope validation for a solution-scoped project filter.
        if (!string.IsNullOrWhiteSpace(project)
            && target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            && !SolutionContainsProject(target, project))
        {
            throw new ReSharperProjectNotFoundException(
                $"Project '{project}' was not found in solution '{Path.GetFileName(target)}'.",
                details: new { project });
        }

        // Phase 1 — acquire engine (separate, longer budget).
        EngineInstallState engine;
        using (var acqCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            acqCts.CancelAfter(TimeSpan.FromSeconds(_options.AcquisitionTimeoutSeconds));
            try
            {
                engine = await HeartbeatProgress.RunAsync(
                    () => _engineProvider.EnsureEngineAsync(acqCts.Token),
                    progress, "acquiring engine", cancellationToken: acqCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ReSharperTimeoutException("acquisition",
                    $"Acquiring the ReSharper engine exceeded {_options.AcquisitionTimeoutSeconds}s.");
            }
        }

        // Phase 2 — run inspection (per-call budget).
        var stopwatch = Stopwatch.StartNew();
        string reportXml;
        using (var inspCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            inspCts.CancelAfter(TimeSpan.FromSeconds(inspectionTimeoutSeconds));
            try
            {
                reportXml = await HeartbeatProgress.RunAsync(
                    () => _runner.RunInspectCodeAsync(
                        new InspectionRunRequest(target, severity, project, noBuild),
                        engine.JbPath,
                        inspCts.Token),
                    progress, "running inspection", cancellationToken: inspCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ReSharperTimeoutException("inspection",
                    $"The inspection exceeded {inspectionTimeoutSeconds}s.");
            }
        }

        progress?.ReportStage("parsing report");
        var solutionDir = Path.GetDirectoryName(Path.GetFullPath(target)) ?? Directory.GetCurrentDirectory();
        var all = _parser.Parse(reportXml, solutionDir);

        // Severity filtering (engine also filters via --severity; this guarantees the contract).
        var filtered = threshold is { } min
            ? all.Where(f => f.Severity >= min).ToList()
            : all;

        stopwatch.Stop();

        var totalCount = filtered.Count;
        var returned = filtered.Take(maxResults).ToList();
        var summary = returned
            .GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key.ToString().ToLowerInvariant(), g => g.Count());

        return new InspectionResult
        {
            Target = Path.GetFullPath(target),
            Findings = returned,
            TotalCount = totalCount,
            ReturnedCount = returned.Count,
            Truncated = totalCount > returned.Count,
            MaxResults = maxResults,
            Summary = summary,
            EngineVersion = engine.Version,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Built = !noBuild
        };
    }

    private static ReSharperSeverity? ParseSeverityThreshold(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return null;
        }

        return severity.Trim().ToUpperInvariant() switch
        {
            "ERROR" => ReSharperSeverity.Error,
            "WARNING" => ReSharperSeverity.Warning,
            "SUGGESTION" => ReSharperSeverity.Suggestion,
            "HINT" => ReSharperSeverity.Hint,
            _ => throw new ArgumentException(
                $"Invalid severity '{severity}'. Valid values: error, warning, suggestion, hint.")
        };
    }

    private static bool SolutionContainsProject(string slnPath, string projectName)
    {
        string text;
        try
        {
            text = File.ReadAllText(slnPath);
        }
        catch (IOException)
        {
            // If we cannot read the solution we cannot disprove the project; let the engine decide.
            return true;
        }

        // Classic .sln: Project("{GUID}") = "Name", "relative\path.csproj", "{GUID}"
        foreach (Match m in ProjectLineRegex().Matches(text))
        {
            var name = m.Groups["name"].Value;
            var path = m.Groups["path"].Value;
            if (string.Equals(name, projectName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileNameWithoutExtension(path), projectName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(
        """
        Project\("\{[^}]+\}"\)\s*=\s*"(?<name>[^"]+)",\s*"(?<path>[^"]+)"
        """)]
    private static partial Regex ProjectLineRegex();
}
