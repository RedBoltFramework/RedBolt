using System;
using System.IO;
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
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt First Cube";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.Position = new Vector3f(0.0f, 2.0f, -6.0f);
        var camera = cameraNode.CreateComponent<Camera>();

        var lightNode = scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.4f, -1.0f, 0.6f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.Rotation = Quaternion.Identity;
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        cube.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        Renderer.SetViewport(0, new Viewport(scene, camera));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }
    }

    private static string FindResourcePrefixPath()
    {
        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (TryNormalizeResourcePrefixPath(envRoot, out var normalizedEnvRoot))
            return normalizedEnvRoot;

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "bin"), out var localBinRoot))
                return localBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), out var testDataRoot))
                return testDataRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "rbfx", "bin"), out var repoRbfxBinRoot))
                return repoRbfxBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "..", "rbfx", "bin"), out var siblingRbfxBinRoot))
                return siblingRbfxBinRoot;

            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }

        throw new InvalidOperationException("FirstCube sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) ||
            !Directory.Exists(Path.Combine(candidateRoot, "Data")))
        {
            return false;
        }

        normalizedRoot = candidateRoot;
        return true;
    }
}
