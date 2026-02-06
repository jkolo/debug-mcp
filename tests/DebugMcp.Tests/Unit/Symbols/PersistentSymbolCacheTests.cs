using DebugMcp.Services.Symbols;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.Symbols;

public class PersistentSymbolCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PersistentSymbolCache _cache;

    public PersistentSymbolCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "debug-mcp-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _cache = new PersistentSymbolCache(_tempDir, NullLogger<PersistentSymbolCache>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Store_CreatesCorrectDirectoryLayout()
    {
        // Arrange
        var debugInfo = CreateDebugInfo("MyLib.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");
        var sourceFile = CreateTempPdb();

        // Act
        var cachedPath = _cache.Store(debugInfo, sourceFile);

        // Assert
        cachedPath.Should().Contain("MyLib.pdb");
        cachedPath.Should().Contain("aabbccdd11223344aabbccdd11223344FFFFFFFF");
        File.Exists(cachedPath).Should().BeTrue();

        // Verify exact layout: {cacheDir}/{pdbFileName}/{signature}/{pdbFileName}
        var expectedPath = Path.Combine(_tempDir, "MyLib.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF", "MyLib.pdb");
        cachedPath.Should().Be(expectedPath);

        File.Delete(sourceFile);
    }

    [Fact]
    public void TryGetPath_WhenCached_ReturnsCachedPath()
    {
        // Arrange
        var debugInfo = CreateDebugInfo("Cached.pdb", "11223344556677881122334455667788FFFFFFFF");
        var sourceFile = CreateTempPdb();
        _cache.Store(debugInfo, sourceFile);

        // Act
        var result = _cache.TryGetPath(debugInfo);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result!).Should().BeTrue();

        File.Delete(sourceFile);
    }

    [Fact]
    public void TryGetPath_WhenNotCached_ReturnsNull()
    {
        var debugInfo = CreateDebugInfo("Missing.pdb", "00000000000000000000000000000000FFFFFFFF");

        var result = _cache.TryGetPath(debugInfo);

        result.Should().BeNull();
    }

    [Fact]
    public void Store_SurvivesNewInstance()
    {
        // Arrange
        var debugInfo = CreateDebugInfo("Persistent.pdb", "aabb0000000000000000000000001111FFFFFFFF");
        var sourceFile = CreateTempPdb();
        _cache.Store(debugInfo, sourceFile);

        // Act — create a new cache instance pointing to the same directory
        var newCache = new PersistentSymbolCache(_tempDir, NullLogger<PersistentSymbolCache>.Instance);
        var result = newCache.TryGetPath(debugInfo);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result!).Should().BeTrue();

        File.Delete(sourceFile);
    }

    private static PeDebugInfo CreateDebugInfo(string pdbFileName, string symbolServerKey)
    {
        return new PeDebugInfo(
            PdbFileName: pdbFileName,
            PdbGuid: Guid.NewGuid(),
            Age: 1,
            Stamp: 0,
            IsPortablePdb: true,
            SymbolServerKey: symbolServerKey,
            ChecksumAlgorithm: null,
            Checksum: System.Collections.Immutable.ImmutableArray<byte>.Empty,
            HasEmbeddedPdb: false);
    }

    private static string CreateTempPdb()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[] { 0x42, 0x53, 0x4A, 0x42 }); // BSJB metadata signature
        return path;
    }
}
