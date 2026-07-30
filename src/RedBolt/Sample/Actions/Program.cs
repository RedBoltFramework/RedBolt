namespace RedBolt.Sample.Actions;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Actions;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.Scene;

internal static class ActionsProgram
{
    private static int Main()
    {
        try
        {
            return new ActionsApplication().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

sealed class ActionsApplication : Application
{
    private sealed class DemoElement : IDisposable
    {
        public DemoElement(string name, Node node, RedObject target, FiniteTimeAction action, Material material,
            in Vector3f position, in Vector4f color)
        {
            Name = name;
            Node = node;
            Target = target;
            Action = action;
            Material = material;
            Position = position;
            Color = color;
            Label = new CString(name);
        }

        public void Dispose()
        {
            Label.Dispose();
            Action.Dispose();
            Material.Dispose();
        }

        public string Name { get; }
        public Node Node { get; }
        public RedObject Target { get; }
        public FiniteTimeAction Action { get; }
        public Material Material { get; }
        public Vector3f Position { get; }
        public Vector4f Color { get; }
        public CString Label { get; }
    }

    private readonly List<DemoElement> _markers = [];
    private readonly CString _status = new("All action tracks are running");
    private readonly CString _running = new();
    private Scene? _scene;
    private Node? _cameraNode;
    private Viewport? _viewport;
    private ActionManager? _actionManager;
    private int _callbackCount;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Actions";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
        }
        parameters.WindowTitle = "RedBolt Actions";
    }

    protected override void Start()
    {
        _actionManager = new ActionManager();
        Context.RegisterSubsystem(_actionManager);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawControls());

        CreateScene();
        CreateActions();
        SetupViewport();
        RunAll();

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-100.0f, 100.0f));
        zone.SetAmbientColor(new Color(0.025f, 0.035f, 0.08f));
        zone.SetFogColor(new Color(0.005f, 0.01f, 0.035f));
        zone.SetFogStart(30.0f);
        zone.SetFogEnd(55.0f);

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetScale(new Vector3f(38.0f, 1.0f, 38.0f));
        var floor = floorNode.CreateComponent<StaticModel>();
        floor.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        floor.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var directionalLightNode = _scene.CreateChild("DirectionalLight");
        directionalLightNode.SetDirection(new Vector3f(0.4f, -1.0f, 0.25f));
        var directionalLight = directionalLightNode.CreateComponent<Light>();
        directionalLight.SetLightType(LightType.Directional);
        directionalLight.SetBrightness(0.8f);
        directionalLight.SetColor(new Color(0.35f, 0.42f, 0.65f));

        CreatePointLight(new Vector3f(-9.0f, 6.0f, -6.0f), new Color(0.1f, 0.45f, 1.0f));
        CreatePointLight(new Vector3f(9.0f, 6.0f, -2.0f), new Color(1.0f, 0.15f, 0.45f));
        CreatePointLight(new Vector3f(0.0f, 7.0f, 9.0f), new Color(0.3f, 1.0f, 0.55f));

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>().SetFarClip(100.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 19.0f, -28.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 1.8f, 1.5f));
    }

    private void CreatePointLight(in Vector3f position, in Color color)
    {
        var node = _scene!.CreateChild("PointLight");
        node.SetPosition(position);
        var light = node.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(22.0f);
        light.SetBrightness(2.3f);
        light.SetColor(color);
    }

    private void CreateActions()
    {
        var offset = new Vector3f(0.0f, 3.2f, 0.0f);

        AddElement("Linear", new ActionBuilder().MoveBy(2.8f, offset).DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Back In", new ActionBuilder().MoveBy(2.8f, offset).BackIn().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Back Out", new ActionBuilder().MoveBy(2.8f, offset).BackOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Back In/Out", new ActionBuilder().MoveBy(2.8f, offset).BackInOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Bounce Out", new ActionBuilder().MoveBy(2.8f, offset).BounceOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Bounce In", new ActionBuilder().MoveBy(2.8f, offset).BounceIn().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Bounce In/Out", new ActionBuilder().MoveBy(2.8f, offset).BounceInOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Sine Out", new ActionBuilder().MoveBy(2.8f, offset).SineOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Sine In", new ActionBuilder().MoveBy(2.8f, offset).SineIn().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Sine In/Out", new ActionBuilder().MoveBy(2.8f, offset).SineInOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Exponential Out", new ActionBuilder().MoveBy(2.8f, offset).ExponentialOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Exponential In", new ActionBuilder().MoveBy(2.8f, offset).ExponentialIn().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Exponential In/Out", new ActionBuilder().MoveBy(2.8f, offset).ExponentialInOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Elastic In", new ActionBuilder().MoveBy(2.8f, offset).ElasticIn().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Elastic Out", new ActionBuilder().MoveBy(2.8f, offset).ElasticOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Elastic In/Out", new ActionBuilder().MoveBy(2.8f, offset).ElasticInOut().DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Quadratic", new ActionBuilder()
            .MoveByQuadratic(3.0f, offset * 0.5f + new Vector3f(1.5f, 0.0f, 0.0f), offset)
            .DelayTime(0.4f).JumpBy(-offset).RepeatForever().Build()!);

        var spin = new ActionBuilder().RotateBy(2.0f, new Quaternion(0.0f, 180.0f, 0.0f)).Build()!;
        AddElement("Parallel", new ActionBuilder().MoveBy(2.0f, offset).Also(spin).DelayTime(0.5f)
            .JumpBy(-offset).RepeatForever().Build()!);
        AddElement("Scale + Repeat", new ActionBuilder()
            .ScaleBy(1.5f, new Vector3f(1.35f, 1.35f, 1.35f)).SineInOut().Repeat(2).Build()!);

        var rotatePosition = GetNextPosition();
        AddElement("Rotate Around", new ActionBuilder()
            .RotateAround(3.0f, rotatePosition + new Vector3f(0.0f, 0.0f, 1.6f),
                new Quaternion(0.0f, 180.0f, 0.0f))
            .RepeatForever().Build()!, rotatePosition);
        AddElement("Blink", new ActionBuilder().Blink(2.5f, 8).DelayTime(0.4f).RepeatForever().Build()!);
        AddElement("Enable / Disable", new ActionBuilder()
            .Disable().DelayTime(0.35f).Enable().DelayTime(1.2f).RepeatForever().Build()!);

        var attributePosition = GetNextPosition();
        using (var from = new Variant(attributePosition))
        using (var to = new Variant(attributePosition + offset))
        {
            AddElement("Attribute From/To", new ActionBuilder()
                .AttributeFromTo(4.0f, AttributeActionConstants.PositionAttribute, from, to).Build()!,
                attributePosition);
        }

        var shaderPosition = GetNextPosition();
        var shaderColor = GetColor(_markers.Count);
        using (var from = new Variant(shaderColor))
        using (var to = new Variant(new Vector4f(1.0f, 1.0f, 1.0f, 1.0f)))
        {
            AddElement("Shader Parameter", new ActionBuilder()
                .ShaderParameterFromTo(4.0f, "MatDiffColor", from, to).Build()!, shaderPosition, true);
        }

        AddElement("Callback", new ActionBuilder().DelayTime(1.0f)
            .CallFunc(this, static (application, _) => application.HandleCallback()).DelayTime(2.0f)
            .RepeatForever().Build()!);
        AddElement("Remove Self", new ActionBuilder().DelayTime(2.0f).RemoveSelf().Build()!);
    }

    private void AddElement(string name, FiniteTimeAction action)
    {
        AddElement(name, action, GetNextPosition());
    }

    private void AddElement(string name, FiniteTimeAction action, in Vector3f position, bool targetMaterial = false)
    {
        var color = GetColor(_markers.Count);
        var material = Resources.Get<Material>("Materials/Constant/GlossyWhite.xml").Clone();
        material.SetShaderParameter("MatDiffColor", color);
        material.SetShaderParameter("MatEmissiveColor", new Vector3f(color.X * 0.22f, color.Y * 0.22f, color.Z * 0.22f));

        var pedestalNode = _scene!.CreateChild("ActionPedestal");
        pedestalNode.SetPosition(new Vector3f(position.X, 0.18f, position.Z));
        pedestalNode.SetScale(new Vector3f(1.8f, 0.18f, 1.8f));
        var pedestal = pedestalNode.CreateComponent<StaticModel>();
        pedestal.SetModel(Resources.Get<Model>("Models/Cylinder.mdl"));
        pedestal.SetMaterial(Resources.Get<Material>("Materials/Constant/DielectricG3.xml"));

        var node = _scene.CreateChild(name);
        node.SetPosition(position);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Sphere.mdl"));
        model.SetMaterial(material);
        model.SetCastShadows(true);

        _markers.Add(new DemoElement(
            name, node, targetMaterial ? material : node, action, material, position, color));
    }

    private Vector3f GetNextPosition()
    {
        var column = _markers.Count % 5;
        var row = _markers.Count / 5;
        return new Vector3f((column - 2.0f) * 3.6f, 1.0f, (row - 2.5f) * 3.5f);
    }

    private static Vector4f GetColor(int index)
    {
        return (index % 8) switch
        {
            0 => new Vector4f(0.1f, 0.75f, 1.0f, 1.0f),
            1 => new Vector4f(0.95f, 0.15f, 0.55f, 1.0f),
            2 => new Vector4f(0.3f, 1.0f, 0.55f, 1.0f),
            3 => new Vector4f(1.0f, 0.65f, 0.08f, 1.0f),
            4 => new Vector4f(0.65f, 0.25f, 1.0f, 1.0f),
            5 => new Vector4f(1.0f, 0.3f, 0.18f, 1.0f),
            6 => new Vector4f(0.2f, 1.0f, 0.9f, 1.0f),
            _ => new Vector4f(0.75f, 0.9f, 0.2f, 1.0f)
        };
    }

    private void SetupViewport()
    {
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, _viewport);
    }

    private void RunAll()
    {
        _actionManager!.CancelAllActions();
        for (var i = 0; i < _markers.Count; ++i)
        {
            Reset(_markers[i]);
            _actionManager.AddAction(_markers[i].Action, _markers[i].Target);
        }
        _status.FormatInPlace("All action tracks are running");
    }

    private void Run(DemoElement marker)
    {
        _actionManager!.CancelAllActionsFromTarget(marker.Target);
        Reset(marker);
        _actionManager.AddAction(marker.Action, marker.Target);
        _status.FormatInPlace($"Running: {marker.Name}");
    }

    private void Reset(DemoElement marker)
    {
        if (marker.Node.Parent is null)
            _scene!.AddChild(marker.Node);
        marker.Node.SetEnabled(true);
        marker.Node.SetPosition(marker.Position);
        marker.Node.SetRotation(Quaternion.IDENTITY);
        marker.Node.SetScale(Vector3f.One);
        marker.Material.SetShaderParameter("MatDiffColor", marker.Color);
    }

    private void HandleCallback()
    {
        ++_callbackCount;
        _status.FormatInPlace($"Callback fired {_callbackCount} times");
    }

    private void DrawControls()
    {
        var activeActions = 0;
        for (var i = 0; i < _markers.Count; ++i)
            activeActions += _actionManager!.GetNumActions(_markers[i].Target);
        _running.FormatInPlace($"Active action states: {activeActions}");

        ImGui.SetNextWindowPos(new Vector2f(16.0f, 16.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(new Vector2f(385.0f, 680.0f), ImGuiCond.ImGuiCondAlways);
        var windowOpen = true;
        if (ImGui.Begin("Action System Lab\0"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("26 live targets: sequencing, parallelism, easing,\nattributes, shaders, callbacks, repeats, and removal.\0"u8);
            ImGui.Separator();
            if (ImGui.Button("Run all\0"u8))
                RunAll();
            ImGui.SameLine();
            if (ImGui.Button("Cancel all\0"u8))
            {
                _actionManager!.CancelAllActions();
                _status.FormatInPlace("All actions canceled");
            }
            ImGui.SameLine();
            if (ImGui.Button("Complete all\0"u8))
            {
                _actionManager!.CompleteAllActions();
                _status.FormatInPlace("All finite actions completed");
            }

            ImGui.TextUnformatted(_running.Bytes);
            ImGui.TextUnformatted(_status.Bytes);
            ImGui.Separator();
            ImGui.TextUnformatted("Click a track to reset and run it alone:\0"u8);

            for (var i = 0; i < _markers.Count; ++i)
            {
                if (ImGui.Button(_markers[i].Label.Bytes))
                    Run(_markers[i]);
                if (i % 2 == 0)
                    ImGui.SameLine();
            }
        }
        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
        DisposeScene();
        base.Stop();
    }

    public override void Dispose()
    {
        DisposeScene();
        _running.Dispose();
        _status.Dispose();
        base.Dispose();
    }

    private void DisposeScene()
    {
        if (_scene is null)
            return;

        _actionManager?.CancelAllActions();
        for (var i = 0; i < _markers.Count; ++i)
            Reset(_markers[i]);

        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene.Dispose();
        _scene = null;

        for (var i = _markers.Count - 1; i >= 0; --i)
            _markers[i].Dispose();
        _markers.Clear();
    }
}
