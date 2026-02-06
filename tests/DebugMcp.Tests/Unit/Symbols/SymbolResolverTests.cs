using DebugMcp.Models.Modules;
using DebugMcp.Services.Symbols;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DebugMcp.Tests.Unit.Symbols;

public class SymbolResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly Mock<PeDebugInfoReader> _mockPeReader;
    private readonly Mock<SymbolServerClient> _mockServerClient;
    private readonly PersistentSymbolCache _persistentCache;

    public SymbolResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "debug-mcp-resolver-test-" + Guid.NewGuid().ToString("N")[..8]);
        _cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_cacheDir);

        _mockPeReader = new Mock<PeDebugInfoReader>(NullLogger<PeDebugInfoReader>.Instance);
        _mockServerClient = new Mock<SymbolServerClient>(
            new HttpClient(), new SymbolServerOptions(), NullLogger<SymbolServerClient>.Instance);
        _persistentCache = new PersistentSymbolCache(_cacheDir, NullLogger<PersistentSymbolCache>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_LocalPdbFound_ReturnsLoadedLocal()
    {
        // Arrange: create an assembly + PDB pair
        var assemblyDir = Path.Combine(_tempDir, "local");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Test.dll");
        var pdbPath = Path.Combine(assemblyDir, "Test.pdb");
        File.WriteAllBytes(assemblyPath, [0]);
        File.WriteAllBytes(pdbPath, [0x42]);

        var debugInfo = CreateDebugInfo("Test.pdb");
        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.Loaded);
        result.Source.Should().Be(SymbolSource.Local);
        result.PdbPath.Should().Be(pdbPath);
    }

    [Fact]
    public async Task ResolveAsync_CacheHit_ReturnsLoadedCache()
    {
        // Arrange: put PDB in persistent cache, no local PDB
        var debugInfo = CreateDebugInfo("Cached.pdb");
        var sourcePdb = Path.Combine(_tempDir, "source.pdb");
        File.WriteAllBytes(sourcePdb, [0x42]);
        _persistentCache.Store(debugInfo, sourcePdb);

        var assemblyDir = Path.Combine(_tempDir, "nocache");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Cached.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.Loaded);
        result.Source.Should().Be(SymbolSource.Cache);
    }

    [Fact]
    public async Task ResolveAsync_ServerDownloadSucceeds_ReturnsLoadedServerAndCaches()
    {
        // Arrange: no local, no cache, server succeeds
        var assemblyDir = Path.Combine(_tempDir, "server");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Remote.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        var debugInfo = CreateDebugInfo("Remote.pdb");
        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);
        _mockPeReader.Setup(r => r.ValidatePdb(It.IsAny<string>(), debugInfo)).Returns(true);

        _mockServerClient.Setup(c => c.TryDownloadAsync(
            It.IsAny<string>(), debugInfo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, PeDebugInfo, string, CancellationToken>((_, _, outputPath, _) =>
            {
                File.WriteAllBytes(outputPath, [0x42]);
                return Task.FromResult(true);
            });

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.Loaded);
        result.Source.Should().Be(SymbolSource.Server);
        result.PdbPath.Should().NotBeNull();

        // Verify PDB was cached
        var cachedPath = _persistentCache.TryGetPath(debugInfo);
        cachedPath.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_AllSourcesFail_ReturnsNotFound()
    {
        // Arrange: no local, no cache, no debug info (can't query servers)
        var assemblyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Nothing.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns((PeDebugInfo?)null);

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.NotFound);
        result.Source.Should().Be(SymbolSource.None);
    }

    [Fact]
    public async Task ResolveAsync_ServerDownloadFailsValidation_ReturnsNotFound()
    {
        // Arrange: server downloads PDB but GUID doesn't match
        var assemblyDir = Path.Combine(_tempDir, "badpdb");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Bad.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        var debugInfo = CreateDebugInfo("Bad.pdb");
        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);
        _mockPeReader.Setup(r => r.ValidatePdb(It.IsAny<string>(), debugInfo)).Returns(false);

        _mockServerClient.Setup(c => c.TryDownloadAsync(
            It.IsAny<string>(), debugInfo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, PeDebugInfo, string, CancellationToken>((_, _, outputPath, _) =>
            {
                File.WriteAllBytes(outputPath, [0x42]);
                return Task.FromResult(true);
            });

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.NotFound);
    }

    [Fact]
    public async Task GetStatus_ReturnsCurrentStatus()
    {
        var assemblyDir = Path.Combine(_tempDir, "status");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Status.dll");
        var pdbPath = Path.Combine(assemblyDir, "Status.pdb");
        File.WriteAllBytes(assemblyPath, [0]);
        File.WriteAllBytes(pdbPath, [0x42]);

        var debugInfo = CreateDebugInfo("Status.pdb");
        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);

        var resolver = CreateResolver();

        // Before resolution
        resolver.GetStatus(assemblyPath).Should().Be(SymbolStatus.None);

        // After resolution
        await resolver.ResolveAsync(assemblyPath);
        resolver.GetStatus(assemblyPath).Should().Be(SymbolStatus.Loaded);
    }

    [Fact]
    public async Task ResolveAsync_RespectsMaxConcurrentDownloads()
    {
        // Arrange: create multiple assemblies that all need server download
        var options = new SymbolServerOptions { MaxConcurrentDownloads = 2 };
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        _mockServerClient.Setup(c => c.TryDownloadAsync(
            It.IsAny<string>(), It.IsAny<PeDebugInfo>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, PeDebugInfo, string, CancellationToken>(async (_, _, outputPath, ct) =>
            {
                lock (lockObj)
                {
                    concurrentCount++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
                }

                await Task.Delay(100, ct);

                lock (lockObj)
                {
                    concurrentCount--;
                }

                File.WriteAllBytes(outputPath, [0x42]);
                return true;
            });

        var resolver = CreateResolver(options);
        var tasks = new List<Task>();

        for (var i = 0; i < 4; i++)
        {
            var assemblyDir = Path.Combine(_tempDir, $"concurrent-{i}");
            Directory.CreateDirectory(assemblyDir);
            var assemblyPath = Path.Combine(assemblyDir, $"Lib{i}.dll");
            File.WriteAllBytes(assemblyPath, [0]);

            var debugInfo = CreateDebugInfo($"Lib{i}.pdb");
            _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);
            _mockPeReader.Setup(r => r.ValidatePdb(It.IsAny<string>(), debugInfo)).Returns(true);

            tasks.Add(resolver.ResolveAsync(assemblyPath));
        }

        await Task.WhenAll(tasks);

        maxConcurrent.Should().BeLessOrEqualTo(2, "concurrency should be limited to MaxConcurrentDownloads");
    }

    [Fact]
    public async Task ResolveAsync_EmbeddedPdbFound_ReturnsLoadedEmbedded()
    {
        // Arrange: no local PDB, assembly has embedded PDB
        var assemblyDir = Path.Combine(_tempDir, "embedded");
        Directory.CreateDirectory(assemblyDir);
        var assemblyPath = Path.Combine(assemblyDir, "Embedded.dll");
        File.WriteAllBytes(assemblyPath, [0]);

        var debugInfo = new PeDebugInfo(
            PdbFileName: "Embedded.pdb",
            PdbGuid: Guid.NewGuid(),
            Age: 1,
            Stamp: 0,
            IsPortablePdb: true,
            SymbolServerKey: Guid.NewGuid().ToString("N") + "FFFFFFFF",
            ChecksumAlgorithm: null,
            Checksum: System.Collections.Immutable.ImmutableArray<byte>.Empty,
            HasEmbeddedPdb: true);

        _mockPeReader.Setup(r => r.TryReadDebugInfo(assemblyPath)).Returns(debugInfo);
        _mockPeReader.Setup(r => r.TryExtractEmbeddedPdb(assemblyPath, It.IsAny<string>()))
            .Returns<string, string>((_, outputPath) =>
            {
                File.WriteAllBytes(outputPath, [0x42]);
                return true;
            });

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync(assemblyPath);

        // Assert
        result.Status.Should().Be(SymbolStatus.Loaded);
        result.Source.Should().Be(SymbolSource.Embedded);
        result.PdbPath.Should().NotBeNull();

        // Verify it was cached
        var cachedPath = _persistentCache.TryGetPath(debugInfo);
        cachedPath.Should().NotBeNull("embedded PDB should be stored in cache");
    }

    private SymbolResolver CreateResolver(SymbolServerOptions? options = null)
    {
        return new SymbolResolver(
            _mockPeReader.Object,
            _persistentCache,
            _mockServerClient.Object,
            options ?? new SymbolServerOptions(),
            NullLogger<SymbolResolver>.Instance);
    }

    private static PeDebugInfo CreateDebugInfo(string pdbFileName)
    {
        return new PeDebugInfo(
            PdbFileName: pdbFileName,
            PdbGuid: Guid.NewGuid(),
            Age: 1,
            Stamp: 0,
            IsPortablePdb: true,
            SymbolServerKey: Guid.NewGuid().ToString("N") + "FFFFFFFF",
            ChecksumAlgorithm: null,
            Checksum: System.Collections.Immutable.ImmutableArray<byte>.Empty,
            HasEmbeddedPdb: false);
    }
}
