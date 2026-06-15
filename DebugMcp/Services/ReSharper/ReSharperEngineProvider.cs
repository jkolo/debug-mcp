using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.ReSharper;

/// <summary>
/// Acquires and caches the pinned ReSharper command-line engine via an isolated
/// <c>dotnet tool install --tool-path</c>, mirroring the symbol-server cache pattern.
/// Lazy (first-use), idempotent, and safe against concurrent first-run installs.
/// </summary>
public sealed class ReSharperEngineProvider : IReSharperEngineProvider
{
    private static readonly SemaphoreSlim InstallGate = new(1, 1);

    private readonly ReSharperOptions _options;
    private readonly ILogger<ReSharperEngineProvider> _logger;

    public ReSharperEngineProvider(ReSharperOptions options, ILogger<ReSharperEngineProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    private string ToolPath => _options.EngineToolPath;
    private string JbShim => Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "jb.exe" : "jb");
    private string Marker => Path.Combine(ToolPath, ".installed");

    public async Task<EngineInstallState> EnsureEngineAsync(CancellationToken cancellationToken)
    {
        if (IsReady())
        {
            _logger.LogDebug("ReSharper engine cache hit at {ToolPath}", ToolPath);
            return new EngineInstallState(JbShim, _options.Version, Acquired: false);
        }

        await InstallGate.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the in-process gate.
            if (IsReady())
            {
                return new EngineInstallState(JbShim, _options.Version, Acquired: false);
            }

            await EnsureDotnetAvailableAsync(cancellationToken);
            CleanPartialInstall();
            await InstallEngineAsync(cancellationToken);

            if (!File.Exists(JbShim))
            {
                throw new ReSharperAcquisitionException(
                    "ReSharper engine install completed but the 'jb' tool was not found.",
                    details: new { toolPath = ToolPath });
            }

            WriteMarker();
            _logger.LogInformation("ReSharper engine acquired ({Version}) at {ToolPath}", _options.Version, ToolPath);
            return new EngineInstallState(JbShim, _options.Version, Acquired: true);
        }
        finally
        {
            InstallGate.Release();
        }
    }

    private bool IsReady() => File.Exists(JbShim) && File.Exists(Marker);

    private void CleanPartialInstall()
    {
        if (Directory.Exists(ToolPath) && !IsReady())
        {
            _logger.LogWarning("Removing partial/corrupt ReSharper engine install at {ToolPath}", ToolPath);
            try
            {
                Directory.Delete(ToolPath, recursive: true);
            }
            catch (IOException ex)
            {
                throw new ReSharperAcquisitionException(
                    $"Could not clean a partial engine install at '{ToolPath}'.",
                    details: new { toolPath = ToolPath, hint = "Delete the directory manually or pass --no-resharper." },
                    inner: ex);
            }
        }
    }

    private async Task EnsureDotnetAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (exit, _, _) = await RunProcessAsync("dotnet", ["--version"], cancellationToken);
            if (exit != 0)
            {
                throw new ReSharperPrerequisiteException(
                    "The .NET SDK ('dotnet') is required to acquire the ReSharper engine but returned an error.",
                    details: new { hint = "Install the .NET SDK, or pass --no-resharper to disable ReSharper tools." });
            }
        }
        catch (Win32Exception ex)
        {
            throw new ReSharperPrerequisiteException(
                "The .NET SDK ('dotnet') was not found on PATH; it is required to acquire the ReSharper engine.",
                details: new { hint = "Install the .NET SDK, or pass --no-resharper to disable ReSharper tools." },
                inner: ex);
        }
    }

    private async Task InstallEngineAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(ToolPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReSharperAcquisitionException(
                $"Cannot create the engine cache directory '{ToolPath}'.",
                details: new { toolPath = ToolPath, hint = "Check permissions or set --resharper-cache to a writable path." },
                inner: ex);
        }

        _logger.LogInformation("Acquiring ReSharper engine {Package} {Version}…", ReSharperOptions.PackageId, _options.Version);

        string[] args =
        [
            "tool", "install", ReSharperOptions.PackageId,
            "--tool-path", ToolPath,
            "--version", _options.Version
        ];

        int exit;
        string stdout, stderr;
        try
        {
            (exit, stdout, stderr) = await RunProcessAsync("dotnet", args, cancellationToken);
        }
        catch (Win32Exception ex)
        {
            throw new ReSharperPrerequisiteException(
                "The .NET SDK ('dotnet') was not found on PATH.",
                details: new { hint = "Install the .NET SDK, or pass --no-resharper." },
                inner: ex);
        }

        if (exit != 0)
        {
            var output = (stdout + stderr);
            throw new ReSharperAcquisitionException(
                "Failed to download/install the ReSharper engine.",
                details: new
                {
                    exitCode = exit,
                    output = output.Length > 2000 ? output[^2000..] : output,
                    hint = "Check network connectivity and NuGet access, or pass --no-resharper to disable ReSharper tools."
                });
        }
    }

    private void WriteMarker()
    {
        try
        {
            File.WriteAllText(Marker, _options.Version);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Failed to write engine marker {Marker}", Marker);
        }
    }

    private static async Task<(int exit, string stdout, string stderr)> RunProcessAsync(
        string fileName, string[] args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
