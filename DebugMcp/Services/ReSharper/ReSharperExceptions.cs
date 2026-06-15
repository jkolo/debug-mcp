namespace DebugMcp.Services.ReSharper;

/// <summary>Base for ReSharper integration failures carrying a stable error code.</summary>
public abstract class ReSharperException : Exception
{
    /// <summary>Stable error code (see <c>ErrorCodes</c>) the tool layer maps to the envelope.</summary>
    public abstract string Code { get; }

    /// <summary>Optional structured details surfaced in the error envelope.</summary>
    public object? Details { get; }

    protected ReSharperException(string message, object? details = null, Exception? inner = null)
        : base(message, inner) => Details = details;
}

/// <summary>The .NET SDK (dotnet CLI) is unavailable to acquire/run the engine.</summary>
public sealed class ReSharperPrerequisiteException : ReSharperException
{
    public override string Code => Models.ErrorCodes.PrerequisiteMissing;
    public ReSharperPrerequisiteException(string message, object? details = null, Exception? inner = null)
        : base(message, details, inner) { }
}

/// <summary>Downloading/installing the engine failed (offline, install error, unwritable cache).</summary>
public sealed class ReSharperAcquisitionException : ReSharperException
{
    public override string Code => Models.ErrorCodes.EngineAcquisitionFailed;
    public ReSharperAcquisitionException(string message, object? details = null, Exception? inner = null)
        : base(message, details, inner) { }
}

/// <summary>The engine's pre-analysis build of the target failed.</summary>
public sealed class ReSharperBuildFailedException : ReSharperException
{
    public override string Code => Models.ErrorCodes.BuildFailed;
    public ReSharperBuildFailedException(string message, object? details = null, Exception? inner = null)
        : base(message, details, inner) { }
}

/// <summary>The engine ran but failed/crashed.</summary>
public sealed class ReSharperRunFailedException : ReSharperException
{
    public override string Code => Models.ErrorCodes.InspectionFailed;
    public ReSharperRunFailedException(string message, object? details = null, Exception? inner = null)
        : base(message, details, inner) { }
}

/// <summary>The engine's report could not be parsed.</summary>
public sealed class InspectionReportParseException : ReSharperException
{
    public override string Code => Models.ErrorCodes.InspectionFailed;
    public InspectionReportParseException(string message, object? details = null, Exception? inner = null)
        : base(message, details, inner) { }
}

/// <summary>A phase (acquisition or inspection) exceeded its time budget.</summary>
public sealed class ReSharperTimeoutException : ReSharperException
{
    public override string Code => Models.ErrorCodes.Timeout;

    /// <summary>"acquisition" or "inspection".</summary>
    public string Phase { get; }

    public ReSharperTimeoutException(string phase, string message)
        : base(message, details: new { phase }) => Phase = phase;
}

/// <summary>The requested project scope was not found in the solution.</summary>
public sealed class ReSharperProjectNotFoundException : ReSharperException
{
    public override string Code => Models.ErrorCodes.ProjectNotFound;
    public ReSharperProjectNotFoundException(string message, object? details = null)
        : base(message, details) { }
}
