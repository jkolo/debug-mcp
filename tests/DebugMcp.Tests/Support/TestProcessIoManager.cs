using DebugMcp.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DebugMcp.Tests.Support;

/// <summary>
/// ProcessIoManager instance for testing.
/// </summary>
public static class TestProcessIoManager
{
    public static ProcessIoManager Instance { get; } = new(NullLogger<ProcessIoManager>.Instance);
}
