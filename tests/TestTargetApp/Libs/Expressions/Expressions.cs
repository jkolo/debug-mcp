namespace Expressions;

public static class ExpressionsUtil
{
    public static string GetName() => $"Expressions(dep:{Collections.CollectionsUtil.GetName()},{Exceptions.ExceptionsUtil.GetName()})";
}

public class ExpressionTarget
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
    public ExpressionTarget? Inner { get; set; }

    public int ComputeSum(int a, int b)
    {
        return a + b;
    }

    /// <summary>
    /// Test method with local variables for expression evaluation testing.
    /// Set a breakpoint on the return statement to have all variables in scope.
    /// </summary>
    public int TestExpressions(int input)
    {
        var localInt = input * 2;
        var localString = "Hello, World!";
        var localBool = input > 0;
        ExpressionTarget? localNull = null;
        var result = localInt + Value;
        // Breakpoint here: line 29 - all locals, this, and Inner in scope
        return result;
    }

    public string GetDescription()
    {
        return $"{Name}: {Value}";
    }

    public static ExpressionTarget Create()
    {
        return new ExpressionTarget
        {
            Name = "TestTarget",
            Value = 42,
            Inner = new ExpressionTarget
            {
                Name = "InnerTarget",
                Value = 100,
                Inner = null
            }
        };
    }

    public static ExpressionTarget CreateWithNull()
    {
        return new ExpressionTarget
        {
            Name = "OuterOnly",
            Value = 10,
            Inner = null
        };
    }
}
