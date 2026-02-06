using System.Diagnostics;
using System.Text.Json;

namespace DebugMcp.Tests.Unit;

public class CliArgumentTests
{
    private static readonly string ProjectPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "DebugMcp", "DebugMcp.csproj"));

    private static readonly string Configuration =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunToolAsync(params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet", ["run", "--project", ProjectPath, "--no-build", "-c", Configuration, "--", .. args])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout.Trim(), stderr.Trim());
    }

    [Fact]
    public async Task Version_Flag_Displays_Version_And_Exits_With_Zero()
    {
        var (exitCode, stdout, _) = await RunToolAsync("--version");

        exitCode.Should().Be(0);
        stdout.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public async Task Help_Flag_Displays_Usage_And_Exits_With_Zero()
    {
        var (exitCode, stdout, _) = await RunToolAsync("--help");

        exitCode.Should().Be(0);
        stdout.Should().Contain("MCP server for debugging .NET applications");
        stdout.Should().Contain("--help");
        stdout.Should().Contain("--version");
    }

    [Fact]
    public async Task Unknown_Argument_Exits_With_NonZero()
    {
        var (exitCode, _, stderr) = await RunToolAsync("--bogus");

        exitCode.Should().NotBe(0);
        stderr.Should().Contain("--bogus");
    }

    [Fact]
    public async Task Stderr_Logging_Flag_Is_Recognized()
    {
        var (exitCode, stdout, _) = await RunToolAsync("--help");

        exitCode.Should().Be(0);
        stdout.Should().Contain("--stderr-logging");
        stdout.Should().Contain("-s");
        stdout.Should().Contain("stderr");
    }

    [Theory]
    [InlineData("--stderr-logging")]
    [InlineData("-s")]
    public async Task Stderr_Logging_Flag_Does_Not_Cause_Error(string flag)
    {
        var psi = new ProcessStartInfo("dotnet", ["run", "--project", ProjectPath, "--no-build", "-c", Configuration, "--", flag])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;

        // Give it a moment to start
        await Task.Delay(2000);

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            // Process was still running = flag accepted and server started
            return;
        }

        // If it exited, it should not be due to an argument error
        var stderr = await process.StandardError.ReadToEndAsync();
        stderr.Should().NotContain("Unrecognized")
            .And.NotContain("Unknown")
            .And.NotContain("Invalid");
    }

    [Fact]
    public async Task NoRoslyn_Flag_Excludes_Code_Tools()
    {
        var toolsWithFlag = await GetToolNamesAsync("--no-roslyn");
        var toolsWithoutFlag = await GetToolNamesAsync();

        // With --no-roslyn: no code_* tools
        toolsWithFlag.Should().NotContain(t => t.StartsWith("code_"),
            "code_* tools should be excluded when --no-roslyn is set");

        // Without flag: code_* tools are present
        toolsWithoutFlag.Should().Contain("code_load")
            .And.Contain("code_find_usages")
            .And.Contain("code_find_assignments")
            .And.Contain("code_goto_definition")
            .And.Contain("code_get_diagnostics");
    }

    private async Task<List<string>> GetToolNamesAsync(params string[] extraArgs)
    {
        var allArgs = new List<string> { "run", "--project", ProjectPath, "--no-build", "-c", Configuration, "--" };
        allArgs.AddRange(extraArgs);

        var psi = new ProcessStartInfo("dotnet", allArgs)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;

        // Send MCP initialize + tools/list
        var initMsg = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""";
        var notifyMsg = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
        var listMsg = """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""";

        await process.StandardInput.WriteLineAsync(initMsg);
        await process.StandardInput.WriteLineAsync(notifyMsg);
        await process.StandardInput.WriteLineAsync(listMsg);
        await process.StandardInput.FlushAsync();

        // Read responses until we get the tools/list result (id: 2)
        var toolNames = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        while (!cts.Token.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(cts.Token);
            if (line == null) break;

            if (line.Contains("\"id\":2") && line.Contains("tools"))
            {
                using var doc = JsonDocument.Parse(line);
                var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
                foreach (var tool in tools.EnumerateArray())
                {
                    toolNames.Add(tool.GetProperty("name").GetString()!);
                }
                break;
            }
        }

        process.Kill(entireProcessTree: true);
        return toolNames;
    }

    [Fact]
    public async Task No_Arguments_Starts_MCP_Server()
    {
        var psi = new ProcessStartInfo("dotnet", ["run", "--project", ProjectPath, "--no-build", "-c", Configuration])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;

        // Give it a moment to start, then kill — we just need to confirm it doesn't exit immediately with error
        await Task.Delay(2000);

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            // Process was still running = MCP server started successfully
            return;
        }

        // If it exited early, it should be due to stdin EOF (expected behavior for MCP server)
        // With MCP logging enabled, stderr is empty unless --stderr-logging is used
        // Exit code 0 indicates normal shutdown
        process.ExitCode.Should().Be(0, "MCP server should exit cleanly when stdin closes");
    }
}
