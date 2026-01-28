namespace Collections;

public static class CollectionsUtil
{
    public static string GetName() => $"Collections(dep:{BaseTypes.BaseTypesUtil.GetName()})";
}

public class CollectionHolder
{
    public List<string> StringList { get; set; } = new();
    public Dictionary<string, int> IntMap { get; set; } = new();
    public int[] Numbers { get; set; } = Array.Empty<int>();

    public static CollectionHolder Create()
    {
        return new CollectionHolder
        {
            StringList = new List<string> { "alpha", "beta", "gamma" },
            IntMap = new Dictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2,
                ["three"] = 3
            },
            Numbers = new[] { 10, 20, 30, 40, 50 }
        };
    }
}
