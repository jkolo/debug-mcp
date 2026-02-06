using DebugMcp.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace DebugMcp.Services.Completions;

/// <summary>
/// Provides expression completions for the debugger.
/// Returns variable names, object members, static type members, and namespace contents.
/// </summary>
public sealed class ExpressionCompletionProvider
{
    private readonly IDebugSessionManager _sessionManager;
    private readonly IProcessDebugger _processDebugger;
    private readonly ILogger<ExpressionCompletionProvider> _logger;

    public ExpressionCompletionProvider(
        IDebugSessionManager sessionManager,
        IProcessDebugger processDebugger,
        ILogger<ExpressionCompletionProvider> logger)
    {
        _sessionManager = sessionManager;
        _processDebugger = processDebugger;
        _logger = logger;
    }

    /// <summary>
    /// Gets completions for the given completion request.
    /// </summary>
    public async Task<CompleteResult> GetCompletionsAsync(
        CompleteRequestParams request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Get the reference name - either from PromptReference or ResourceTemplateReference
            var referenceName = GetReferenceName(request.Ref);

            // We support completions for:
            // - PromptReference with name "evaluate" (if we create a prompt)
            // - ResourceTemplateReference with URI containing "evaluate"
            // - Any reference where the argument name is "expression"
            // For pragmatic reasons, we accept any request with argument name "expression"
            if (request.Argument?.Name != "expression")
            {
                _logger.LogDebug("Completion request for unsupported argument: {ArgumentName}",
                    request.Argument?.Name);
                return EmptyResult();
            }

            // Check for active session
            var session = _sessionManager.CurrentSession;
            if (session == null)
            {
                _logger.LogDebug("No active debug session for completion request");
                return EmptyResult();
            }

            // Must be paused to enumerate variables
            if (session.State != SessionState.Paused)
            {
                _logger.LogDebug("Session not paused (state: {State}), returning empty completions",
                    session.State);
                return EmptyResult();
            }

            var expression = request.Argument?.Value ?? "";
            var context = CompletionContextParser.Parse(expression);

            _logger.LogDebug("Completion request: expression='{Expression}', kind={Kind}, prefix='{Prefix}'",
                expression, context.Kind, context.Prefix);

            var completions = context.Kind switch
            {
                CompletionKind.Variable => await GetVariableCompletionsAsync(context, cancellationToken),
                CompletionKind.Member => await GetMemberCompletionsAsync(context, cancellationToken),
                CompletionKind.StaticMember => await GetStaticMemberCompletionsAsync(context, cancellationToken),
                CompletionKind.Namespace => await GetNamespaceCompletionsAsync(context, cancellationToken),
                _ => []
            };

            return CreateResult(completions);
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 100)
            {
                _logger.LogInformation("Slow completion request: {ElapsedMs}ms for expression '{Expression}'",
                    stopwatch.ElapsedMilliseconds, request.Argument?.Value);
            }
        }
    }

    /// <summary>
    /// Extracts the reference name from a Reference object.
    /// </summary>
    private static string? GetReferenceName(Reference? reference)
    {
        return reference switch
        {
            PromptReference pr => pr.Name,
            ResourceTemplateReference rtr => rtr.Uri,
            _ => null
        };
    }

    /// <summary>
    /// Gets completions for variable names in the current scope.
    /// </summary>
    internal async Task<IReadOnlyList<string>> GetVariableCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async operations
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var variables = _sessionManager.GetVariables(null, 0, "all", null);
            var names = variables
                .Select(v => v.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Where(n => string.IsNullOrEmpty(context.Prefix) ||
                           n.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogDebug("Found {Count} variable completions for prefix '{Prefix}'",
                names.Count, context.Prefix);

            return names;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting variable completions");
            return [];
        }
    }

    /// <summary>
    /// Gets completions for object members.
    /// </summary>
    internal async Task<IReadOnlyList<string>> GetMemberCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.ObjectExpression))
            return [];

        try
        {
            // Evaluate the object to get its type
            var evalResult = await _sessionManager.EvaluateAsync(
                context.ObjectExpression,
                null, 0, 5000,
                cancellationToken);

            if (!evalResult.Success || string.IsNullOrEmpty(evalResult.Type))
            {
                _logger.LogDebug("Could not evaluate object expression: {Expression}",
                    context.ObjectExpression);
                return [];
            }

            // Get members for this type
            var membersResult = await _processDebugger.GetMembersAsync(
                evalResult.Type,
                moduleName: null,
                includeInherited: false,
                memberKinds: null, // Include all member kinds
                visibility: null,  // Include all visibilities
                includeStatic: true,
                includeInstance: true,
                cancellationToken);

            // Collect all member names
            var names = new List<string>();
            names.AddRange(membersResult.Methods.Select(m => m.Name));
            names.AddRange(membersResult.Properties.Select(p => p.Name));
            names.AddRange(membersResult.Fields.Select(f => f.Name));

            // Filter by prefix, deduplicate, and sort
            var completions = names
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Where(n => string.IsNullOrEmpty(context.Prefix) ||
                           n.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogDebug("Found {Count} member completions for type '{Type}' with prefix '{Prefix}'",
                completions.Count, evalResult.Type, context.Prefix);

            return completions;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting member completions for '{Expression}'",
                context.ObjectExpression);
            return [];
        }
    }

    /// <summary>
    /// Gets completions for static type members.
    /// </summary>
    internal async Task<IReadOnlyList<string>> GetStaticMemberCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(context.TypeName))
            return [];

        try
        {
            // Resolve well-known type names to full names
            var fullTypeName = ResolveTypeName(context.TypeName);

            // Get members for this type
            var membersResult = await _processDebugger.GetMembersAsync(
                fullTypeName,
                moduleName: null,
                includeInherited: false,
                memberKinds: null,
                visibility: null,
                includeStatic: true,
                includeInstance: true,
                cancellationToken);

            // Collect only static member names
            var names = new List<string>();
            names.AddRange(membersResult.Methods.Where(m => m.IsStatic).Select(m => m.Name));
            names.AddRange(membersResult.Properties.Where(p => p.IsStatic).Select(p => p.Name));
            names.AddRange(membersResult.Fields.Where(f => f.IsStatic).Select(f => f.Name));

            // Filter by prefix, deduplicate, and sort
            var completions = names
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Where(n => string.IsNullOrEmpty(context.Prefix) ||
                           n.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogDebug("Found {Count} static member completions for type '{Type}' with prefix '{Prefix}'",
                completions.Count, fullTypeName, context.Prefix);

            return completions;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting static member completions for type '{Type}'",
                context.TypeName);
            return [];
        }
    }

    /// <summary>
    /// Resolves well-known type names to their fully qualified names.
    /// </summary>
    private static string ResolveTypeName(string typeName)
    {
        // Map well-known simple names to System.* equivalents
        return typeName switch
        {
            "Math" => "System.Math",
            "DateTime" => "System.DateTime",
            "DateTimeOffset" => "System.DateTimeOffset",
            "TimeSpan" => "System.TimeSpan",
            "String" => "System.String",
            "Console" => "System.Console",
            "Convert" => "System.Convert",
            "Guid" => "System.Guid",
            "Environment" => "System.Environment",
            "Path" => "System.IO.Path",
            "File" => "System.IO.File",
            "Directory" => "System.IO.Directory",
            "Enum" => "System.Enum",
            "Type" => "System.Type",
            "Activator" => "System.Activator",
            "GC" => "System.GC",
            "Task" => "System.Threading.Tasks.Task",
            "Thread" => "System.Threading.Thread",
            _ => typeName // Already fully qualified or unknown
        };
    }

    /// <summary>
    /// Gets completions for namespace contents.
    /// For well-known namespaces, returns child namespaces statically.
    /// Future enhancement: enumerate types from loaded modules.
    /// </summary>
    internal async Task<IReadOnlyList<string>> GetNamespaceCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for future async module enumeration
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(context.TypeName))
            return [];

        // Get well-known child namespaces for this namespace
        var childNamespaces = GetWellKnownChildNamespaces(context.TypeName);

        // Filter by prefix, sort
        var completions = childNamespaces
            .Where(n => string.IsNullOrEmpty(context.Prefix) ||
                       n.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogDebug("Found {Count} namespace completions for '{Namespace}' with prefix '{Prefix}'",
            completions.Count, context.TypeName, context.Prefix);

        return completions;
    }

    /// <summary>
    /// Gets well-known child namespaces for a given parent namespace.
    /// This is a static mapping for common .NET namespaces.
    /// </summary>
    private static IEnumerable<string> GetWellKnownChildNamespaces(string parentNamespace)
    {
        return parentNamespace switch
        {
            "System" => ["Collections", "IO", "Text", "Threading", "Linq", "Net", "Reflection", "Diagnostics", "Globalization", "Runtime"],
            "System.Collections" => ["Generic", "Concurrent", "Specialized", "ObjectModel"],
            "System.IO" => ["Compression", "Pipes"],
            "System.Text" => ["Json", "RegularExpressions", "Encoding"],
            "System.Threading" => ["Tasks", "Channels"],
            "System.Net" => ["Http", "Sockets", "Security"],
            "System.Reflection" => ["Emit", "Metadata"],
            "System.Runtime" => ["CompilerServices", "InteropServices", "Serialization"],
            "Microsoft" => ["Extensions", "CSharp", "VisualBasic"],
            "Microsoft.Extensions" => ["DependencyInjection", "Logging", "Configuration", "Hosting", "Options"],
            _ => []
        };
    }

    private static CompleteResult EmptyResult() => new()
    {
        Completion = new Completion
        {
            Values = [],
            Total = 0,
            HasMore = false
        }
    };

    private static CompleteResult CreateResult(IReadOnlyList<string> completions)
    {
        var values = completions.Take(100).ToList();
        return new CompleteResult
        {
            Completion = new Completion
            {
                Values = values,
                Total = completions.Count,
                HasMore = completions.Count > 100
            }
        };
    }
}
