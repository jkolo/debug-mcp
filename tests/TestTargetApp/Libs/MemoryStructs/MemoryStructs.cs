namespace MemoryStructs;

public static class MemoryStructsUtil
{
    public static string GetName() => "MemoryStructs";
}

public struct LayoutStruct
{
    public int Id;        // 4 bytes
    public double Value;  // 8 bytes
    public bool Flag;     // 1 byte (+ padding)

    public LayoutStruct(int id, double value, bool flag)
    {
        Id = id;
        Value = value;
        Flag = flag;
    }

    public static LayoutStruct Create()
    {
        return new LayoutStruct(123, 3.14159, true);
    }
}

public struct PackedPoint
{
    public short X;  // 2 bytes
    public short Y;  // 2 bytes

    public PackedPoint(short x, short y)
    {
        X = x;
        Y = y;
    }
}
