using DebugMcp.Services.Completions;
using FluentAssertions;
using Xunit;

namespace DebugMcp.Tests.Unit.Completions;

/// <summary>
/// Unit tests for CompletionContextParser.
/// Tests parsing of expression strings into CompletionContext.
/// </summary>
public class CompletionContextParserTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsVariableKind()
    {
        // Arrange
        var expression = "";

        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Variable);
        context.Prefix.Should().Be("");
        context.ObjectExpression.Should().BeNull();
        context.TypeName.Should().BeNull();
    }

    [Theory]
    [InlineData("cust", "cust")]
    [InlineData("customer", "customer")]
    [InlineData("x", "x")]
    [InlineData("_privateVar", "_privateVar")]
    [InlineData("this", "this")]
    public void Parse_SimpleIdentifier_ReturnsVariableKindWithPrefix(string expression, string expectedPrefix)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Variable);
        context.Prefix.Should().Be(expectedPrefix);
        context.ObjectExpression.Should().BeNull();
        context.TypeName.Should().BeNull();
    }

    [Fact]
    public void Parse_ObjectWithDot_ReturnsMemberKind()
    {
        // Arrange
        var expression = "user.";

        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Member);
        context.Prefix.Should().Be("");
        context.ObjectExpression.Should().Be("user");
        context.TypeName.Should().BeNull();
    }

    [Fact]
    public void Parse_ObjectWithPartialMember_ReturnsMemberKindWithPrefix()
    {
        // Arrange
        var expression = "user.Na";

        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Member);
        context.Prefix.Should().Be("Na");
        context.ObjectExpression.Should().Be("user");
        context.TypeName.Should().BeNull();
    }

    [Theory]
    [InlineData("customer.Address.Ci", "customer.Address", "Ci")]
    [InlineData("this.items.Count", "this.items", "Count")]
    [InlineData("obj.Prop1.Prop2.", "obj.Prop1.Prop2", "")]
    public void Parse_NestedObjectAccess_ReturnsMemberKindWithFullPath(
        string expression, string expectedObject, string expectedPrefix)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Member);
        context.Prefix.Should().Be(expectedPrefix);
        context.ObjectExpression.Should().Be(expectedObject);
    }

    [Theory]
    [InlineData("DateTime.", "DateTime")]
    [InlineData("Math.", "Math")]
    [InlineData("String.", "String")]
    [InlineData("Console.", "Console")]
    public void Parse_WellKnownType_ReturnsStaticMemberKind(string expression, string expectedTypeName)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.StaticMember);
        context.Prefix.Should().Be("");
        context.TypeName.Should().Be(expectedTypeName);
        context.ObjectExpression.Should().BeNull();
    }

    [Fact]
    public void Parse_WellKnownTypeWithPartialMember_ReturnsStaticMemberKindWithPrefix()
    {
        // Arrange
        var expression = "DateTime.N";

        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.StaticMember);
        context.Prefix.Should().Be("N");
        context.TypeName.Should().Be("DateTime");
    }

    [Theory]
    [InlineData("System.", "System")]
    [InlineData("System.Collections.", "System.Collections")]
    [InlineData("Microsoft.Extensions.", "Microsoft.Extensions")]
    public void Parse_NamespacePrefix_ReturnsNamespaceKind(string expression, string expectedNamespace)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Namespace);
        context.Prefix.Should().Be("");
        context.TypeName.Should().Be(expectedNamespace);
        context.ObjectExpression.Should().BeNull();
    }

    [Theory]
    [InlineData("System.Col", "System", "Col")]
    [InlineData("System.Collections.Gen", "System.Collections", "Gen")]
    public void Parse_NamespaceWithPartialChild_ReturnsNamespaceKindWithPrefix(
        string expression, string expectedNamespace, string expectedPrefix)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Namespace);
        context.Prefix.Should().Be(expectedPrefix);
        context.TypeName.Should().Be(expectedNamespace);
    }

    [Theory]
    [InlineData("list[0].", "list[0]")]
    [InlineData("dict[\"key\"].", "dict[\"key\"]")]
    [InlineData("array[i].Prop", "array[i]")]
    public void Parse_IndexerAccess_ReturnsMemberKind(string expression, string expectedObject)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Member);
        context.ObjectExpression.Should().Be(expectedObject);
    }

    [Theory]
    [InlineData("GetUser().", "GetUser()")]
    [InlineData("obj.GetItems().", "obj.GetItems()")]
    [InlineData("obj.Method().Prop", "obj.Method()")]
    public void Parse_MethodCallResult_ReturnsMemberKind(string expression, string expectedObject)
    {
        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Member);
        context.ObjectExpression.Should().Be(expectedObject);
    }

    [Fact]
    public void Parse_NullExpression_TreatsAsEmpty()
    {
        // Act
        var context = CompletionContextParser.Parse(null!);

        // Assert
        context.Kind.Should().Be(CompletionKind.Variable);
        context.Prefix.Should().Be("");
    }

    [Fact]
    public void Parse_WhitespaceOnly_TreatsAsEmpty()
    {
        // Arrange
        var expression = "   ";

        // Act
        var context = CompletionContextParser.Parse(expression);

        // Assert
        context.Kind.Should().Be(CompletionKind.Variable);
        context.Prefix.Should().Be("");
    }
}
