using System.Diagnostics;
using System.Text.Json;
using DebugMcp.Infrastructure;
using DebugMcp.Models;
using DebugMcp.Services.Progress;
using DebugMcp.Services.ReSharper;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace DebugMcp.Tools;

/// <summary>
/// Shared validation, invocation, and error-envelope mapping for the ReSharper inspection
/// tools (solution + project), keeping each tool class thin.
/// </summary>
internal static class ReSharperToolHelper
{
    private const int MinTimeoutSeconds = 10;
    private const int MaxTimeoutSeconds = 1800;
    private const int MaxResultsCap = 500;

    public static async Task<string> RunAsync(
        string toolName,
        string target,
        string requiredExtension,
        string? severity,
        string? project,
        bool noBuild,
        int? timeoutSeconds,
        int? maxResults,
        IReSharperInspectionService service,
        ReSharperOptions options,
        ILogger logger,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.ToolInvoked(toolName, JsonSerializer.Serialize(new { target, severity, project, noBuild, timeoutSeconds, maxResults }));

        try
        {
            // Validate target.
            if (string.IsNullOrWhiteSpace(target))
            {
                return Error(toolName, logger, jsonOptions, ErrorCodes.InvalidPath, $"{requiredExtension} path cannot be empty.");
            }
            if (!target.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Error(toolName, logger, jsonOptions, ErrorCodes.InvalidPath, $"Path must be a {requiredExtension} file: {target}");
            }
            if (!File.Exists(target))
            {
                return Error(toolName, logger, jsonOptions, ErrorCodes.InvalidPath, $"File not found: {target}");
            }

            // Validate bounds.
            var effectiveTimeout = timeoutSeconds ?? options.InspectionTimeoutSeconds;
            if (effectiveTimeout < MinTimeoutSeconds || effectiveTimeout > MaxTimeoutSeconds)
            {
                return Error(toolName, logger, jsonOptions, ErrorCodes.InvalidParameter,
                    $"timeoutSeconds must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} (got {effectiveTimeout}).");
            }
            var effectiveMax = Math.Min(maxResults ?? options.MaxResults, MaxResultsCap);
            if (effectiveMax <= 0)
            {
                effectiveMax = options.MaxResults;
            }

            var result = await service.InspectAsync(
                Path.GetFullPath(target), severity, project, noBuild, effectiveTimeout, effectiveMax,
                cancellationToken, ProgressReporterAdapter.Create(progress));

            stopwatch.Stop();
            logger.ToolCompleted(toolName, stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(new { success = true, data = result }, jsonOptions);
        }
        catch (ReSharperException ex)
        {
            return Error(toolName, logger, jsonOptions, ex.Code, ex.Message, ex.Details);
        }
        catch (ArgumentException ex)
        {
            return Error(toolName, logger, jsonOptions, ErrorCodes.InvalidParameter, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Error(toolName, logger, jsonOptions, ErrorCodes.Timeout, "The inspection was cancelled.");
        }
        catch (Exception ex)
        {
            return Error(toolName, logger, jsonOptions, ErrorCodes.InspectionFailed, $"Inspection failed: {ex.Message}");
        }
    }

    private static string Error(string toolName, ILogger logger, JsonSerializerOptions jsonOptions,
        string code, string message, object? details = null)
    {
        logger.ToolError(toolName, code);
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = new ErrorResponse { Code = code, Message = message, Details = details }
        }, jsonOptions);
    }
}
