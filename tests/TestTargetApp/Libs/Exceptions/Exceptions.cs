namespace Exceptions;

public static class ExceptionsUtil
{
    public static string GetName() => $"Exceptions(dep:{BaseTypes.BaseTypesUtil.GetName()})";
}

public class CustomTestException : Exception
{
    public int ErrorCode { get; }

    public CustomTestException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public static class ExceptionThrower
{
    public static void ThrowInvalidOp()
    {
        throw new InvalidOperationException("Test invalid operation");
    }

    public static void ThrowArgumentNull()
    {
        throw new ArgumentNullException("testParam", "Test argument null");
    }

    public static void ThrowCustom()
    {
        throw new CustomTestException("Custom test error", 42);
    }

    public static string NestedTryCatch()
    {
        try
        {
            try
            {
                throw new InvalidOperationException("Inner exception");
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException("Wrapped exception");
            }
        }
        catch (ArgumentException ex)
        {
            return $"Caught: {ex.Message}";
        }
    }
}
