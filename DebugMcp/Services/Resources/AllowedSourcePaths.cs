using System.Collections.Concurrent;

namespace DebugMcp.Services.Resources;

/// <summary>
/// Tracks PDB-referenced source file paths for the security boundary
/// of the debugger://source/{file} resource. Only files referenced in PDB
/// symbols of loaded modules are allowed to be served.
/// </summary>
public sealed class AllowedSourcePaths
{
    // Normalized file path → set of module paths that reference it
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _paths = new();
    // Module path → set of normalized file paths it references
    private readonly ConcurrentDictionary<string, HashSet<string>> _moduleToFiles = new();

    /// <summary>
    /// Adds source file paths associated with a module.
    /// </summary>
    public void AddModule(string modulePath, IEnumerable<string> sourcePaths)
    {
        var normalizedModule = NormalizePath(modulePath);
        var files = new HashSet<string>();

        foreach (var path in sourcePaths)
        {
            var normalized = NormalizePath(path);
            files.Add(normalized);

            var modules = _paths.GetOrAdd(normalized, _ => new ConcurrentDictionary<string, byte>());
            modules.TryAdd(normalizedModule, 0);
        }

        _moduleToFiles[normalizedModule] = files;
    }

    /// <summary>
    /// Removes all source file paths associated with a module.
    /// </summary>
    public void RemoveModule(string modulePath)
    {
        var normalizedModule = NormalizePath(modulePath);
        if (!_moduleToFiles.TryRemove(normalizedModule, out var files))
            return;

        foreach (var file in files)
        {
            if (_paths.TryGetValue(file, out var modules))
            {
                modules.TryRemove(normalizedModule, out _);
                // Remove the file entry if no modules reference it
                if (modules.IsEmpty)
                    _paths.TryRemove(file, out _);
            }
        }
    }

    /// <summary>
    /// Checks whether a file path is in the allowed set.
    /// </summary>
    public bool IsAllowed(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return _paths.TryGetValue(normalized, out var modules) && !modules.IsEmpty;
    }

    /// <summary>
    /// Clears all allowed paths.
    /// </summary>
    public void Clear()
    {
        _paths.Clear();
        _moduleToFiles.Clear();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
