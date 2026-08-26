using DebugMcp.Tests.Support;

namespace DebugMcp.Tests.Contract;

/// <summary>
/// FR-001/FR-006: every one of the 39 tools is asynchronous and cancellable. Red until
/// T014–T017 convert the remaining synchronous tools and add the missing
/// <see cref="CancellationToken"/> parameters.
/// </summary>
public class ToolAsyncContractTests
{
    [Fact]
    public void AllTools_ReturnTask()
    {
        var nonAsync = McpToolDiscovery.GetAllToolMethods()
            .Where(t => !typeof(Task).IsAssignableFrom(t.Method.ReturnType))
            .Select(t => t.Name)
            .ToList();

        nonAsync.Should().BeEmpty("every tool must return Task<...> (FR-001)");
    }

    [Fact]
    public void AllTools_AcceptCancellationToken()
    {
        var missing = McpToolDiscovery.GetAllToolMethods()
            .Where(t => !t.Method.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)))
            .Select(t => t.Name)
            .ToList();

        missing.Should().BeEmpty("every tool must accept a CancellationToken (FR-006)");
    }
}
