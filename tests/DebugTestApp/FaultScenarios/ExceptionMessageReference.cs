namespace DebugTestApp.FaultScenarios;

/// <summary>
/// The exception message names the offending variable by name ("orderId"). Two frames each hold
/// a null-ish local; only the one the message names should rank first. Fault frame:
/// <see cref="Validate"/>.
/// </summary>
public static class ExceptionMessageReference
{
    public static void Run()
    {
        string? sessionToken = null; // present but unrelated to the thrown error
        Validate(null, sessionToken);
    }

    public static void Validate(string? orderId, string? sessionToken)
    {
        if (orderId is null)
        {
            throw new ArgumentNullException(nameof(orderId), "Value cannot be null. (Parameter 'orderId')");
        }
    }
}
