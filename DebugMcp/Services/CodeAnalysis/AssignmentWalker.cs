using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DebugMcp.Models.CodeAnalysis;

namespace DebugMcp.Services.CodeAnalysis;

/// <summary>
/// Syntax walker that finds all assignments to a target symbol.
/// </summary>
internal sealed class AssignmentWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly ISymbol _targetSymbol;
    private readonly List<SymbolAssignment> _assignments = [];

    public AssignmentWalker(SemanticModel semanticModel, ISymbol targetSymbol)
    {
        _semanticModel = semanticModel;
        _targetSymbol = targetSymbol;
    }

    public IReadOnlyList<SymbolAssignment> Assignments => _assignments;

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        // Check if this is a declaration of our target symbol
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol is not null && SymbolEqualityComparer.Default.Equals(symbol, _targetSymbol))
        {
            // If there's an initializer, it's an assignment
            if (node.Initializer is not null)
            {
                AddAssignment(node, AssignmentKind.Declaration, "=", node.Initializer.Value.ToString());
            }
            else
            {
                // Declaration without initializer
                AddAssignment(node, AssignmentKind.Declaration, null, null);
            }
        }

        base.VisitVariableDeclarator(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        // Check if the left side is our target symbol
        var leftSymbol = _semanticModel.GetSymbolInfo(node.Left).Symbol;
        if (leftSymbol is not null && SymbolEqualityComparer.Default.Equals(leftSymbol, _targetSymbol))
        {
            var kind = node.Kind() switch
            {
                SyntaxKind.SimpleAssignmentExpression => AssignmentKind.Simple,
                SyntaxKind.AddAssignmentExpression or
                SyntaxKind.SubtractAssignmentExpression or
                SyntaxKind.MultiplyAssignmentExpression or
                SyntaxKind.DivideAssignmentExpression or
                SyntaxKind.ModuloAssignmentExpression or
                SyntaxKind.AndAssignmentExpression or
                SyntaxKind.OrAssignmentExpression or
                SyntaxKind.ExclusiveOrAssignmentExpression or
                SyntaxKind.LeftShiftAssignmentExpression or
                SyntaxKind.RightShiftAssignmentExpression or
                SyntaxKind.CoalesceAssignmentExpression => AssignmentKind.Compound,
                _ => AssignmentKind.Simple
            };

            AddAssignment(node, kind, node.OperatorToken.Text, node.Right.ToString());
        }

        base.VisitAssignmentExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression)
        {
            var operandSymbol = _semanticModel.GetSymbolInfo(node.Operand).Symbol;
            if (operandSymbol is not null && SymbolEqualityComparer.Default.Equals(operandSymbol, _targetSymbol))
            {
                var kind = node.Kind() == SyntaxKind.PreIncrementExpression
                    ? AssignmentKind.Increment
                    : AssignmentKind.Decrement;

                AddAssignment(node, kind, node.OperatorToken.Text, null);
            }
        }

        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        if (node.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression)
        {
            var operandSymbol = _semanticModel.GetSymbolInfo(node.Operand).Symbol;
            if (operandSymbol is not null && SymbolEqualityComparer.Default.Equals(operandSymbol, _targetSymbol))
            {
                var kind = node.Kind() == SyntaxKind.PostIncrementExpression
                    ? AssignmentKind.Increment
                    : AssignmentKind.Decrement;

                AddAssignment(node, kind, node.OperatorToken.Text, null);
            }
        }

        base.VisitPostfixUnaryExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        // Check for out/ref parameters
        if (node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) || node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            var argSymbol = _semanticModel.GetSymbolInfo(node.Expression).Symbol;
            if (argSymbol is not null && SymbolEqualityComparer.Default.Equals(argSymbol, _targetSymbol))
            {
                var kind = node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
                    ? AssignmentKind.OutParameter
                    : AssignmentKind.RefParameter;

                AddAssignment(node, kind, node.RefKindKeyword.Text, null);
            }
        }

        base.VisitArgument(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        // Check if this is our target property with an initializer
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol is not null && SymbolEqualityComparer.Default.Equals(symbol, _targetSymbol))
        {
            if (node.Initializer is not null)
            {
                AddAssignment(node, AssignmentKind.Initializer, "=", node.Initializer.Value.ToString());
            }
        }

        base.VisitPropertyDeclaration(node);
    }

    public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        // Handle field initializers
        if (node.Parent is VariableDeclaratorSyntax declarator)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(declarator);
            if (symbol is not null && SymbolEqualityComparer.Default.Equals(symbol, _targetSymbol))
            {
                // Already handled in VisitVariableDeclarator
            }
        }

        base.VisitEqualsValueClause(node);
    }

    private void AddAssignment(SyntaxNode node, AssignmentKind kind, string? operatorText, string? valueExpression)
    {
        var location = node.GetLocation();
        var lineSpan = location.GetLineSpan();

        _assignments.Add(new SymbolAssignment
        {
            File = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            EndColumn = lineSpan.EndLinePosition.Character + 1,
            Kind = kind,
            Context = GetContainingMemberName(node),
            Operator = operatorText,
            ValueExpression = TruncateIfNeeded(valueExpression, 100)
        });
    }

    private static string? GetContainingMemberName(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.Text;
                case PropertyDeclarationSyntax property:
                    return property.Identifier.Text;
                case ConstructorDeclarationSyntax:
                    return ".ctor";
                case ClassDeclarationSyntax cls:
                    return cls.Identifier.Text;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? TruncateIfNeeded(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }
        return value[..(maxLength - 3)] + "...";
    }
}
