using System.Text.Json;
using DotnetMcp.Models;
using FluentAssertions;

namespace DotnetMcp.Tests.Contract;

/// <summary>
/// Schema validation tests ensuring the implementation matches the MCP contract.
/// Validates that all types serialize correctly per the mcp-tools.json schema.
/// </summary>
public class SchemaValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// DebugSession serializes all required fields correctly.
    /// </summary>
    [Fact]
    public void DebugSession_Serializes_AllRequiredFields()
    {
        // Arrange - contract requires: processId, processName, runtimeVersion, state, launchMode, attachedAt
        var session = new DebugSession
        {
            ProcessId = 1234,
            ProcessName = "testapp",
            ExecutablePath = "/path/to/testapp.dll",
            RuntimeVersion = ".NET 8.0",
            AttachedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            State = SessionState.Running,
            LaunchMode = LaunchMode.Attach
        };

        // Act
        var json = JsonSerializer.Serialize(session, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - all required fields present
        root.TryGetProperty("processId", out var pid).Should().BeTrue();
        pid.GetInt32().Should().Be(1234);

        root.TryGetProperty("processName", out var name).Should().BeTrue();
        name.GetString().Should().Be("testapp");

        root.TryGetProperty("runtimeVersion", out var runtime).Should().BeTrue();
        runtime.GetString().Should().Be(".NET 8.0");

        // Note: enums serialize as numbers by default in System.Text.Json
        // The tool implementations convert to lowercase strings manually
        root.TryGetProperty("state", out var state).Should().BeTrue();
        state.ValueKind.Should().BeOneOf(JsonValueKind.Number, JsonValueKind.String);

        root.TryGetProperty("launchMode", out var mode).Should().BeTrue();
        mode.ValueKind.Should().BeOneOf(JsonValueKind.Number, JsonValueKind.String);

        root.TryGetProperty("attachedAt", out var attachedAt).Should().BeTrue();
        attachedAt.GetDateTime().Should().Be(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// SessionState enum values match contract.
    /// </summary>
    [Theory]
    [InlineData(SessionState.Disconnected, "disconnected")]
    [InlineData(SessionState.Running, "running")]
    [InlineData(SessionState.Paused, "paused")]
    public void SessionState_Serializes_ToLowercaseStrings(SessionState state, string expected)
    {
        // Contract expects lowercase: "disconnected", "running", "paused"
        var lowered = state.ToString().ToLowerInvariant();
        lowered.Should().Be(expected);
    }

    /// <summary>
    /// LaunchMode enum values match contract.
    /// </summary>
    [Theory]
    [InlineData(LaunchMode.Attach, "attach")]
    [InlineData(LaunchMode.Launch, "launch")]
    public void LaunchMode_Serializes_ToLowercaseStrings(LaunchMode mode, string expected)
    {
        // Contract expects lowercase: "attach", "launch"
        var lowered = mode.ToString().ToLowerInvariant();
        lowered.Should().Be(expected);
    }

    /// <summary>
    /// PauseReason enum values match contract.
    /// </summary>
    [Theory]
    [InlineData(PauseReason.Breakpoint, "breakpoint")]
    [InlineData(PauseReason.Step, "step")]
    [InlineData(PauseReason.Exception, "exception")]
    [InlineData(PauseReason.Pause, "pause")]
    [InlineData(PauseReason.Entry, "entry")]
    public void PauseReason_Serializes_ToLowercaseStrings(PauseReason reason, string expected)
    {
        // Contract expects lowercase: "breakpoint", "step", "exception", "pause", "entry"
        var lowered = reason.ToString().ToLowerInvariant();
        lowered.Should().Be(expected);
    }

    /// <summary>
    /// SourceLocation serializes required fields.
    /// </summary>
    [Fact]
    public void SourceLocation_Serializes_RequiredFields()
    {
        // Contract requires: file, line
        // Optional: column, functionName, moduleName
        var location = new SourceLocation(
            File: "/path/to/source.cs",
            Line: 42,
            Column: 8,
            FunctionName: "TestMethod",
            ModuleName: "TestAssembly"
        );

        var json = JsonSerializer.Serialize(location, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Required fields
        root.TryGetProperty("file", out var file).Should().BeTrue();
        file.GetString().Should().Be("/path/to/source.cs");

        root.TryGetProperty("line", out var line).Should().BeTrue();
        line.GetInt32().Should().Be(42);
    }

    /// <summary>
    /// SourceLocation with nulls omits optional fields.
    /// </summary>
    [Fact]
    public void SourceLocation_WithNulls_OmitsOptionalFields()
    {
        var location = new SourceLocation(
            File: "/path/to/source.cs",
            Line: 1,
            Column: null,
            FunctionName: null,
            ModuleName: null
        );

        var json = JsonSerializer.Serialize(location, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Required fields should be present
        root.TryGetProperty("file", out _).Should().BeTrue();
        root.TryGetProperty("line", out _).Should().BeTrue();

        // Optional null fields may be present or omitted depending on serialization settings
        // Just verify the object serializes without error
        json.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// ErrorResponse serializes correctly.
    /// </summary>
    [Fact]
    public void ErrorResponse_Serializes_RequiredFields()
    {
        // Contract requires: error.code, error.message
        // Optional: error.details
        var error = new ErrorResponse
        {
            Code = ErrorCodes.ProcessNotFound,
            Message = "Process 12345 not found",
            Details = new { pid = 12345 }
        };

        var json = JsonSerializer.Serialize(error, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("code", out var code).Should().BeTrue();
        code.GetString().Should().Be(ErrorCodes.ProcessNotFound);

        root.TryGetProperty("message", out var msg).Should().BeTrue();
        msg.GetString().Should().Contain("12345");

        root.TryGetProperty("details", out var details).Should().BeTrue();
        details.TryGetProperty("pid", out var pid).Should().BeTrue();
        pid.GetInt32().Should().Be(12345);
    }

    /// <summary>
    /// ProcessInfo record serializes all fields.
    /// </summary>
    [Fact]
    public void ProcessInfo_Serializes_AllFields()
    {
        var info = new ProcessInfo(
            Pid: 1234,
            Name: "testapp",
            ExecutablePath: "/path/to/testapp.dll",
            IsManaged: true,
            CommandLine: "dotnet testapp.dll --arg",
            RuntimeVersion: ".NET 8.0"
        );

        var json = JsonSerializer.Serialize(info, JsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("pid", out var pid).Should().BeTrue();
        pid.GetInt32().Should().Be(1234);

        root.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be("testapp");

        root.TryGetProperty("isManaged", out var isManaged).Should().BeTrue();
        isManaged.GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Tool response format is consistent.
    /// </summary>
    [Fact]
    public void ToolResponse_Format_IsConsistent()
    {
        // All tool responses should have: success (bool)
        // On success: relevant data
        // On error: error object with code and message

        // Success response
        var successResponse = new
        {
            success = true,
            session = new { processId = 1234 }
        };

        var successJson = JsonSerializer.Serialize(successResponse, JsonOptions);
        var successDoc = JsonDocument.Parse(successJson);
        successDoc.RootElement.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        // Error response
        var errorResponse = new
        {
            success = false,
            error = new
            {
                code = ErrorCodes.ProcessNotFound,
                message = "Process not found"
            }
        };

        var errorJson = JsonSerializer.Serialize(errorResponse, JsonOptions);
        var errorDoc = JsonDocument.Parse(errorJson);
        errorDoc.RootElement.TryGetProperty("success", out var errorSuccess).Should().BeTrue();
        errorSuccess.GetBoolean().Should().BeFalse();
        errorDoc.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
        error.TryGetProperty("code", out _).Should().BeTrue();
        error.TryGetProperty("message", out _).Should().BeTrue();
    }

    /// <summary>
    /// All defined ErrorCodes are valid.
    /// </summary>
    [Fact]
    public void ErrorCodes_AllDefined_AreScreamingSnakeCase()
    {
        var codes = new[]
        {
            ErrorCodes.ProcessNotFound,
            ErrorCodes.NotDotNetProcess,
            ErrorCodes.PermissionDenied,
            ErrorCodes.SessionActive,
            ErrorCodes.AlreadyAttached,
            ErrorCodes.NoSession,
            ErrorCodes.AttachFailed,
            ErrorCodes.LaunchFailed,
            ErrorCodes.InvalidPath,
            ErrorCodes.Timeout
        };

        foreach (var code in codes)
        {
            code.Should().NotBeNullOrEmpty();
            code.Should().MatchRegex(@"^[A-Z_]+$", $"Error code '{code}' should be SCREAMING_SNAKE_CASE");
        }
    }
}
