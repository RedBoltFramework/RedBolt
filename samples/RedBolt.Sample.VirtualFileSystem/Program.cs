using System;
using System.Collections.Generic;
using System.Text;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new VirtualFileSystemApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Virtual file system example.</summary>
sealed class VirtualFileSystemApplication : Application
{
    private readonly List<byte[]> _lines = [];
    private ContextEventSubscription? _dearImGuiSubscription;
    protected override void Setup(EngineParameters parameters) { parameters.ApplicationName = "RedBolt.Sample.VirtualFileSystem"; parameters.Borderless = false; parameters.FullScreen = false; parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;.."; parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Virtual File System"; }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => RenderUi());
        SetupViewport();
        CreateText(); QueryAndScan(); Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }
    private void SetupViewport()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(Color.Black);
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        Renderer.SetViewport(0, new Viewport(scene, camera));
    }
    private void CreateText()
    {
        var vfs = Context.GetSubsystem<VirtualFileSystem>()!;
        Add("MountPoints:");
        for (var i = 0u; i < vfs.NumMountPoints(); ++i) Add("  " + vfs.GetMountPoint(i)!.GetName());
    }
    private void QueryAndScan()
    {
        var vfs = Context.GetSubsystem<VirtualFileSystem>()!;
        var fileIdentifier = FileIdentifier.FromUri("Models/Box.mdl");
        var canonicalForm = vfs.GetCanonicalIdentifier(fileIdentifier);
        var exists = vfs.Exists(fileIdentifier);
        var absoluteFileName = vfs.GetAbsoluteNameFromIdentifier(fileIdentifier);
        var readOnlyFile = vfs.OpenFile(fileIdentifier, RedBolt.FileMode.Read);
        var modificationTime = vfs.GetLastModifiedTime(fileIdentifier, true);
        var reversedUri = vfs.GetIdentifierFromAbsoluteName(absoluteFileName).ToUri();
        Add("URI: Models/Box.mdl"); Add("scheme: " + fileIdentifier.Scheme); Add("path: " + fileIdentifier.FileName);
        Add("canonical: " + canonicalForm.ToUri()); Add("exists: " + (exists ? "yes" : "no"));
        Add("absolute path: " + (absoluteFileName.Length == 0 ? "[not found]" : absoluteFileName));
        Add("file size: " + (readOnlyFile is null ? "[not found]" : readOnlyFile.Size.ToString()));
        Add("modification time: " + (modificationTime == 0 ? "[unknown]" : modificationTime.ToString()));
        Add("reversed URI: " + (reversedUri.Length == 0 ? "[not found]" : reversedUri));
        var scanPath = new FileIdentifier(string.Empty, "Materials");
        var scanResults = new List<string>();
        vfs.Scan(scanResults, scanPath, "*.*", ScanFlags.ScanRecursive | ScanFlags.ScanFiles);
        Add("scan results:");
        foreach (var result in scanResults) Add("  " + (scanPath + result).FileName);
    }
    private void Add(string value) => _lines.Add(Encoding.UTF8.GetBytes(value + "\0"));
    private void RenderUi() { if (ImGui.Begin("VFS Query Interface\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) foreach (var line in _lines) ImGui.TextUnformatted(line); ImGui.End(); }
    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); }
    protected override void Stop() { _dearImGuiSubscription?.Dispose(); }
}
