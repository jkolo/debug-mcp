namespace DebugMcp.Services.Completions;

/// <summary>
/// Parses expression strings to determine what kind of completion is needed.
/// </summary>
public static class CompletionContextParser
{
    /// <summary>
    /// Well-known static types that should be recognized without full qualification.
    /// These are types commonly used in expressions that have static members.
    /// </summary>
    private static readonly HashSet<string> WellKnownStaticTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Math",
        "DateTime",
        "DateTimeOffset",
        "TimeSpan",
        "String",
        "Console",
        "Convert",
        "Guid",
        "Environment",
        "Path",
        "File",
        "Directory",
        "Enum",
        "Type",
        "Activator",
        "GC",
        "Task",
        "Thread"
    };

    /// <summary>
    /// Well-known namespaces that should be recognized for namespace completion.
    /// </summary>
    private static readonly HashSet<string> WellKnownNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Microsoft",
        "System.Collections",
        "System.Collections.Generic",
        "System.Linq",
        "System.IO",
        "System.Text",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Net",
        "System.Reflection"
    };

    /// <summary>
    /// Parses an expression string to determine what kind of completion is needed.
    /// </summary>
    /// <param name="expression">The partial expression to parse</param>
    /// <returns>A CompletionContext describing what completions are needed</returns>
    public static CompletionContext Parse(string? expression)
    {
        // Handle null/empty/whitespace
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new CompletionContext(CompletionKind.Variable, "");
        }

        expression = expression.Trim();

        // Find the last dot that isn't inside brackets or parentheses
        var lastDotIndex = FindLastDotOutsideBrackets(expression);

        // No dot - this is a variable name prefix
        if (lastDotIndex < 0)
        {
            return new CompletionContext(CompletionKind.Variable, expression);
        }

        // Split into left (object/type/namespace) and right (member prefix)
        var leftPart = expression[..lastDotIndex];
        var rightPart = expression[(lastDotIndex + 1)..];

        // Determine what kind of completion based on the left part
        var kind = DetermineCompletionKind(leftPart);

        return kind switch
        {
            CompletionKind.StaticMember => new CompletionContext(
                CompletionKind.StaticMember, rightPart, TypeName: leftPart),
            CompletionKind.Namespace => new CompletionContext(
                CompletionKind.Namespace, rightPart, TypeName: leftPart),
            _ => new CompletionContext(
                CompletionKind.Member, rightPart, ObjectExpression: leftPart)
        };
    }

    /// <summary>
    /// Finds the index of the last dot that isn't inside brackets, parentheses, or quotes.
    /// </summary>
    private static int FindLastDotOutsideBrackets(string expression)
    {
        var bracketDepth = 0;
        var parenDepth = 0;
        var inString = false;
        var lastDotIndex = -1;
        char stringChar = '\0';

        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];

            // Handle string literals
            if ((c == '"' || c == '\'') && (i == 0 || expression[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }
            }

            if (inString)
                continue;

            switch (c)
            {
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(0, bracketDepth - 1);
                    break;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth = Math.Max(0, parenDepth - 1);
                    break;
                case '.' when bracketDepth == 0 && parenDepth == 0:
                    lastDotIndex = i;
                    break;
            }
        }

        return lastDotIndex;
    }

    /// <summary>
    /// Determines the completion kind based on the left part of a dotted expression.
    /// </summary>
    private static CompletionKind DetermineCompletionKind(string leftPart)
    {
        // Empty left part is invalid, treat as variable
        if (string.IsNullOrEmpty(leftPart))
            return CompletionKind.Variable;

        // Check if it's a well-known namespace
        if (WellKnownNamespaces.Contains(leftPart))
            return CompletionKind.Namespace;

        // Check if it's a well-known static type (simple name, starts with uppercase)
        if (WellKnownStaticTypes.Contains(leftPart))
            return CompletionKind.StaticMember;

        // Check if it looks like a namespace (has dots and all parts start with uppercase)
        if (leftPart.Contains('.'))
        {
            var parts = leftPart.Split('.');
            var allPartsStartUppercase = parts.All(p =>
                !string.IsNullOrEmpty(p) && char.IsUpper(p[0]));

            if (allPartsStartUppercase)
            {
                // Could be a namespace or fully qualified type
                // If first part is a known namespace root, treat as namespace
                if (parts.Length > 0 && (parts[0] == "System" || parts[0] == "Microsoft"))
                    return CompletionKind.Namespace;
            }
        }

        // Check if the left part looks like a simple type name (starts with uppercase, no dots)
        if (!leftPart.Contains('.') && !leftPart.Contains('[') && !leftPart.Contains('('))
        {
            var firstChar = leftPart[0];
            // If it starts with uppercase and is a simple identifier, might be a type
            if (char.IsUpper(firstChar) && leftPart.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                // But we need to be conservative - default to member access
                // since most uses will be accessing members of variables
                return CompletionKind.Member;
            }
        }

        // Default to member access (most common case)
        return CompletionKind.Member;
    }
}
