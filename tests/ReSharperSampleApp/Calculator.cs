namespace ReSharperSampleApp;

public static class Calculator
{
    /// <summary>
    /// Returns a constant. The cast <c>(int)5</c> is redundant — ReSharper flags it
    /// (RedundantCast, WARNING) but the C# compiler does not. The value is used (returned)
    /// so there is no CS0219/CS0169 compiler diagnostic; this keeps the issue ReSharper-only.
    /// </summary>
    public static int Compute()
    {
        int value = (int)5;
        return value;
    }

    /// <summary>
    /// Several ReSharper-only smells of differing native severities, none of which the C#
    /// compiler reports, used to exercise severity extraction in the parser fixture.
    /// </summary>
    public static string Describe(string input)
    {
        // Redundant explicit type argument / redundant string interpolation etc. vary by version;
        // the recorded fixture captures whatever native severities the engine emits here.
        string result = string.Format("{0}", input);
        return result;
    }
}
