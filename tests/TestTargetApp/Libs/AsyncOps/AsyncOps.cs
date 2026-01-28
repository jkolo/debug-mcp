namespace AsyncOps;

public static class AsyncOpsUtil
{
    public static string GetName() => $"AsyncOps(dep:{Recursion.RecursionUtil.GetName()},{Threading.ThreadingUtil.GetName()})";
}
