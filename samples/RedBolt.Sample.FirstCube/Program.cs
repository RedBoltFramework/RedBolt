using RedBolt;

try
{
    var exitCode = new FirstCubeApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt first cube presented through generated SDL/Diligent bindings.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class FirstCubeApplication : Application
{
    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.FirstCube";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt First Cube";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        AddResourceDirs();

        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.Position = new Vector3(0.0f, 2.0f, -6.0f);
        var camera = cameraNode.CreateComponent<Camera>();

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.Rotation = Quaternion.Identity;
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        cube.SetMaterial(Resources.Get<Material>("Materials/FirstCube"));

        Renderer.SetViewport(0, new Viewport(scene, camera));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        Thread.Sleep(16);
    }

    private void AddResourceDirs()
    {
        var resourceRoots = FindResourceRoots();
        foreach (var root in resourceRoots)
            Resources.AddResourceDir(root);

        if (resourceRoots.Count == 0)
            throw new InvalidOperationException("FirstCube sample could not locate rbfx CoreData/Data resource directories.");

        var localDataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Resources.AddResourceDir(localDataDir) && !Directory.Exists(localDataDir))
            throw new InvalidOperationException("FirstCube sample data directory was not available for ResourceCache.");
    }

    private static List<string> FindResourceRoots()
    {
        var result = new List<string>();

        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(envRoot))
            AddCandidateRoots(result, envRoot);

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            AddCandidateRoots(result, Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"));
            AddCandidateRoots(result, Path.Combine(current.FullName, "rbfx"));
            AddCandidateRoots(result, Path.Combine(current.FullName, "bin"));
            AddCandidateRoots(result, Path.Combine(current.FullName, "..", "rbfx", "bin"));

            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }

        return result;
    }

    private static void AddCandidateRoots(List<string> result, string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var coreData = Path.Combine(normalizedRoot, "CoreData");
        var data = Path.Combine(normalizedRoot, "Data");
        if (!Directory.Exists(coreData) || !Directory.Exists(data))
            return;

        if (!result.Contains(coreData, StringComparer.Ordinal))
            result.Add(coreData);
        if (!result.Contains(data, StringComparer.Ordinal))
            result.Add(data);
    }
}
