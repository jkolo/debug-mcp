namespace Scenarios;

public static class ScenariosUtil
{
    public static string GetName() => $"Scenarios(dep:{ComplexObjects.ComplexObjectsUtil.GetName()})";
}
