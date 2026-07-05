namespace RedBolt.Tests;

internal static class FirstCubeTestData
{
    public static void AddResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var repoRoot = FindRepoRoot();
        var sampleData = Path.Combine(repoRoot, "samples", "RedBolt.Sample.FirstCube", "Data");
        if (!Directory.Exists(sampleData))
            throw new DirectoryNotFoundException($"FirstCube sample data directory was not found: {sampleData}");

        resources.AddResourceDir(sampleData);
        RbfxTestData.AddResourceDirs(resources);
    }

    private static string FindRepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the RedBolt repository root.");
    }
}
