namespace DebugMcp.Services.Progress;

/// <summary>
/// An <see cref="IProgress{T}"/> that invokes its callback synchronously, on the calling thread,
/// instead of the BCL's <see cref="System.Progress{T}"/> — which posts to the captured
/// <see cref="SynchronizationContext"/> (or the thread pool, absent one), so its callback can
/// still be pending when the operation it was reporting on has already returned. Progress is
/// advisory (data-model.md §4), so that race is harmless on the wire, but it makes tests
/// asserting "all updates arrived by the time the call returned" flaky. Used where an API only
/// accepts <see cref="IProgress{T}"/> (e.g. <c>MSBuildWorkspace.OpenSolutionAsync</c>).
/// </summary>
public sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
