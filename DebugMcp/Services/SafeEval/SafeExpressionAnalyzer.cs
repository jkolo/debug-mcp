using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DebugMcp.Services.SafeEval;

public sealed class SafeExpressionAnalyzer(SafeEvalAllowlist allowlist) : ISafeExpressionAnalyzer
{
    public SafeAnalysisResult Analyze(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return SafeAnalysisResult.Rejected(new SafeEvalRejection(
                RejectionCategory.ParseError, expression, "Expression cannot be empty"));

        var wrapped = $"_ = {expression};";
        var tree = CSharpSyntaxTree.ParseText(
            wrapped,
            CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));

        var errors = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            return SafeAnalysisResult.Rejected(new SafeEvalRejection(
                RejectionCategory.ParseError,
                expression,
                $"Expression could not be parsed: {errors[0].GetMessage()}"));

        // Walk only the inner expression (right-hand side of the _ = {expr} wrapper).
        var root = tree.GetRoot();
        var innerExpr = root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault()
            ?.Right;

        var walker = new SafetyWalker(allowlist);
        walker.Visit(innerExpr ?? root);

        return walker.Rejection != null
            ? SafeAnalysisResult.Rejected(walker.Rejection)
            : SafeAnalysisResult.Allowed();
    }

    private sealed class SafetyWalker(SafeEvalAllowlist allowlist) : CSharpSyntaxWalker
    {
        public SafeEvalRejection? Rejection { get; private set; }

        public override void Visit(SyntaxNode? node)
        {
            if (Rejection != null) return; // fail-fast
            base.Visit(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (Rejection != null) return;

            var (receiver, method) = ExtractReceiverAndMethod(node);
            if (!allowlist.IsAllowed(receiver, method))
            {
                var offending = node.Expression.ToString();
                Rejection = new SafeEvalRejection(
                    RejectionCategory.MethodCall,
                    offending,
                    $"Method call '{offending}' is not on the safe-eval allowlist");
                return;
            }

            // Allowlisted method — but still must check its arguments
            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            if (Rejection != null) return;
            Rejection = new SafeEvalRejection(
                RejectionCategory.ObjectCreation,
                node.ToString(),
                "Object construction is not permitted in safe mode");
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (Rejection != null) return;
            Rejection = new SafeEvalRejection(
                RejectionCategory.Assignment,
                node.ToString(),
                "Assignment expressions are not permitted in safe mode");
        }

        private static (string receiver, string method) ExtractReceiverAndMethod(
            InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var method = memberAccess.Name.Identifier.Text;
                var receiver = ExtractSimpleName(memberAccess.Expression);
                return (receiver, method);
            }

            if (node.Expression is IdentifierNameSyntax identifier)
                return (string.Empty, identifier.Identifier.Text);

            return (string.Empty, node.Expression.ToString());
        }

        private static string ExtractSimpleName(ExpressionSyntax expr)
        {
            return expr switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => expr.ToString()
            };
        }
    }
}
