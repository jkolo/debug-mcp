using DebugMcp.Models.Modules;
using DebugMcp.Services.Symbols;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Integration;

/// <summary>
/// Integration tests that download real PDB files from public symbol servers.
/// Requires network access — filtered by [Trait("Category", "Integration")].
/// </summary>
[Trait("Category", "Integration")]
public class SymbolServerDownloadTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly PeDebugInfoReader _peReader;
    private readonly HttpClient _httpClient;

    public SymbolServerDownloadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "debug-mcp-dl-test-" + Guid.NewGuid().ToString("N")[..8]);
        _cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_cacheDir);

        _peReader = new PeDebugInfoReader(NullLogger<PeDebugInfoReader>.Instance);
        _httpClient = new HttpClient();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Downloads the PDB for System.Console.dll from Microsoft's symbol server.
    /// System.Console is a small runtime assembly always present on disk.
    /// </summary>
    [Fact]
    public async Task Download_SystemConsole_FromMicrosoftServer()
    {
        // Arrange: find a runtime assembly that is NOT too large and has debug info
        var runtimeDir = Path.GetDirectoryName(typeof(Console).Assembly.Location)!;
        var assemblyPath = Path.Combine(runtimeDir, "System.Console.dll");

        if (!File.Exists(assemblyPath))
        {
            return; // Skip if assembly not at expected path
        }

        var debugInfo = _peReader.TryReadDebugInfo(assemblyPath);
        if (debugInfo == null)
        {
            return; // Skip if no debug info
        }

        var options = new SymbolServerOptions { TimeoutSeconds = 30, MaxFileSizeMB = 50 };
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);

        var outputPath = Path.Combine(_tempDir, "System.Console.pdb");

        // Act: download from Microsoft's symbol server
        var downloaded = await client.TryDownloadAsync(
            SymbolServerOptions.DefaultMicrosoftServer, debugInfo, outputPath);

        // Assert
        if (!downloaded)
        {
            // PDB may not be available for this specific runtime build — not a failure
            return;
        }

        File.Exists(outputPath).Should().BeTrue("downloaded PDB should be written to disk");
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0, "PDB should have content");

        // Validate the downloaded PDB matches the assembly
        var isValid = _peReader.ValidatePdb(outputPath, debugInfo);
        isValid.Should().BeTrue("downloaded PDB GUID should match the assembly's CodeView GUID");
    }

    /// <summary>
    /// Downloads the PDB for System.Text.Json.dll from NuGet's symbol server.
    /// </summary>
    [Fact]
    public async Task Download_SystemTextJson_FromNuGetServer()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(System.Text.Json.JsonSerializer).Assembly.Location)!;
        var assemblyPath = Path.Combine(runtimeDir, "System.Text.Json.dll");

        if (!File.Exists(assemblyPath))
        {
            return;
        }

        var debugInfo = _peReader.TryReadDebugInfo(assemblyPath);
        if (debugInfo == null)
        {
            return;
        }

        var options = new SymbolServerOptions { TimeoutSeconds = 30, MaxFileSizeMB = 50 };
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);

        var outputPath = Path.Combine(_tempDir, "System.Text.Json.pdb");

        // Act: try NuGet symbol server
        var downloaded = await client.TryDownloadAsync(
            SymbolServerOptions.DefaultNuGetServer, debugInfo, outputPath);

        if (!downloaded)
        {
            return; // Not available on NuGet for this build
        }

        File.Exists(outputPath).Should().BeTrue();
        new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        _peReader.ValidatePdb(outputPath, debugInfo).Should().BeTrue(
            "downloaded PDB should match the assembly");
    }

    /// <summary>
    /// Full SymbolResolver end-to-end: resolves a runtime assembly PDB via the full chain.
    /// Uses real PeDebugInfoReader, PersistentSymbolCache, SymbolServerClient.
    /// </summary>
    [Fact]
    public async Task SymbolResolver_EndToEnd_ResolvesRuntimeAssembly()
    {
        // Pick a small runtime assembly
        var runtimeDir = Path.GetDirectoryName(typeof(Console).Assembly.Location)!;
        var assemblyPath = Path.Combine(runtimeDir, "System.Console.dll");

        if (!File.Exists(assemblyPath))
        {
            return;
        }

        var debugInfo = _peReader.TryReadDebugInfo(assemblyPath);
        if (debugInfo == null)
        {
            return;
        }

        // Skip if it has an embedded PDB (would resolve without network)
        // We want to test the server download path
        if (debugInfo.HasEmbeddedPdb)
        {
            return;
        }

        var options = new SymbolServerOptions
        {
            TimeoutSeconds = 30,
            MaxFileSizeMB = 50,
            CacheDirectory = _cacheDir,
            MaxConcurrentDownloads = 2
        };

        var persistentCache = new PersistentSymbolCache(_cacheDir, NullLogger<PersistentSymbolCache>.Instance);
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);
        var resolver = new SymbolResolver(_peReader, persistentCache, client, options, NullLogger<SymbolResolver>.Instance);

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert — the PDB should be resolved from one of the servers
        if (result.Status == SymbolStatus.NotFound)
        {
            // Some preview/custom runtime builds may not have PDBs on public servers
            return;
        }

        result.Status.Should().Be(SymbolStatus.Loaded);
        result.Source.Should().BeOneOf(SymbolSource.Server, SymbolSource.Embedded, SymbolSource.Local);
        result.PdbPath.Should().NotBeNull();
        File.Exists(result.PdbPath).Should().BeTrue();

        // Verify it was cached for next time
        if (result.Source == SymbolSource.Server)
        {
            var cachedPath = persistentCache.TryGetPath(debugInfo);
            cachedPath.Should().NotBeNull("server-downloaded PDB should be persisted to cache");
        }

        // Second resolve should hit cache (not re-download)
        var result2 = await resolver.ResolveAsync(assemblyPath);
        result2.Status.Should().Be(SymbolStatus.Loaded);
    }

    /// <summary>
    /// Verifies that the SSQP URL is correctly constructed and the server responds
    /// with either 200 or 404 (not 400/500 which would indicate a bad URL).
    /// </summary>
    [Theory]
    [InlineData("https://msdl.microsoft.com/download/symbols")]
    [InlineData("https://symbols.nuget.org/download/symbols")]
    public async Task SymbolServer_ReturnsValidHttpResponse(string serverUrl)
    {
        // Use a well-known runtime assembly to build a real SSQP URL
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");

        if (!File.Exists(coreLibPath))
        {
            return;
        }

        var debugInfo = _peReader.TryReadDebugInfo(coreLibPath);
        if (debugInfo == null)
        {
            return;
        }

        var options = new SymbolServerOptions { TimeoutSeconds = 15 };
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);

        var url = client.BuildDownloadUrl(serverUrl, debugInfo);

        // Act: HEAD request to check if the server responds correctly
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await _httpClient.SendAsync(request, cts.Token);

        // Assert: server should return 200 (found), 302 (redirect), 404 (not found),
        // or 403 (NuGet returns this for non-NuGet packages like runtime assemblies).
        // Should NOT return 400 (bad URL) or 500 (server error).
        var statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(
            [200, 302, 404, 403],
            $"SSQP URL should get a valid response, got {statusCode} for {url}");
    }

    /// <summary>
    /// Verifies that a non-existent PDB correctly returns 404 (not an error).
    /// </summary>
    [Fact]
    public async Task SymbolServer_NonExistentPdb_Returns404()
    {
        var fakePdbInfo = new PeDebugInfo(
            PdbFileName: "NonExistent.FakeAssembly.pdb",
            PdbGuid: Guid.NewGuid(),
            Age: 1,
            Stamp: 0,
            IsPortablePdb: true,
            SymbolServerKey: "00000000000000000000000000000000FFFFFFFF",
            ChecksumAlgorithm: null,
            Checksum: System.Collections.Immutable.ImmutableArray<byte>.Empty,
            HasEmbeddedPdb: false);

        var options = new SymbolServerOptions { TimeoutSeconds = 15 };
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);

        var outputPath = Path.Combine(_tempDir, "fake.pdb");

        // Act
        var downloaded = await client.TryDownloadAsync(
            SymbolServerOptions.DefaultMicrosoftServer, fakePdbInfo, outputPath);

        // Assert
        downloaded.Should().BeFalse("non-existent PDB should not be downloadable");
        File.Exists(outputPath).Should().BeFalse("no file should be created for 404");
    }

    /// <summary>
    /// Downloads PDBs for multiple runtime assemblies in parallel,
    /// verifying the concurrency limiter works with real network I/O.
    /// </summary>
    [Fact]
    public async Task SymbolResolver_ParallelDownloads_MultipleAssemblies()
    {
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        // Pick several small runtime assemblies
        var candidates = new[]
        {
            "System.Console.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.IO.dll"
        };

        var assemblyPaths = candidates
            .Select(name => Path.Combine(runtimeDir, name))
            .Where(File.Exists)
            .Where(path => _peReader.TryReadDebugInfo(path) is { HasEmbeddedPdb: false })
            .Take(3) // Limit to 3 to keep test fast
            .ToList();

        if (assemblyPaths.Count < 2)
        {
            return; // Need at least 2 assemblies for a parallel test
        }

        var options = new SymbolServerOptions
        {
            TimeoutSeconds = 30,
            MaxFileSizeMB = 50,
            CacheDirectory = _cacheDir,
            MaxConcurrentDownloads = 2
        };

        var persistentCache = new PersistentSymbolCache(_cacheDir, NullLogger<PersistentSymbolCache>.Instance);
        var client = new SymbolServerClient(_httpClient, options, NullLogger<SymbolServerClient>.Instance);
        var resolver = new SymbolResolver(_peReader, persistentCache, client, options, NullLogger<SymbolResolver>.Instance);

        // Act: resolve all in parallel
        var tasks = assemblyPaths.Select(p => resolver.ResolveAsync(p)).ToList();
        var results = await Task.WhenAll(tasks);

        // Assert: all should complete without exceptions
        results.Should().AllSatisfy(r =>
        {
            r.Status.Should().BeOneOf([SymbolStatus.Loaded, SymbolStatus.NotFound],
                "each resolution should complete with a definitive status");
        });

        // At least check that the resolver tracked statuses
        foreach (var path in assemblyPaths)
        {
            var status = resolver.GetStatus(path);
            status.Should().NotBe(SymbolStatus.None, $"status for {Path.GetFileName(path)} should be resolved");
            status.Should().NotBe(SymbolStatus.Downloading, "download should have completed");
        }
    }
}
