using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// Reads PE debug directory entries to extract PDB identification and embedded PDB data.
/// </summary>
public class PeDebugInfoReader
{
    private readonly ILogger<PeDebugInfoReader> _logger;

    public PeDebugInfoReader(ILogger<PeDebugInfoReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Reads debug information from a PE assembly's debug directory.
    /// </summary>
    /// <returns>PeDebugInfo if the assembly has a CodeView debug entry, null otherwise.</returns>
    public virtual PeDebugInfo? TryReadDebugInfo(string assemblyPath)
    {
        try
        {
            using var peStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(peStream);

            var entries = peReader.ReadDebugDirectory();

            // Find CodeView entry (required for PDB identification)
            var cvEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.CodeView);
            if (cvEntry.DataSize == 0)
            {
                _logger.LogDebug("No CodeView debug entry in {AssemblyPath}", assemblyPath);
                return null;
            }

            var cv = peReader.ReadCodeViewDebugDirectoryData(cvEntry);
            if (cv.Guid == Guid.Empty)
            {
                _logger.LogDebug("Empty PDB GUID in {AssemblyPath}", assemblyPath);
                return null;
            }

            var pdbFileName = Path.GetFileName(cv.Path);
            var isPortable = cvEntry.IsPortableCodeView;

            // Construct symbol server key
            var symbolServerKey = isPortable
                ? $"{cv.Guid:N}FFFFFFFF"
                : $"{cv.Guid:N}{cv.Age:x}";

            // Read PDB checksum if available
            string? checksumAlgorithm = null;
            var checksum = ImmutableArray<byte>.Empty;
            var csEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.PdbChecksum);
            if (csEntry.DataSize > 0)
            {
                var cs = peReader.ReadPdbChecksumDebugDirectoryData(csEntry);
                checksumAlgorithm = cs.AlgorithmName;
                checksum = cs.Checksum;
            }

            // Check for embedded PDB
            var hasEmbeddedPdb = entries.Any(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);

            var result = new PeDebugInfo(
                PdbFileName: pdbFileName,
                PdbGuid: cv.Guid,
                Age: cv.Age,
                Stamp: cvEntry.Stamp,
                IsPortablePdb: isPortable,
                SymbolServerKey: symbolServerKey,
                ChecksumAlgorithm: checksumAlgorithm,
                Checksum: checksum,
                HasEmbeddedPdb: hasEmbeddedPdb);

            _logger.LogDebug("Read debug info for {AssemblyPath}: PDB={PdbFileName}, Portable={IsPortable}, Embedded={HasEmbedded}",
                assemblyPath, pdbFileName, isPortable, hasEmbeddedPdb);

            return result;
        }
        catch (BadImageFormatException ex)
        {
            _logger.LogDebug(ex, "Not a valid PE file: {AssemblyPath}", assemblyPath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read PE file: {AssemblyPath}", assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Extracts an embedded Portable PDB from a PE assembly and writes it to the output path.
    /// </summary>
    /// <returns>True if an embedded PDB was found and extracted, false otherwise.</returns>
    public virtual bool TryExtractEmbeddedPdb(string assemblyPath, string outputPath)
    {
        try
        {
            using var peStream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(peStream);

            var entries = peReader.ReadDebugDirectory();
            var embEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
            if (embEntry.DataSize == 0)
            {
                return false;
            }

            // Re-read the raw embedded PDB data from the PE to write to disk.
            // The debug directory entry points to: 4-byte signature "MPDB" + 4-byte uncompressed size + Deflate data.
            // We decompress using DeflateStream to get the raw Portable PDB bytes.
            var embeddedData = peReader.GetSectionData(embEntry.DataRelativeVirtualAddress);
            var embeddedReader = embeddedData.GetReader();

            var signature = embeddedReader.ReadUInt32(); // "MPDB"
            var uncompressedSize = embeddedReader.ReadInt32();

            var compressedData = embeddedReader.ReadBytes(embEntry.DataSize - 8);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null)
            {
                Directory.CreateDirectory(outputDir);
            }

            using var compressedStream = new MemoryStream(compressedData);
            using var deflateStream = new System.IO.Compression.DeflateStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
            using var outputStream = File.Create(outputPath);
            deflateStream.CopyTo(outputStream);

            _logger.LogDebug("Extracted embedded PDB from {AssemblyPath} to {OutputPath}", assemblyPath, outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract embedded PDB from {AssemblyPath}", assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Validates a PDB file against the expected debug info by checking the PDB GUID.
    /// </summary>
    public virtual bool ValidatePdb(string pdbPath, PeDebugInfo debugInfo)
    {
        try
        {
            using var pdbStream = File.OpenRead(pdbPath);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            var reader = provider.GetMetadataReader();
            var pdbId = new BlobContentId(reader.DebugMetadataHeader!.Id);

            if (pdbId.Guid != debugInfo.PdbGuid)
            {
                _logger.LogDebug("PDB GUID mismatch for {PdbPath}: expected {Expected}, got {Actual}",
                    pdbPath, debugInfo.PdbGuid, pdbId.Guid);
                return false;
            }

            _logger.LogDebug("PDB validation passed for {PdbPath}", pdbPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PDB validation failed for {PdbPath}", pdbPath);
            return false;
        }
    }
}
