namespace Recursion;

public static class RecursionUtil
{
    public static string GetName() => "Recursion";
}

public static class RecursiveCalculator
{
    public static long Factorial(int n)
    {
        // Base case
        if (n <= 1)
        {
            var result = 1L;
            return result; // Breakpoint-friendly: separate line for return
        }

        // Recursive case
        var recursiveResult = Factorial(n - 1);
        var multiplied = n * recursiveResult;
        return multiplied; // Breakpoint-friendly: separate line for return
    }

    public static int Fibonacci(int n)
    {
        if (n <= 0)
        {
            return 0;
        }
        if (n == 1)
        {
            return 1;
        }

        var fib1 = Fibonacci(n - 1);
        var fib2 = Fibonacci(n - 2);
        var sum = fib1 + fib2;
        return sum;
    }
}
