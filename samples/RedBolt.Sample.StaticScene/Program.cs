using RedBolt;

try
{
    var exitCode = new StaticSceneApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt StaticScene sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class StaticSceneApplication : Application
{
    private readonly Random random = new(1);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.StaticScene";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Static Scene";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        AddResourceDirs();

        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var planeNode = scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3(100.0f, 1.0f, 100.0f));
        var plane = planeNode.CreateComponent<StaticModel>();
        plane.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        plane.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var lightNode = scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        for (var i = 0; i < 200; ++i)
        {
            var mushroomNode = scene.CreateChild("Mushroom");
            mushroomNode.SetPosition(new Vector3(RandomRange(-45.0f, 45.0f), 0.0f, RandomRange(-45.0f, 45.0f)));
            mushroomNode.SetRotation(new Quaternion(0.0f, RandomRange(0.0f, 360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + RandomRange(0.0f, 2.0f));
            var mushroom = mushroomNode.CreateComponent<StaticModel>();
            mushroom.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
            mushroom.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));
        }

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3(0.0f, 5.0f, 0.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        var controller = cameraNode.CreateComponent<FreeFlyController>();
        controller.SetInput(Input);

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
            throw new InvalidOperationException("StaticScene sample could not locate rbfx CoreData/Data resource directories.");
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

    private float RandomRange(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
