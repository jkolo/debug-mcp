using DebugMcp.Services.Symbols;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.Symbols;

public class PeDebugInfoReaderTests
{
    private static readonly string TestAppDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DebugTestApp", "bin", "Debug", "net10.0"));

    private static readonly string TestAppDll = Path.Combine(TestAppDir, "DebugTestApp.dll");

    private readonly PeDebugInfoReader _reader = new(NullLogger<PeDebugInfoReader>.Instance);

    [Fact]
    public void TryReadDebugInfo_WithDebugTestApp_ReturnsCodeViewInfo()
    {
        // DebugTestApp has a local PDB built alongside it
        var result = _reader.TryReadDebugInfo(TestAppDll);

        result.Should().NotBeNull();
        result!.PdbFileName.Should().Be("DebugTestApp.pdb");
        result.PdbGuid.Should().NotBe(Guid.Empty);
        result.IsPortablePdb.Should().BeTrue("modern .NET builds produce Portable PDBs");
        result.SymbolServerKey.Should().EndWith("FFFFFFFF", "Portable PDB keys use FFFFFFFF suffix");
        result.SymbolServerKey.Should().HaveLength(40, "32 hex chars for GUID + 8 for FFFFFFFF");
    }

    [Fact]
    public void TryReadDebugInfo_WithNonPeFile_ReturnsNull()
    {
        // Use a text file — not a valid PE
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "This is not a PE file");
            var result = _reader.TryReadDebugInfo(tempFile);
            result.Should().BeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryReadDebugInfo_SymbolServerKey_HasCorrectFormat()
    {
        var result = _reader.TryReadDebugInfo(TestAppDll);

        result.Should().NotBeNull();

        // Key should be: 32 hex lowercase chars + "FFFFFFFF"
        var key = result!.SymbolServerKey;
        var guidPart = key[..32];
        var suffixPart = key[32..];

        guidPart.Should().MatchRegex("^[0-9a-f]{32}$", "GUID part should be 32 lowercase hex chars");
        suffixPart.Should().Be("FFFFFFFF");
    }

    [Fact]
    public void TryReadDebugInfo_WithSystemAssembly_DetectsEmbeddedPdb()
    {
        // Find a system assembly that has embedded PDB
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");

        if (!File.Exists(coreLibPath))
        {
            // Skip if runtime assembly not available in expected location
            return;
        }

        var result = _reader.TryReadDebugInfo(coreLibPath);

        // System.Private.CoreLib should have debug info
        // It may or may not have embedded PDB depending on the runtime build
        result.Should().NotBeNull();
        result!.PdbGuid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ValidatePdb_WithMatchingPdb_ReturnsTrue()
    {
        // DebugTestApp.dll should have a matching .pdb in the same directory
        var pdbPath = Path.ChangeExtension(TestAppDll, ".pdb");
        if (!File.Exists(pdbPath))
        {
            return; // Skip if PDB not built
        }

        var debugInfo = _reader.TryReadDebugInfo(TestAppDll);
        debugInfo.Should().NotBeNull();

        var isValid = _reader.ValidatePdb(pdbPath, debugInfo!);
        isValid.Should().BeTrue("the PDB built alongside the DLL should match");
    }

    [Fact]
    public void ValidatePdb_WithNonMatchingPdb_ReturnsFalse()
    {
        // Create a fake PDB file that's not valid
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });

            var debugInfo = _reader.TryReadDebugInfo(TestAppDll);
            debugInfo.Should().NotBeNull();

            var isValid = _reader.ValidatePdb(tempFile, debugInfo!);
            isValid.Should().BeFalse("a random byte file is not a valid PDB");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryExtractEmbeddedPdb_WithEmbeddedPdbAssembly_ExtractsValidPdb()
    {
        // Find a system assembly that has an embedded PDB
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");

        if (!File.Exists(coreLibPath))
        {
            return; // Skip if not available
        }

        var debugInfo = _reader.TryReadDebugInfo(coreLibPath);
        if (debugInfo == null || !debugInfo.HasEmbeddedPdb)
        {
            // Not all runtime builds have embedded PDBs, skip gracefully
            return;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"embedded-test-{Guid.NewGuid():N}.pdb");
        try
        {
            var result = _reader.TryExtractEmbeddedPdb(coreLibPath, outputPath);

            result.Should().BeTrue("assembly with HasEmbeddedPdb should extract successfully");
            File.Exists(outputPath).Should().BeTrue("output PDB file should be created");
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0, "extracted PDB should have content");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void TryExtractEmbeddedPdb_WithNoEmbeddedPdb_ReturnsFalse()
    {
        // DebugTestApp has a local PDB but not embedded
        var debugInfo = _reader.TryReadDebugInfo(TestAppDll);
        if (debugInfo != null && debugInfo.HasEmbeddedPdb)
        {
            return; // Skip if it happens to have embedded PDB
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"no-embedded-test-{Guid.NewGuid():N}.pdb");
        try
        {
            var result = _reader.TryExtractEmbeddedPdb(TestAppDll, outputPath);

            result.Should().BeFalse("assembly without embedded PDB should return false");
            File.Exists(outputPath).Should().BeFalse("no file should be created");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
