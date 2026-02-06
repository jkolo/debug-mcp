using System.Net;
using DebugMcp.Services.Symbols;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Unit.Symbols;

public class SymbolServerClientTests : IDisposable
{
    private readonly string _tempDir;

    public SymbolServerClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "debug-mcp-client-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void BuildDownloadUrl_ProducesCorrectSsqpUrl()
    {
        var client = CreateClient(new MockHandler(HttpStatusCode.OK, []));
        var debugInfo = CreateDebugInfo("MyLib.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");

        var url = client.BuildDownloadUrl("https://symbols.nuget.org/download/symbols", debugInfo);

        url.Should().Be("https://symbols.nuget.org/download/symbols/mylib.pdb/aabbccdd11223344aabbccdd11223344FFFFFFFF/mylib.pdb");
    }

    [Fact]
    public void BuildDownloadUrl_LowercasesFilename()
    {
        var client = CreateClient(new MockHandler(HttpStatusCode.OK, []));
        var debugInfo = CreateDebugInfo("System.Private.CoreLib.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");

        var url = client.BuildDownloadUrl("https://msdl.microsoft.com/download/symbols", debugInfo);

        url.Should().Contain("system.private.corelib.pdb");
        url.Should().NotContain("System.Private.CoreLib.pdb");
    }

    [Fact]
    public async Task TryDownloadAsync_WithSuccessResponse_WritesFileAndReturnsTrue()
    {
        var pdbBytes = new byte[] { 0x42, 0x53, 0x4A, 0x42, 0x01, 0x02, 0x03 };
        var client = CreateClient(new MockHandler(HttpStatusCode.OK, pdbBytes));
        var debugInfo = CreateDebugInfo("Test.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");
        var outputPath = Path.Combine(_tempDir, "Test.pdb");

        var result = await client.TryDownloadAsync(
            "https://symbols.nuget.org/download/symbols", debugInfo, outputPath);

        result.Should().BeTrue();
        File.Exists(outputPath).Should().BeTrue();
        File.ReadAllBytes(outputPath).Should().BeEquivalentTo(pdbBytes);
    }

    [Fact]
    public async Task TryDownloadAsync_With404Response_ReturnsFalseNoFile()
    {
        var client = CreateClient(new MockHandler(HttpStatusCode.NotFound, []));
        var debugInfo = CreateDebugInfo("Missing.pdb", "00000000000000000000000000000000FFFFFFFF");
        var outputPath = Path.Combine(_tempDir, "Missing.pdb");

        var result = await client.TryDownloadAsync(
            "https://symbols.nuget.org/download/symbols", debugInfo, outputPath);

        result.Should().BeFalse();
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryDownloadAsync_WithTimeout_ReturnsFalse()
    {
        var handler = new MockHandler(HttpStatusCode.OK, [], simulateDelay: TimeSpan.FromSeconds(60));
        var options = new SymbolServerOptions { TimeoutSeconds = 1 };
        var client = CreateClient(handler, options);
        var debugInfo = CreateDebugInfo("Slow.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");
        var outputPath = Path.Combine(_tempDir, "Slow.pdb");

        var result = await client.TryDownloadAsync(
            "https://symbols.nuget.org/download/symbols", debugInfo, outputPath);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryDownloadAsync_WithOversizedContent_ReturnsFalse()
    {
        var options = new SymbolServerOptions { MaxFileSizeMB = 1 };
        // Content-Length header says 200MB
        var handler = new MockHandler(HttpStatusCode.OK, new byte[100], contentLength: 200 * 1024 * 1024);
        var client = CreateClient(handler, options);
        var debugInfo = CreateDebugInfo("Huge.pdb", "aabbccdd11223344aabbccdd11223344FFFFFFFF");
        var outputPath = Path.Combine(_tempDir, "Huge.pdb");

        var result = await client.TryDownloadAsync(
            "https://symbols.nuget.org/download/symbols", debugInfo, outputPath);

        result.Should().BeFalse();
        File.Exists(outputPath).Should().BeFalse();
    }

    private static SymbolServerClient CreateClient(HttpMessageHandler handler, SymbolServerOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        return new SymbolServerClient(httpClient, options ?? new SymbolServerOptions(), NullLogger<SymbolServerClient>.Instance);
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

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _content;
        private readonly TimeSpan? _delay;
        private readonly long? _contentLength;

        public MockHandler(HttpStatusCode statusCode, byte[] content, TimeSpan? simulateDelay = null, long? contentLength = null)
        {
            _statusCode = statusCode;
            _content = content;
            _delay = simulateDelay;
            _contentLength = contentLength;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_delay.HasValue)
            {
                await Task.Delay(_delay.Value, cancellationToken);
            }

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_content)
            };

            if (_contentLength.HasValue)
            {
                response.Content.Headers.ContentLength = _contentLength.Value;
            }

            return response;
        }
    }
}
