using Microsoft.Extensions.Logging;

namespace DebugMcp.Services.Symbols;

/// <summary>
/// HTTP client for the Simple Symbol Query Protocol (SSQP).
/// Downloads PDB files from symbol servers like Microsoft and NuGet.
/// </summary>
public class SymbolServerClient
{
    private readonly HttpClient _httpClient;
    private readonly SymbolServerOptions _options;
    private readonly ILogger<SymbolServerClient> _logger;

    public SymbolServerClient(HttpClient httpClient, SymbolServerOptions options, ILogger<SymbolServerClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Builds the SSQP download URL for a PDB file.
    /// </summary>
    public string BuildDownloadUrl(string serverUrl, PeDebugInfo debugInfo)
    {
        var lowerFileName = debugInfo.PdbFileName.ToLowerInvariant();
        return $"{serverUrl.TrimEnd('/')}/{lowerFileName}/{debugInfo.SymbolServerKey}/{lowerFileName}";
    }

    /// <summary>
    /// Tries to download a PDB file from a symbol server.
    /// </summary>
    /// <returns>True if download succeeded, false if not found or error.</returns>
    public virtual async Task<bool> TryDownloadAsync(
        string serverUrl, PeDebugInfo debugInfo, string outputPath, CancellationToken cancellationToken = default)
    {
        var url = BuildDownloadUrl(serverUrl, debugInfo);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("PDB not found on server: {Url}", url);
                return false;
            }

            response.EnsureSuccessStatusCode();

            // Check Content-Length against size limit
            var contentLength = response.Content.Headers.ContentLength;
            var maxBytes = (long)_options.MaxFileSizeMB * 1024 * 1024;
            if (contentLength.HasValue && contentLength.Value > maxBytes)
            {
                _logger.LogWarning("PDB too large: {PdbFileName} is {SizeMB}MB (limit: {LimitMB}MB)",
                    debugInfo.PdbFileName, contentLength.Value / (1024 * 1024), _options.MaxFileSizeMB);
                return false;
            }

            // Download to temporary file first, then move to output
            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null)
            {
                Directory.CreateDirectory(outputDir);
            }

            var tempPath = outputPath + ".tmp";
            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var fileStream = File.Create(tempPath);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
                {
                    totalRead += bytesRead;
                    if (totalRead > maxBytes)
                    {
                        _logger.LogWarning("PDB download exceeded size limit during streaming: {PdbFileName}", debugInfo.PdbFileName);
                        fileStream.Close();
                        File.Delete(tempPath);
                        return false;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                }

                fileStream.Close();
                File.Move(tempPath, outputPath, overwrite: true);

                _logger.LogInformation("Downloaded PDB: {PdbFileName} ({Bytes} bytes) from {ServerUrl}",
                    debugInfo.PdbFileName, totalRead, serverUrl);
                return true;
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("PDB download timed out after {Timeout}s: {PdbFileName} from {ServerUrl}",
                _options.TimeoutSeconds, debugInfo.PdbFileName, serverUrl);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "PDB download failed: {PdbFileName} from {ServerUrl}", debugInfo.PdbFileName, serverUrl);
            return false;
        }
    }
}
