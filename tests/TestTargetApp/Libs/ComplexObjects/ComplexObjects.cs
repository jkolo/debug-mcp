namespace ComplexObjects;

public static class ComplexObjectsUtil
{
    public static string GetName() => $"ComplexObjects(dep:{Expressions.ExpressionsUtil.GetName()},{AsyncOps.AsyncOpsUtil.GetName()},{MemoryStructs.MemoryStructsUtil.GetName()})";
}

public class DeepObject
{
    public int Level { get; set; }
    public DeepObject? Child { get; set; }
    public string Data { get; set; } = "";

    public static DeepObject CreateChain(int depth)
    {
        if (depth <= 0)
        {
            return new DeepObject { Level = 0, Data = "Leaf", Child = null };
        }

        return new DeepObject
        {
            Level = depth,
            Data = $"Level-{depth}",
            Child = CreateChain(depth - 1)
        };
    }
}

public class ComplexContainer
{
    public string Name { get; set; } = "";
    public List<DeepObject> Items { get; set; } = new();
    public Dictionary<string, DeepObject> Lookup { get; set; } = new();

    public static ComplexContainer Create()
    {
        var container = new ComplexContainer
        {
            Name = "TestContainer",
            Items = new List<DeepObject>
            {
                DeepObject.CreateChain(2),
                DeepObject.CreateChain(1)
            }
        };
        container.Lookup["first"] = container.Items[0];
        container.Lookup["second"] = container.Items[1];
        return container;
    }
}
