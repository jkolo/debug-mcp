using System.Reflection;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Server;

namespace DebugMcp.Tests.Support;

/// <summary>
/// Reflection helpers for US3's contract tests (T039–T042). Every migrated tool method returns
/// <c>Task&lt;TFlatResult&gt;</c> where <c>TFlatResult</c> is a flat positional record: a
/// <c>Success</c> field, the tool's own domain fields (each defaulted so a failure result, which
/// omits them, still satisfies its own schema — see data-model.md §1's requiredness correction),
/// and an <c>Error</c> field of type <c>DebugMcp.Models.Results.ToolError?</c>.
/// </summary>
public static class ToolResultShape
{
    /// <summary>Unwraps a tool method's <c>Task&lt;T&gt;</c> return type to <c>T</c>.</summary>
    public static Type GetResultType(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            throw new InvalidOperationException(
                $"{method.DeclaringType?.Name}.{method.Name} must return Task<T> for a structured-content tool; found {returnType}.");
        }
        return returnType.GetGenericArguments()[0];
    }

    /// <summary>
    /// Constructs an instance of a flat result record via its primary constructor: forces
    /// <c>Success</c> to <paramref name="success"/> and leaves every other parameter at its
    /// declared default (or the type's zero value if it has none), which is exactly the shape a
    /// real failure result takes on the wire — every domain field absent.
    /// </summary>
    public static object BuildInstance(Type resultType, bool success)
    {
        var ctor = resultType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"{resultType.Name} has no public constructor.");

        var args = ctor.GetParameters().Select(p =>
            string.Equals(p.Name, "Success", StringComparison.OrdinalIgnoreCase)
                ? (object)success
                : p.HasDefaultValue
                    ? p.DefaultValue
                    : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();

        return ctor.Invoke(args);
    }

    /// <summary>An uninitialized instance of a tool type, sufficient for schema-only reflection via <see cref="McpServerTool.Create(MethodInfo, object, McpServerToolCreateOptions)"/> — never invoked.</summary>
    public static object UninitializedToolInstance(Type toolType) => RuntimeHelpers.GetUninitializedObject(toolType);
}
