using RedBolt;

namespace RedBolt.Tests;

internal static class RbfxTestData
{
    public static string Root => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(typeof(RbfxTestData).Assembly.Location)!, ".."));

    public static string CoreData => Path.Combine(Root, "CoreData");

    public static string Data => Path.Combine(Root, "Data");

    public static void AddResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        resources.AddResourceDir(CoreData);
        resources.AddResourceDir(Data);
    }

    public static void RemoveResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        resources.RemoveResourceDir(Data);
        resources.RemoveResourceDir(CoreData);
    }
}
