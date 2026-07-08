using RedBolt;
using RedBolt.Bindings.DearImGui;
using System.Buffers;
using System.Buffers.Text;

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
    private readonly byte[] heapSizeLine = new byte[64];
    private readonly byte[] gen0CollectionsLine = new byte[64];
    private readonly byte[] gen1CollectionsLine = new byte[64];
    private readonly byte[] gen2CollectionsLine = new byte[64];
    private readonly byte[] totalAllocatedLine = new byte[64];
    private long cachedHeapSizeBytes = -1;
    private int cachedGen0Collections = -1;
    private int cachedGen1Collections = -1;
    private int cachedGen2Collections = -1;
    private long cachedTotalAllocatedBytes = -1;
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
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawGCStats());

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

    private void DrawGCStats()
    {
        ImGui.SetNextWindowPos(new Vector2(8, 8), ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(280, 160), ImGuiCond.ImGuiCondFirstUseEver);

        var flags = ImGuiWindowFlags.ImGuiWindowFlagsNoResize
                  | ImGuiWindowFlags.ImGuiWindowFlagsNoMove
                  | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings;

        if (ImGui.Begin("GC Stats\0"u8, out _, flags))
        {
            UpdateMegabyteLine(this.heapSizeLine, "Heap Size: "u8, ref this.cachedHeapSizeBytes, GC.GetTotalMemory(false));
            UpdateIntegerLine(this.gen0CollectionsLine, "Gen 0 Collections: "u8, ref this.cachedGen0Collections, GC.CollectionCount(0));
            UpdateIntegerLine(this.gen1CollectionsLine, "Gen 1 Collections: "u8, ref this.cachedGen1Collections, GC.CollectionCount(1));
            UpdateIntegerLine(this.gen2CollectionsLine, "Gen 2 Collections: "u8, ref this.cachedGen2Collections, GC.CollectionCount(2));
            UpdateMegabyteLine(this.totalAllocatedLine, "Total Allocated: "u8, ref this.cachedTotalAllocatedBytes, GC.GetTotalAllocatedBytes());

            DrawStatLine(this.heapSizeLine);
            DrawStatLine(this.gen0CollectionsLine);
            DrawStatLine(this.gen1CollectionsLine);
            DrawStatLine(this.gen2CollectionsLine);
            DrawStatLine(this.totalAllocatedLine);
        }

        ImGui.End();
    }

    private static void DrawStatLine(byte[] value)
    {
        ImGui.TextUnformatted(value);
    }

    private static void UpdateMegabyteLine(byte[] buffer, ReadOnlySpan<byte> label, ref long cachedValue, long valueBytes)
    {
        if (cachedValue == valueBytes)
            return;

        cachedValue = valueBytes;
        var target = buffer.AsSpan();
        label.CopyTo(target);
        var offset = label.Length;
        var valueMegabytes = valueBytes / (1024.0 * 1024.0);
        if (!Utf8Formatter.TryFormat(valueMegabytes, target[offset..], out var written, new StandardFormat('F', 1)))
            throw new InvalidOperationException("Failed to format GC stats megabyte value.");

        offset += written;
        " MB\0"u8.CopyTo(target[offset..]);
    }

    private static void UpdateIntegerLine(byte[] buffer, ReadOnlySpan<byte> label, ref int cachedValue, int value)
    {
        if (cachedValue == value)
            return;

        cachedValue = value;
        var target = buffer.AsSpan();
        label.CopyTo(target);
        var offset = label.Length;
        if (!Utf8Formatter.TryFormat(value, target[offset..], out var written))
            throw new InvalidOperationException("Failed to format GC stats integer value.");

        offset += written;
        "\0"u8.CopyTo(target[offset..]);
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
