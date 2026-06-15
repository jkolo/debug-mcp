using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Runs <c>jb inspectcode</c> as a child process and returns the XML report text.
/// </summary>
public sealed class ReSharperCliRunner : IReSharperRunner
{
    private readonly ILogger<ReSharperCliRunner> _logger;

    public ReSharperCliRunner(ILogger<ReSharperCliRunner> logger) => _logger = logger;

    public async Task<string> RunInspectCodeAsync(InspectionRunRequest request, string jbPath, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debug-mcp-inspect-{Guid.NewGuid():N}.xml");

        var psi = new ProcessStartInfo
        {
            FileName = jbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("inspectcode");
        psi.ArgumentList.Add(request.Target);
        psi.ArgumentList.Add($"--output={outputPath}");
        psi.ArgumentList.Add("--format=Xml");
        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            psi.ArgumentList.Add($"--severity={request.Severity.ToUpperInvariant()}");
        }
        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            psi.ArgumentList.Add($"--project={request.Project}");
        }
        if (request.NoBuild)
        {
            psi.ArgumentList.Add("--no-build");
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
            {
                throw new ReSharperRunFailedException("Failed to start the ReSharper engine process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var combined = stdout.ToString() + stderr.ToString();

            if (process.ExitCode != 0)
            {
                if (LooksLikeBuildFailure(combined))
                {
                    throw new ReSharperBuildFailedException(
                        "The target failed to build before inspection.",
                        details: new { exitCode = process.ExitCode, output = Tail(combined) });
                }

                throw new ReSharperRunFailedException(
                    $"ReSharper inspectcode exited with code {process.ExitCode}.",
                    details: new { exitCode = process.ExitCode, output = Tail(combined) });
            }

            if (!File.Exists(outputPath))
            {
                throw new ReSharperRunFailedException(
                    "ReSharper inspectcode produced no report file.",
                    details: new { output = Tail(combined) });
            }

            return await File.ReadAllTextAsync(outputPath, cancellationToken);
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private static bool LooksLikeBuildFailure(string output) =>
        output.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase)
        || output.Contains(": error MSB", StringComparison.Ordinal)
        || output.Contains("Cannot restore", StringComparison.OrdinalIgnoreCase)
        || output.Contains("Could not load", StringComparison.OrdinalIgnoreCase);

    private static string Tail(string s, int max = 2000) =>
        s.Length <= max ? s : s[^max..];

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to kill ReSharper engine process");
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete temp report {Path}", path);
        }
    }
}
