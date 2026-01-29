using BuggyApp.Models;

namespace BuggyApp.Services;

public class UserService
{
    private readonly Dictionary<string, User> _users = new()
    {
        ["user-123"] = new User
        {
            Id = "user-123",
            Name = "Alice Johnson",
            Email = "alice@example.com",
            IsActive = true,
            Roles = ["admin", "user"]
        },
        ["user-456"] = new User
        {
            Id = "user-456",
            Name = "Bob Smith",
            Email = "bob@example.com",
            IsActive = false,
            Roles = ["user"]
        }
    };

    /// <summary>
    /// Bug: returns null for unknown user IDs instead of throwing or returning a default.
    /// This causes NullReferenceException in callers that don't null-check.
    /// </summary>
    public User? GetUser(string userId)
    {
        _users.TryGetValue(userId, out var user);
        return user; // Returns null if not found — caller doesn't expect this
    }

    public bool UserExists(string userId) => _users.ContainsKey(userId);
}
