namespace DebugMcp.Models.CodeAnalysis;

/// <summary>
/// Indicates the type of workspace that was loaded.
/// </summary>
public enum WorkspaceType
{
    /// <summary>
    /// Workspace loaded from a .sln solution file.
    /// </summary>
    Solution,

    /// <summary>
    /// Workspace loaded from a single .csproj project file.
    /// </summary>
    Project
}
