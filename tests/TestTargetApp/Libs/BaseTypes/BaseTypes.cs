namespace BaseTypes;

public static class BaseTypesUtil
{
    public static string GetName() => "BaseTypes";
}

public enum TestEnum
{
    Red = 1,
    Green = 2,
    Blue = 3
}

public struct TestStruct
{
    public int X;
    public int Y;
    public string Name;

    public TestStruct(int x, int y, string name)
    {
        X = x;
        Y = y;
        Name = name;
    }
}

public class NullableHolder
{
    public int? NullableInt { get; set; }
    public string? NullableString { get; set; }

    public static NullableHolder CreateWithValues(int intVal, string strVal)
    {
        return new NullableHolder { NullableInt = intVal, NullableString = strVal };
    }

    public static NullableHolder CreateWithNulls()
    {
        return new NullableHolder { NullableInt = null, NullableString = null };
    }
}
