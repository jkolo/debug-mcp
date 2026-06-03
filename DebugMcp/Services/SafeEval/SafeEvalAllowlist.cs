namespace DebugMcp.Services.SafeEval;

public sealed record AllowlistEntry(string? TypeSimpleName, string MethodName)
{
    public bool IsWildcard => MethodName == "*";
}

public sealed class SafeEvalAllowlist
{
    private static readonly IReadOnlySet<AllowlistEntry> DefaultEntries = BuildDefaults();

    private readonly IReadOnlySet<AllowlistEntry> _entries;

    public SafeEvalAllowlist(IEnumerable<string>? additionalPatterns = null)
    {
        if (additionalPatterns == null)
        {
            _entries = DefaultEntries;
            return;
        }

        var merged = new HashSet<AllowlistEntry>(DefaultEntries);
        foreach (var pattern in additionalPatterns)
        {
            var entry = ParseEntry(pattern.Trim());
            if (entry != null)
                merged.Add(entry);
        }
        _entries = merged;
    }

    public bool IsAllowed(string receiverSimpleName, string methodName) =>
        _entries.Any(e =>
            (e.TypeSimpleName == null || e.TypeSimpleName == receiverSimpleName) &&
            (e.IsWildcard || e.MethodName == methodName));

    private static AllowlistEntry? ParseEntry(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        var lastDot = pattern.LastIndexOf('.');
        if (lastDot < 0)
            return null;

        var method = pattern[(lastDot + 1)..];
        var typePart = pattern[..lastDot];

        // Strip namespace prefix — keep only the simple type name (last segment)
        var typeSimpleName = typePart.Contains('.')
            ? typePart[(typePart.LastIndexOf('.') + 1)..]
            : typePart;

        return new AllowlistEntry(typeSimpleName, method);
    }

    private static HashSet<AllowlistEntry> BuildDefaults()
    {
        var entries = new HashSet<AllowlistEntry>
        {
            // Any-receiver entries (ToString, Equals, GetHashCode exist on all types)
            new(null, "ToString"),
            new(null, "Equals"),
            new(null, "GetHashCode"),

            // String methods
            new("String", "Format"),
            new("String", "Concat"),
            new("String", "IsNullOrEmpty"),
            new("String", "IsNullOrWhiteSpace"),
            new("String", "Join"),
            new("String", "Compare"),
            new("String", "Equals"),

            // Math — wildcard covers all members
            new("Math", "*"),

            // Enumerable
            new("Enumerable", "Count"),
            new("Enumerable", "Any"),
            new("Enumerable", "First"),
            new("Enumerable", "FirstOrDefault"),
            new("Enumerable", "Last"),
            new("Enumerable", "LastOrDefault"),
            new("Enumerable", "ToList"),
            new("Enumerable", "ToArray"),

            // Convert
            new("Convert", "ToString"),
            new("Convert", "ToInt32"),
            new("Convert", "ToDouble"),
            new("Convert", "ToBoolean"),

            // DateTime, TimeSpan, Guid
            new("DateTime", "ToString"),
            new("TimeSpan", "ToString"),
            new("Guid", "ToString"),
        };
        return entries;
    }
}
