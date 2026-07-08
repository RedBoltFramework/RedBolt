using RedBolt;
using RedBolt.Bindings.DearImGui;

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
    private ContextEventSubscription? dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.StaticScene";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Static Scene";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.SystemUIFlags));
        dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(static (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawGCStats());

        var scene = new Scene();
        scene.CreateComponent<RenderPipeline>();
        scene.CreateComponent<Octree>();
        var outlineGroup = scene.CreateComponent<OutlineGroup>();
        outlineGroup.SetColor(new Color(1.0f, 0.1f, 0.9f, 1.0f));

        var planeNode = scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3(100.0f, 1.0f, 100.0f));
        var plane = planeNode.CreateComponent<StaticModel>();
        plane.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        plane.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var lightNode = scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        var outlinedMushrooms = 0;
        for (var i = 0; i < 200; ++i)
        {
            var mushroomNode = scene.CreateChild("Mushroom");
            var position = new Vector3(RandomRange(-45.0f, 45.0f), 0.0f, RandomRange(-45.0f, 45.0f));
            mushroomNode.SetPosition(position);
            mushroomNode.SetRotation(Quaternion.FromEulerAngles(0.0f, RandomRange(0.0f, 360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + RandomRange(0.0f, 2.0f));
            var mushroom = mushroomNode.CreateComponent<StaticModel>();
            mushroom.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
            mushroom.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));

            if (outlinedMushrooms < 18 && position.Z > 4.0f && position.Z < 28.0f && MathF.Abs(position.X) < 18.0f)
            {
                outlineGroup.AddDrawable(mushroom);
                ++outlinedMushrooms;
            }
        }

        foreach (var x in new[] { -5.5f, 0.0f, 5.5f })
        {
            var outlinedNode = scene.CreateChild("OutlinedMushroom");
            outlinedNode.SetPosition(new Vector3(x, 0.0f, 9.0f));
            outlinedNode.SetScale(3.75f);
            var outlinedMushroom = outlinedNode.CreateComponent<StaticModel>();
            outlinedMushroom.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
            outlinedMushroom.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));
            outlineGroup.AddDrawable(outlinedMushroom);
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

    protected override void Stop()
    {
        dearImGuiSubscription?.Dispose();
        dearImGuiSubscription = null;
    }

    private static void DrawGCStats()
    {
        ImGui.SetNextWindowPos(new Vector2(8, 8), ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280, 160), ImGuiCond.ImGuiCondFirstUseEver);

        var flags = ImGuiWindowFlags.ImGuiWindowFlagsNoResize
                  | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                  | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings;

        if (ImGui.Begin("GC Stats\0"u8, out _, flags))
        {
            var heapSizeMb = (double)GC.GetTotalMemory(false) / (1024 * 1024);
            DrawStatLine("Heap Size", $"{heapSizeMb:N1} MB");
            DrawStatLine("Gen 0 Collections", GC.CollectionCount(0).ToString());
            DrawStatLine("Gen 1 Collections", GC.CollectionCount(1).ToString());
            DrawStatLine("Gen 2 Collections", GC.CollectionCount(2).ToString());
            DrawStatLine("Total Allocated", $"{(double)GC.GetTotalAllocatedBytes() / (1024 * 1024):N1} MB");
        }

        ImGui.End();
    }

    private static void DrawStatLine(string label, string value)
    {
        ImGui.TextUnformatted(System.Text.Encoding.UTF8.GetBytes($"{label}: {value}\0"));
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

        throw new InvalidOperationException("StaticScene sample could not locate rbfx CoreData/Data resource directories.");
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

    private float RandomRange(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
