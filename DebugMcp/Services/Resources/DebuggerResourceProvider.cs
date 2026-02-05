using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugMcp.Models;
using DebugMcp.Models.Breakpoints;
using DebugMcp.Models.Inspection;
using DebugMcp.Services.Breakpoints;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DebugMcp.Services.Resources;

/// <summary>
/// Provides debugger state as MCP Resources.
/// All resource methods check for an active session and return structured data.
/// </summary>
[McpServerResourceType]
public sealed class DebuggerResourceProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly IDebugSessionManager _sessionManager;
    private readonly BreakpointRegistry _breakpointRegistry;
    private readonly ThreadSnapshotCache _threadSnapshotCache;
    private readonly AllowedSourcePaths _allowedSourcePaths;
    private readonly ILogger<DebuggerResourceProvider> _logger;

    public DebuggerResourceProvider(
        IDebugSessionManager sessionManager,
        BreakpointRegistry breakpointRegistry,
        ThreadSnapshotCache threadSnapshotCache,
        AllowedSourcePaths allowedSourcePaths,
        ILogger<DebuggerResourceProvider> logger)
    {
        _sessionManager = sessionManager;
        _breakpointRegistry = breakpointRegistry;
        _threadSnapshotCache = threadSnapshotCache;
        _allowedSourcePaths = allowedSourcePaths;
        _logger = logger;
    }

    /// <summary>
    /// Current debug session state (process info, state, location).
    /// </summary>
    [McpServerResource(UriTemplate = "debugger://session", Name = "Debug Session", MimeType = "application/json")]
    public string GetSessionJson()
    {
        var session = _sessionManager.CurrentSession
            ?? throw new InvalidOperationException("No active debug session");

        var dto = new SessionResourceDto
        {
            ProcessId = session.ProcessId,
            ProcessName = session.ProcessName,
            ExecutablePath = session.ExecutablePath,
            RuntimeVersion = session.RuntimeVersion,
            State = session.State.ToString(),
            LaunchMode = session.LaunchMode.ToString(),
            AttachedAt = session.AttachedAt,
            PauseReason = session.PauseReason?.ToString(),
            CurrentLocation = session.CurrentLocation != null
                ? SourceLocationToDto(session.CurrentLocation)
                : null,
            ActiveThreadId = session.ActiveThreadId,
            CommandLineArgs = session.CommandLineArgs,
            WorkingDirectory = session.WorkingDirectory
        };

        _logger.LogDebug("Reading session resource for process {ProcessId}", session.ProcessId);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// All active breakpoints, tracepoints, and exception breakpoints.
    /// </summary>
    [McpServerResource(UriTemplate = "debugger://breakpoints", Name = "Breakpoints", MimeType = "application/json")]
    public string GetBreakpointsJson()
    {
        if (_sessionManager.CurrentSession == null)
            throw new InvalidOperationException("No active debug session");

        var breakpoints = _breakpointRegistry.GetAll();
        var exceptions = _breakpointRegistry.GetAllExceptions();

        var dto = new BreakpointsResourceDto
        {
            Breakpoints = breakpoints.Select(bp => new BreakpointDto
            {
                Id = bp.Id,
                Type = bp.Type == BreakpointType.Tracepoint ? "Tracepoint" : "Breakpoint",
                File = bp.Location.File,
                Line = bp.Location.Line,
                Column = bp.Location.Column,
                Enabled = bp.Enabled,
                Verified = bp.Verified,
                State = bp.State.ToString(),
                HitCount = bp.HitCount,
                Condition = bp.Condition,
                LogMessage = bp.LogMessage,
                HitCountMultiple = bp.HitCountMultiple,
                MaxNotifications = bp.MaxNotifications,
                NotificationsSent = bp.NotificationsSent
            }).ToList(),
            ExceptionBreakpoints = exceptions.Select(eb => new ExceptionBreakpointDto
            {
                Id = eb.Id,
                ExceptionType = eb.ExceptionType,
                BreakOnFirstChance = eb.BreakOnFirstChance,
                BreakOnSecondChance = eb.BreakOnSecondChance,
                IncludeSubtypes = eb.IncludeSubtypes,
                Enabled = eb.Enabled,
                Verified = eb.Verified,
                HitCount = eb.HitCount
            }).ToList()
        };

        _logger.LogDebug("Reading breakpoints resource: {Count} breakpoints, {ExCount} exception breakpoints",
            breakpoints.Count, exceptions.Count);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Managed threads in the debugged process.
    /// </summary>
    [McpServerResource(UriTemplate = "debugger://threads", Name = "Threads", MimeType = "application/json")]
    public string GetThreadsJson()
    {
        if (_sessionManager.CurrentSession == null)
            throw new InvalidOperationException("No active debug session");

        var state = _sessionManager.GetCurrentState();
        var threads = _threadSnapshotCache.Threads ?? [];
        var stale = _threadSnapshotCache.IsStale(state);
        var capturedAt = _threadSnapshotCache.CapturedAt ?? DateTimeOffset.UtcNow;

        var dto = new ThreadsResourceDto
        {
            Threads = threads.Select(t => new ThreadDto
            {
                Id = t.Id,
                Name = t.Name,
                State = t.State.ToString(),
                IsCurrent = t.IsCurrent,
                Location = t.Location != null ? SourceLocationToDto(t.Location) : null
            }).ToList(),
            Stale = stale,
            CapturedAt = capturedAt
        };

        _logger.LogDebug("Reading threads resource: {Count} threads, stale={Stale}", threads.Count, stale);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    /// <summary>
    /// Source code from PDB-referenced files in the debugged process.
    /// </summary>
    [McpServerResource(UriTemplate = "debugger://source/{+file}", Name = "Source File", MimeType = "text/plain")]
    public async Task<string> GetSourceFileAsync(string file)
    {
        if (_sessionManager.CurrentSession == null)
            throw new InvalidOperationException("No active debug session");

        if (!_allowedSourcePaths.IsAllowed(file))
            throw new InvalidOperationException(
                $"File path '{file}' is not referenced in any loaded PDB symbols");

        if (!System.IO.File.Exists(file))
            throw new InvalidOperationException(
                $"Source file '{file}' referenced in PDB but not found on disk");

        _logger.LogDebug("Reading source resource: {FilePath}", file);
        return await System.IO.File.ReadAllTextAsync(file);
    }

    /// <summary>
    /// Gets the list of available resources (only when session is active).
    /// </summary>
    public bool HasActiveSession => _sessionManager.CurrentSession != null;

    private static SourceLocationDto SourceLocationToDto(SourceLocation loc) => new()
    {
        File = loc.File,
        Line = loc.Line,
        Column = loc.Column,
        FunctionName = loc.FunctionName,
        ModuleName = loc.ModuleName
    };

    #region DTOs

    internal sealed class SessionResourceDto
    {
        public int ProcessId { get; set; }
        public required string ProcessName { get; set; }
        public required string ExecutablePath { get; set; }
        public required string RuntimeVersion { get; set; }
        public required string State { get; set; }
        public required string LaunchMode { get; set; }
        public DateTimeOffset AttachedAt { get; set; }
        public string? PauseReason { get; set; }
        public SourceLocationDto? CurrentLocation { get; set; }
        public int? ActiveThreadId { get; set; }
        public string[]? CommandLineArgs { get; set; }
        public string? WorkingDirectory { get; set; }
    }

    internal sealed class SourceLocationDto
    {
        public required string File { get; set; }
        public int Line { get; set; }
        public int? Column { get; set; }
        public string? FunctionName { get; set; }
        public string? ModuleName { get; set; }
    }

    internal sealed class BreakpointsResourceDto
    {
        public required List<BreakpointDto> Breakpoints { get; set; }
        public required List<ExceptionBreakpointDto> ExceptionBreakpoints { get; set; }
    }

    internal sealed class BreakpointDto
    {
        public required string Id { get; set; }
        public required string Type { get; set; }
        public required string File { get; set; }
        public int Line { get; set; }
        public int? Column { get; set; }
        public bool Enabled { get; set; }
        public bool Verified { get; set; }
        public required string State { get; set; }
        public int HitCount { get; set; }
        public string? Condition { get; set; }
        public string? LogMessage { get; set; }
        public int HitCountMultiple { get; set; }
        public int MaxNotifications { get; set; }
        public int NotificationsSent { get; set; }
    }

    internal sealed class ExceptionBreakpointDto
    {
        public required string Id { get; set; }
        public required string ExceptionType { get; set; }
        public bool BreakOnFirstChance { get; set; }
        public bool BreakOnSecondChance { get; set; }
        public bool IncludeSubtypes { get; set; }
        public bool Enabled { get; set; }
        public bool Verified { get; set; }
        public int HitCount { get; set; }
    }

    internal sealed class ThreadsResourceDto
    {
        public required List<ThreadDto> Threads { get; set; }
        public bool Stale { get; set; }
        public DateTimeOffset CapturedAt { get; set; }
    }

    internal sealed class ThreadDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public required string State { get; set; }
        public bool IsCurrent { get; set; }
        public SourceLocationDto? Location { get; set; }
    }

    #endregion
}
