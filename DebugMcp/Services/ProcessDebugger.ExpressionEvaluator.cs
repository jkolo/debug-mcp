using System.Globalization;
using System.Runtime.InteropServices;
using ClrDebug;
using DebugMcp.Models.Inspection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DebugMcp.Services;

/// <summary>
/// Roslyn-backed C# expression evaluator (BUG-010). Parses an expression into a syntax tree and
/// walks it node-by-node against the live debuggee via ICorDebug: literals, identifiers
/// (locals/arguments/<c>this</c> fields), member access (fields + property getters), indexers,
/// method calls, the full set of arithmetic/comparison/logical/bitwise operators, conditional
/// (<c>?:</c>) expressions, casts and string interpolation.
///
/// Genuine limits (reported as a clear <c>not_supported</c> error rather than a misleading
/// syntax error): lambdas / LINQ query &amp; method syntax (these require compiling and injecting
/// IL into the debuggee), and reference-typed method/indexer arguments (strings, objects) which
/// cannot be materialised synchronously through ICorDebugEval.
/// </summary>
public sealed partial class ProcessDebugger
{
    /// <summary>Per-evaluation context. The IL frame is re-acquired on demand because a
    /// nested func-eval (property getter / method call) resumes the process and invalidates any
    /// previously obtained frame.</summary>
    private sealed class EvalContext(int? threadId, int frameIndex, CorDebugThread thread, int timeoutMs, CancellationToken ct)
    {
        public int? ThreadId { get; } = threadId;
        public int FrameIndex { get; } = frameIndex;
        public CorDebugThread Thread { get; } = thread;
        public int TimeoutMs { get; } = timeoutMs;
        public CancellationToken Ct { get; } = ct;
    }

    /// <summary>A value produced during evaluation: either a live debuggee value or a host-side literal.</summary>
    private readonly struct EvalValue
    {
        public CorDebugValue? Debuggee { get; }
        public object? Literal { get; }   // boxed bool/char/sbyte..double/string, or null
        public bool IsLiteral { get; }

        private EvalValue(CorDebugValue? debuggee, object? literal, bool isLiteral)
        {
            Debuggee = debuggee;
            Literal = literal;
            IsLiteral = isLiteral;
        }

        public static EvalValue FromDebuggee(CorDebugValue value) => new(value, null, false);
        public static EvalValue FromLiteral(object? value) => new(null, value, true);
        public static readonly EvalValue NullLiteral = new(null, null, true);
    }

    private sealed class EvalNotSupportedException(string message) : Exception(message);

    private sealed class EvalFailureException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    /// <summary>
    /// Attempts to evaluate <paramref name="expression"/> as a full C# expression tree.
    /// Returns <c>null</c> only when the text cannot be parsed (so the caller can fall back),
    /// otherwise a success or a structured error result.
    /// </summary>
    private async Task<EvaluationResult?> TryEvaluateExpressionTreeAsync(
        string expression,
        int? threadId,
        int frameIndex,
        CorDebugThread thread,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax syntax;
        try
        {
            syntax = SyntaxFactory.ParseExpression(expression);
            if (syntax.ContainsDiagnostics &&
                syntax.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return null;
            }
            if (syntax.Span.Length == 0)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        var ctx = new EvalContext(threadId, frameIndex, thread, timeoutMs, cancellationToken);
        try
        {
            var value = await EvalNodeAsync(syntax, ctx);
            return ToEvaluationResult(value);
        }
        catch (EvalNotSupportedException ex)
        {
            return new EvaluationResult(Success: false,
                Error: new EvaluationError("not_supported", ex.Message));
        }
        catch (EvalFailureException ex)
        {
            return new EvaluationResult(Success: false,
                Error: new EvaluationError(ex.Code, ex.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EvaluationResult(Success: false,
                Error: new EvaluationError("eval_exception", ex.Message, ExceptionType: ex.GetType().FullName));
        }
    }

    private CorDebugILFrame RequireFrame(EvalContext ctx)
    {
        var frame = GetILFrame(ctx.ThreadId, ctx.FrameIndex);
        if (frame == null)
            throw new EvalFailureException("variable_unavailable", "Could not access the stack frame for evaluation");
        return frame;
    }

    private async Task<EvalValue> EvalNodeAsync(ExpressionSyntax node, EvalContext ctx)
    {
        ctx.Ct.ThrowIfCancellationRequested();

        switch (node)
        {
            case ParenthesizedExpressionSyntax paren:
                return await EvalNodeAsync(paren.Expression, ctx);

            case LiteralExpressionSyntax literal:
                return EvalLiteral(literal);

            case PrefixUnaryExpressionSyntax unary:
                return EvalUnary(unary.Kind(), await EvalNodeAsync(unary.Operand, ctx));

            case BinaryExpressionSyntax binary:
                return await EvalBinaryAsync(binary, ctx);

            case ConditionalExpressionSyntax cond:
            {
                var test = await EvalNodeAsync(cond.Condition, ctx);
                return AsBool(test)
                    ? await EvalNodeAsync(cond.WhenTrue, ctx)
                    : await EvalNodeAsync(cond.WhenFalse, ctx);
            }

            case CastExpressionSyntax cast:
                return EvalCast(cast.Type.ToString().Trim(), await EvalNodeAsync(cast.Expression, ctx));

            case IdentifierNameSyntax identifier:
                return await EvalIdentifierAsync(identifier.Identifier.ValueText, ctx);

            case MemberAccessExpressionSyntax member:
            {
                var target = await EvalNodeAsync(member.Expression, ctx);
                return await EvalMemberAsync(target, member.Name.Identifier.ValueText, ctx);
            }

            case ElementAccessExpressionSyntax element:
                return await EvalElementAccessAsync(element, ctx);

            case InvocationExpressionSyntax invocation:
                return await EvalInvocationAsync(invocation, ctx);

            case InterpolatedStringExpressionSyntax interpolated:
                return await EvalInterpolatedStringAsync(interpolated, ctx);

            case ThisExpressionSyntax:
            {
                var thisValue = TryGetThisForEval(RequireFrame(ctx));
                if (thisValue == null)
                    throw new EvalFailureException("variable_unavailable", "'this' is not available in the current frame");
                return EvalValue.FromDebuggee(thisValue);
            }

            case PostfixUnaryExpressionSyntax:
                throw new EvalNotSupportedException("Increment/decrement operators are not supported in expression evaluation");

            case LambdaExpressionSyntax:
            case QueryExpressionSyntax:
                throw new EvalNotSupportedException("Lambda and LINQ query expressions are not supported (they require compiling code into the debuggee)");

            default:
                throw new EvalNotSupportedException($"Expression form '{node.Kind()}' is not supported");
        }
    }

    private static EvalValue EvalLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() switch
        {
            SyntaxKind.NullLiteralExpression => EvalValue.NullLiteral,
            SyntaxKind.TrueLiteralExpression => EvalValue.FromLiteral(true),
            SyntaxKind.FalseLiteralExpression => EvalValue.FromLiteral(false),
            _ => EvalValue.FromLiteral(literal.Token.Value)
        };
    }

    private async Task<EvalValue> EvalIdentifierAsync(string name, EvalContext ctx)
    {
        var frame = RequireFrame(ctx);

        if (name == "this")
        {
            var thisValue = TryGetThisForEval(frame);
            if (thisValue != null) return EvalValue.FromDebuggee(thisValue);
            throw new EvalFailureException("variable_unavailable", "'this' is not available in the current frame");
        }

        var local = TryGetLocalOrArgument(name, frame);
        if (local != null) return EvalValue.FromDebuggee(local);

        // Fall back to an implicit `this.<name>` so bare instance fields/properties resolve.
        var thisRef = TryGetThisForEval(frame);
        if (thisRef != null)
        {
            var member = await TryGetMemberValueAsync(thisRef, name, ctx.Thread, ctx.TimeoutMs, ctx.Ct);
            if (member != null) return EvalValue.FromDebuggee(member);
        }

        throw new EvalFailureException("variable_unavailable", $"Unknown identifier '{name}'");
    }

    private async Task<EvalValue> EvalMemberAsync(EvalValue target, string memberName, EvalContext ctx)
    {
        // Member access on a primitive/string (host literal or a debuggee primitive) → reflection.
        var hostTarget = AsHostObject(target);
        if (hostTarget != null)
        {
            if (hostTarget == NullSentinel)
                throw new EvalFailureException("eval_exception", $"Cannot access '{memberName}' on a null value");
            var hostType = hostTarget.GetType();
            var prop = hostType.GetProperty(memberName);
            if (prop != null) return EvalValue.FromLiteral(prop.GetValue(hostTarget));
            var field = hostType.GetField(memberName);
            if (field != null) return EvalValue.FromLiteral(field.GetValue(hostTarget));
            throw new EvalFailureException("variable_unavailable",
                $"Member '{memberName}' not found on {hostType.FullName}");
        }

        var value = await TryGetMemberValueAsync(target.Debuggee!, memberName, ctx.Thread, ctx.TimeoutMs, ctx.Ct);
        if (value == null)
            throw new EvalFailureException("variable_unavailable",
                $"Member '{memberName}' not found on '{GetTypeName(target.Debuggee!)}'");
        return EvalValue.FromDebuggee(value);
    }

    private async Task<EvalValue> EvalElementAccessAsync(ElementAccessExpressionSyntax element, EvalContext ctx)
    {
        var target = await EvalNodeAsync(element.Expression, ctx);
        if (element.ArgumentList.Arguments.Count != 1)
            throw new EvalNotSupportedException("Only single-dimension indexers are supported");

        var indexValue = await EvalNodeAsync(element.ArgumentList.Arguments[0].Expression, ctx);

        // Host string literal indexer.
        if (target.IsLiteral)
        {
            if (target.Literal is string s)
            {
                var idx = (int)RequireLong(indexValue, "index");
                if (idx < 0 || idx >= s.Length)
                    throw new EvalFailureException("eval_exception", $"Index {idx} out of range for string of length {s.Length}");
                return EvalValue.FromLiteral(s[idx]);
            }
            throw new EvalNotSupportedException("Indexer not supported on this value");
        }

        var resolved = DereferenceForInspection(target.Debuggee!);

        if (resolved is CorDebugArrayValue arrayValue)
        {
            var idx = (int)RequireLong(indexValue, "index");
            var count = (int)arrayValue.Count;
            if (idx < 0 || idx >= count)
                throw new EvalFailureException("eval_exception", $"Index {idx} out of range for array of length {count}");
            return EvalValue.FromDebuggee(arrayValue.GetElementAtPosition(idx));
        }

        if (resolved is CorDebugStringValue stringValue)
        {
            var str = stringValue.GetString((int)stringValue.Length + 1) ?? "";
            var idx = (int)RequireLong(indexValue, "index");
            if (idx < 0 || idx >= str.Length)
                throw new EvalFailureException("eval_exception", $"Index {idx} out of range for string of length {str.Length}");
            return EvalValue.FromLiteral(str[idx]);
        }

        // Indexer property get_Item(index) via func-eval (List<T>, Dictionary<K,V>, IList, …).
        var getItem = FindMethod(target.Debuggee!, "get_Item");
        if (getItem == null)
            throw new EvalNotSupportedException($"Type '{GetTypeName(target.Debuggee!)}' has no supported indexer");

        var callResult = await CallFunctionAsync(ctx.Thread, getItem, target.Debuggee!, null,
            GetTypeArguments(target.Debuggee!), ctx.TimeoutMs, ctx.Ct, hostArgs: [LiteralToHostArg(indexValue)]);
        if (!callResult.Success || callResult.Value == null)
            throw new EvalFailureException("eval_exception",
                callResult.Exception?.Message ?? "Indexer evaluation failed");
        return EvalValue.FromDebuggee(callResult.Value);
    }

    private async Task<EvalValue> EvalInvocationAsync(InvocationExpressionSyntax invocation, EvalContext ctx)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            throw new EvalNotSupportedException("Only instance method calls of the form 'target.Method(args)' are supported");

        var methodName = memberAccess.Name.Identifier.ValueText;
        var target = await EvalNodeAsync(memberAccess.Expression, ctx);

        var args = new List<EvalValue>();
        foreach (var arg in invocation.ArgumentList.Arguments)
            args.Add(await EvalNodeAsync(arg.Expression, ctx));

        // Primitive/string target (host literal or debuggee primitive) → reflection on the host copy.
        var hostTarget = AsHostObject(target);
        if (hostTarget != null)
        {
            if (hostTarget == NullSentinel)
                throw new EvalFailureException("eval_exception", $"Cannot call '{methodName}' on a null value");
            return InvokeHostMethod(hostTarget, methodName, args);
        }

        var method = FindMethod(target.Debuggee!, methodName);
        if (method == null)
            throw new EvalFailureException("variable_unavailable",
                $"Method '{methodName}' not found on '{GetTypeName(target.Debuggee!)}'");

        var hostArgs = args.Select(LiteralToHostArg).ToList();
        var callResult = await CallFunctionAsync(ctx.Thread, method, target.Debuggee!, null,
            GetTypeArguments(target.Debuggee!), ctx.TimeoutMs, ctx.Ct, hostArgs: hostArgs);
        if (!callResult.Success)
            throw new EvalFailureException("eval_exception",
                callResult.Exception?.Message ?? $"Call to '{methodName}' failed");
        return callResult.Value == null ? EvalValue.NullLiteral : EvalValue.FromDebuggee(callResult.Value);
    }

    private static EvalValue InvokeHostMethod(object target, string methodName, List<EvalValue> args)
    {
        var hostArgs = new object?[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            if (!args[i].IsLiteral)
                throw new EvalNotSupportedException("Passing debuggee objects to methods on primitive values is not supported");
            hostArgs[i] = args[i].Literal;
        }

        var method = target.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Count);
        if (method == null)
            throw new EvalFailureException("variable_unavailable",
                $"Method '{methodName}' with {args.Count} argument(s) not found on {target.GetType().FullName}");

        try
        {
            return EvalValue.FromLiteral(method.Invoke(target, hostArgs));
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            throw new EvalFailureException("eval_exception", tie.InnerException?.Message ?? tie.Message);
        }
    }

    private async Task<EvalValue> EvalInterpolatedStringAsync(InterpolatedStringExpressionSyntax interpolated, EvalContext ctx)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(text.TextToken.ValueText);
                    break;
                case InterpolationSyntax interp:
                {
                    var value = await EvalNodeAsync(interp.Expression, ctx);
                    sb.Append(ToDisplayString(value, quoteStrings: false));
                    break;
                }
            }
        }
        return EvalValue.FromLiteral(sb.ToString());
    }

    // ---- Operators -------------------------------------------------------------------

    private EvalValue EvalUnary(SyntaxKind kind, EvalValue operand)
    {
        switch (kind)
        {
            case SyntaxKind.UnaryMinusExpression:
                return IsFloating(operand) ? EvalValue.FromLiteral(-AsDouble(operand)) : EvalValue.FromLiteral(-AsLong(operand));
            case SyntaxKind.UnaryPlusExpression:
                return operand;
            case SyntaxKind.LogicalNotExpression:
                return EvalValue.FromLiteral(!AsBool(operand));
            case SyntaxKind.BitwiseNotExpression:
                return EvalValue.FromLiteral(~AsLong(operand));
            default:
                throw new EvalNotSupportedException($"Unary operator '{kind}' is not supported");
        }
    }

    private async Task<EvalValue> EvalBinaryAsync(BinaryExpressionSyntax binary, EvalContext ctx)
    {
        var kind = binary.Kind();

        if (kind == SyntaxKind.LogicalAndExpression)
        {
            var left = await EvalNodeAsync(binary.Left, ctx);
            if (!AsBool(left)) return EvalValue.FromLiteral(false);
            return EvalValue.FromLiteral(AsBool(await EvalNodeAsync(binary.Right, ctx)));
        }
        if (kind == SyntaxKind.LogicalOrExpression)
        {
            var left = await EvalNodeAsync(binary.Left, ctx);
            if (AsBool(left)) return EvalValue.FromLiteral(true);
            return EvalValue.FromLiteral(AsBool(await EvalNodeAsync(binary.Right, ctx)));
        }

        var l = await EvalNodeAsync(binary.Left, ctx);
        var r = await EvalNodeAsync(binary.Right, ctx);

        if (kind == SyntaxKind.AddExpression && (IsString(l) || IsString(r)))
            return EvalValue.FromLiteral(ToDisplayString(l, quoteStrings: false) + ToDisplayString(r, quoteStrings: false));

        switch (kind)
        {
            case SyntaxKind.EqualsExpression: return EvalValue.FromLiteral(AreEqual(l, r));
            case SyntaxKind.NotEqualsExpression: return EvalValue.FromLiteral(!AreEqual(l, r));
        }

        bool floating = IsFloating(l) || IsFloating(r);
        if (floating)
        {
            double a = AsDouble(l), b = AsDouble(r);
            return kind switch
            {
                SyntaxKind.AddExpression => EvalValue.FromLiteral(a + b),
                SyntaxKind.SubtractExpression => EvalValue.FromLiteral(a - b),
                SyntaxKind.MultiplyExpression => EvalValue.FromLiteral(a * b),
                SyntaxKind.DivideExpression => EvalValue.FromLiteral(a / b),
                SyntaxKind.ModuloExpression => EvalValue.FromLiteral(a % b),
                SyntaxKind.LessThanExpression => EvalValue.FromLiteral(a < b),
                SyntaxKind.GreaterThanExpression => EvalValue.FromLiteral(a > b),
                SyntaxKind.LessThanOrEqualExpression => EvalValue.FromLiteral(a <= b),
                SyntaxKind.GreaterThanOrEqualExpression => EvalValue.FromLiteral(a >= b),
                _ => throw new EvalNotSupportedException($"Operator '{kind}' is not supported on floating-point values")
            };
        }

        long la = AsLong(l), lb = AsLong(r);
        return kind switch
        {
            SyntaxKind.AddExpression => EvalValue.FromLiteral(la + lb),
            SyntaxKind.SubtractExpression => EvalValue.FromLiteral(la - lb),
            SyntaxKind.MultiplyExpression => EvalValue.FromLiteral(la * lb),
            SyntaxKind.DivideExpression => lb == 0
                ? throw new EvalFailureException("eval_exception", "Attempted to divide by zero")
                : EvalValue.FromLiteral(la / lb),
            SyntaxKind.ModuloExpression => lb == 0
                ? throw new EvalFailureException("eval_exception", "Attempted to divide by zero")
                : EvalValue.FromLiteral(la % lb),
            SyntaxKind.LessThanExpression => EvalValue.FromLiteral(la < lb),
            SyntaxKind.GreaterThanExpression => EvalValue.FromLiteral(la > lb),
            SyntaxKind.LessThanOrEqualExpression => EvalValue.FromLiteral(la <= lb),
            SyntaxKind.GreaterThanOrEqualExpression => EvalValue.FromLiteral(la >= lb),
            SyntaxKind.BitwiseAndExpression => EvalValue.FromLiteral(la & lb),
            SyntaxKind.BitwiseOrExpression => EvalValue.FromLiteral(la | lb),
            SyntaxKind.ExclusiveOrExpression => EvalValue.FromLiteral(la ^ lb),
            SyntaxKind.LeftShiftExpression => EvalValue.FromLiteral(la << (int)lb),
            SyntaxKind.RightShiftExpression => EvalValue.FromLiteral(la >> (int)lb),
            _ => throw new EvalNotSupportedException($"Operator '{kind}' is not supported")
        };
    }

    private EvalValue EvalCast(string typeName, EvalValue value)
    {
        var simple = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
        try
        {
            return simple switch
            {
                "int" or "Int32" => EvalValue.FromLiteral((int)AsLong(value)),
                "long" or "Int64" => EvalValue.FromLiteral(AsLong(value)),
                "short" or "Int16" => EvalValue.FromLiteral((short)AsLong(value)),
                "byte" or "Byte" => EvalValue.FromLiteral((byte)AsLong(value)),
                "sbyte" or "SByte" => EvalValue.FromLiteral((sbyte)AsLong(value)),
                "uint" or "UInt32" => EvalValue.FromLiteral((uint)AsLong(value)),
                "ulong" or "UInt64" => EvalValue.FromLiteral((ulong)AsLong(value)),
                "double" or "Double" => EvalValue.FromLiteral(AsDouble(value)),
                "float" or "Single" => EvalValue.FromLiteral((float)AsDouble(value)),
                "decimal" or "Decimal" => EvalValue.FromLiteral((decimal)AsDouble(value)),
                "char" or "Char" => EvalValue.FromLiteral((char)AsLong(value)),
                "bool" or "Boolean" => EvalValue.FromLiteral(AsBool(value)),
                _ => throw new EvalNotSupportedException($"Cast to '{typeName}' is not supported")
            };
        }
        catch (EvalNotSupportedException) { throw; }
        catch (EvalFailureException) { throw; }
        catch (Exception ex)
        {
            throw new EvalFailureException("eval_exception", $"Cast to '{typeName}' failed: {ex.Message}");
        }
    }

    // ---- Coercion helpers ------------------------------------------------------------

    /// <summary>Sentinel returned by <see cref="AsHostObject"/> for an explicit/debuggee null.</summary>
    private static readonly object NullSentinel = new();

    /// <summary>
    /// Returns a host object usable for reflection when the value is a primitive/string —
    /// a host literal, or a debuggee value that reads back as a primitive/string. Returns
    /// <c>null</c> when the value is a real debuggee object (so callers use func-eval), and
    /// <see cref="NullSentinel"/> for an explicit null.
    /// </summary>
    private object? AsHostObject(EvalValue value)
    {
        if (value.IsLiteral)
            return value.Literal ?? NullSentinel;
        if (value.Debuggee is CorDebugReferenceValue { IsNull: true })
            return NullSentinel;
        return TryReadDebuggeePrimitive(value.Debuggee!);
    }

    /// <summary>Reads a debuggee value as a host primitive (boxed), or returns null if it isn't one.</summary>
    private object? TryReadDebuggeePrimitive(CorDebugValue value)
    {
        var resolved = DereferenceForInspection(value);

        if (resolved is CorDebugStringValue stringValue)
            return stringValue.GetString((int)stringValue.Length + 1) ?? "";

        if (resolved is CorDebugGenericValue generic)
        {
            try
            {
                var size = generic.Size;
                var bytes = new byte[size];
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try { generic.GetValue(handle.AddrOfPinnedObject()); }
                finally { handle.Free(); }

                return generic.Type switch
                {
                    CorElementType.Boolean => BitConverter.ToBoolean(bytes, 0),
                    CorElementType.Char => (char)BitConverter.ToInt16(bytes, 0),
                    CorElementType.I1 => (sbyte)bytes[0],
                    CorElementType.U1 => bytes[0],
                    CorElementType.I2 => BitConverter.ToInt16(bytes, 0),
                    CorElementType.U2 => BitConverter.ToUInt16(bytes, 0),
                    CorElementType.I4 => BitConverter.ToInt32(bytes, 0),
                    CorElementType.U4 => BitConverter.ToUInt32(bytes, 0),
                    CorElementType.I8 => BitConverter.ToInt64(bytes, 0),
                    CorElementType.U8 => BitConverter.ToUInt64(bytes, 0),
                    CorElementType.R4 => BitConverter.ToSingle(bytes, 0),
                    CorElementType.R8 => BitConverter.ToDouble(bytes, 0),
                    CorElementType.I => IntPtr.Size == 4 ? BitConverter.ToInt32(bytes, 0) : BitConverter.ToInt64(bytes, 0),
                    CorElementType.U => IntPtr.Size == 4 ? BitConverter.ToUInt32(bytes, 0) : (object)BitConverter.ToUInt64(bytes, 0),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private object RequirePrimitive(EvalValue value, string role)
    {
        if (value.IsLiteral)
        {
            if (value.Literal == null)
                throw new EvalFailureException("eval_exception", $"Cannot use null as {role}");
            return value.Literal;
        }
        var primitive = TryReadDebuggeePrimitive(value.Debuggee!);
        if (primitive == null)
            throw new EvalFailureException("eval_exception", $"Value is not a primitive usable as {role}");
        return primitive;
    }

    private long AsLong(EvalValue value) => RequireLong(value, "an integer operand");

    private long RequireLong(EvalValue value, string role)
    {
        var primitive = RequirePrimitive(value, role);
        try
        {
            return primitive switch
            {
                bool b => b ? 1 : 0,
                char c => c,
                _ => Convert.ToInt64(primitive, CultureInfo.InvariantCulture)
            };
        }
        catch
        {
            throw new EvalFailureException("eval_exception", $"Value '{primitive}' is not usable as {role}");
        }
    }

    private double AsDouble(EvalValue value)
    {
        var primitive = RequirePrimitive(value, "a numeric operand");
        try { return Convert.ToDouble(primitive, CultureInfo.InvariantCulture); }
        catch { throw new EvalFailureException("eval_exception", $"Value '{primitive}' is not numeric"); }
    }

    private bool AsBool(EvalValue value)
    {
        var primitive = RequirePrimitive(value, "a boolean operand");
        if (primitive is bool b) return b;
        throw new EvalFailureException("eval_exception", $"Value '{primitive}' is not a boolean");
    }

    private bool IsFloating(EvalValue value)
    {
        var p = value.IsLiteral ? value.Literal : (value.Debuggee != null ? TryReadDebuggeePrimitive(value.Debuggee) : null);
        return p is float or double or decimal;
    }

    private bool IsString(EvalValue value)
    {
        if (value.IsLiteral) return value.Literal is string;
        return value.Debuggee != null && DereferenceForInspection(value.Debuggee) is CorDebugStringValue;
    }

    private bool AreEqual(EvalValue left, EvalValue right)
    {
        bool leftNull = IsNullValue(left), rightNull = IsNullValue(right);
        if (leftNull || rightNull) return leftNull && rightNull;

        if (IsString(left) || IsString(right))
            return ToDisplayString(left, quoteStrings: false) == ToDisplayString(right, quoteStrings: false);

        if (IsFloating(left) || IsFloating(right))
            return AsDouble(left) == AsDouble(right);

        var lp = TryGetComparable(left);
        var rp = TryGetComparable(right);
        if (lp is bool || rp is bool)
            return Equals(lp, rp);
        return AsLong(left) == AsLong(right);
    }

    private object? TryGetComparable(EvalValue value)
        => value.IsLiteral ? value.Literal : (value.Debuggee != null ? TryReadDebuggeePrimitive(value.Debuggee) : null);

    private bool IsNullValue(EvalValue value)
    {
        if (value.IsLiteral) return value.Literal == null;
        return value.Debuggee is CorDebugReferenceValue { IsNull: true };
    }

    /// <summary>Converts an evaluated value into a host arg for func-eval marshaling.</summary>
    private object? LiteralToHostArg(EvalValue value) => value.IsLiteral ? value.Literal : value.Debuggee;

    private CorDebugValue DereferenceForInspection(CorDebugValue value)
    {
        var current = value;
        if (current is CorDebugReferenceValue refValue && !refValue.IsNull)
        {
            var deref = refValue.Dereference();
            if (deref != null) current = deref;
        }
        if (current is CorDebugBoxValue boxValue)
        {
            var unboxed = boxValue.Object;
            if (unboxed != null) current = unboxed;
        }
        return current;
    }

    /// <summary>Creates a debuggee value-type value from a host primitive for func-eval arguments.</summary>
    private CorDebugValue? CreatePrimitiveEvalValue(CorDebugEval eval, object? host)
    {
        if (host == null) return null;       // null reference args not supported here
        if (host is string) return null;     // strings require NewString (async eval) — unsupported

        CorElementType elementType;
        byte[] bytes;
        switch (host)
        {
            case bool b: elementType = CorElementType.Boolean; bytes = [(byte)(b ? 1 : 0)]; break;
            case char c: elementType = CorElementType.Char; bytes = BitConverter.GetBytes((short)c); break;
            case sbyte sb: elementType = CorElementType.I1; bytes = [(byte)sb]; break;
            case byte by: elementType = CorElementType.U1; bytes = [by]; break;
            case short s: elementType = CorElementType.I2; bytes = BitConverter.GetBytes(s); break;
            case ushort us: elementType = CorElementType.U2; bytes = BitConverter.GetBytes(us); break;
            case int i: elementType = CorElementType.I4; bytes = BitConverter.GetBytes(i); break;
            case uint ui: elementType = CorElementType.U4; bytes = BitConverter.GetBytes(ui); break;
            case long l:
                if (l is >= int.MinValue and <= int.MaxValue) { elementType = CorElementType.I4; bytes = BitConverter.GetBytes((int)l); }
                else { elementType = CorElementType.I8; bytes = BitConverter.GetBytes(l); }
                break;
            case ulong ul: elementType = CorElementType.U8; bytes = BitConverter.GetBytes(ul); break;
            case float f: elementType = CorElementType.R4; bytes = BitConverter.GetBytes(f); break;
            case double d: elementType = CorElementType.R8; bytes = BitConverter.GetBytes(d); break;
            default: return null;
        }

        try
        {
            var created = eval.CreateValue(elementType, null);
            if (created is CorDebugGenericValue generic)
            {
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try { generic.SetValue(handle.AddrOfPinnedObject()); }
                finally { handle.Free(); }
            }
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create primitive eval value for {Host}", host);
            return null;
        }
    }

    // ---- Result formatting -----------------------------------------------------------

    private EvaluationResult ToEvaluationResult(EvalValue value)
    {
        if (value.IsLiteral)
        {
            return new EvaluationResult(
                Success: true,
                Value: ToDisplayString(value, quoteStrings: true),
                Type: LiteralTypeName(value.Literal),
                HasChildren: false);
        }

        var (display, typeName, hasChildren, _) = FormatValue(value.Debuggee!);
        // FormatValue can't always name a primitive's CLR type; recover it from the raw value.
        if (typeName is "Unknown" or null)
        {
            var primitive = TryReadDebuggeePrimitive(value.Debuggee!);
            if (primitive != null) typeName = LiteralTypeName(primitive);
        }
        return new EvaluationResult(Success: true, Value: display, Type: typeName, HasChildren: hasChildren);
    }

    private string ToDisplayString(EvalValue value, bool quoteStrings)
    {
        if (value.IsLiteral)
        {
            return value.Literal switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                string s => quoteStrings ? $"\"{s}\"" : s,
                char c => quoteStrings ? $"'{c}'" : c.ToString(),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                var o => o.ToString() ?? "null"
            };
        }

        var (display, _, _, _) = FormatValue(value.Debuggee!);
        if (!quoteStrings && display.Length >= 2 && display[0] == '"' && display[^1] == '"')
            return display[1..^1];
        return display;
    }

    private static string LiteralTypeName(object? literal) => literal switch
    {
        null => "null",
        bool => "System.Boolean",
        char => "System.Char",
        sbyte => "System.SByte",
        byte => "System.Byte",
        short => "System.Int16",
        ushort => "System.UInt16",
        int => "System.Int32",
        uint => "System.UInt32",
        long => "System.Int64",
        ulong => "System.UInt64",
        float => "System.Single",
        double => "System.Double",
        decimal => "System.Decimal",
        string => "System.String",
        _ => literal.GetType().FullName ?? "object"
    };
}
