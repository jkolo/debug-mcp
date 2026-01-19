using System.Text.Json.Serialization;

namespace DotnetMcp.Models;

/// <summary>
/// Standard error response structure for MCP tools.
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>Error code for programmatic handling.</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Additional context about the error.</summary>
    [JsonPropertyName("details")]
    public object? Details { get; init; }
}

/// <summary>
/// Error codes for debug session operations.
/// </summary>
public static class ErrorCodes
{
    /// <summary>PID does not exist.</summary>
    public const string ProcessNotFound = "PROCESS_NOT_FOUND";

    /// <summary>Process is not a .NET application.</summary>
    public const string NotDotNetProcess = "NOT_DOTNET_PROCESS";

    /// <summary>Insufficient privileges to debug.</summary>
    public const string PermissionDenied = "PERMISSION_DENIED";

    /// <summary>A debug session is already active.</summary>
    public const string SessionActive = "SESSION_ACTIVE";

    /// <summary>Already attached to a process.</summary>
    public const string AlreadyAttached = "ALREADY_ATTACHED";

    /// <summary>No active session to operate on.</summary>
    public const string NoSession = "NO_SESSION";

    /// <summary>ICorDebug attach failed.</summary>
    public const string AttachFailed = "ATTACH_FAILED";

    /// <summary>Process launch failed.</summary>
    public const string LaunchFailed = "LAUNCH_FAILED";

    /// <summary>Executable path invalid or not found.</summary>
    public const string InvalidPath = "INVALID_PATH";

    /// <summary>Operation timed out.</summary>
    public const string Timeout = "TIMEOUT";
}
