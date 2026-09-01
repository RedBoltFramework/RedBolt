// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Text3D;

using System;

using RedBolt;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using Text3DComponent = global::RedBolt.Graphics.Text3D;

internal static class Text3DProgram
{
    private static int Main()
    {
        try
        {
            using var application = new Text3DApplication();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public sealed class Text3DApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private Viewport? _viewport;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Text3D";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Text3D";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        base.Start();

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.3f, 0.34f, 0.42f));
        zone.SetFogColor(new Color(0.045f, 0.06f, 0.1f));
        zone.SetFogStart(35.0f);
        zone.SetFogEnd(60.0f);

        var skyNode = _scene.CreateChild("Sky");
        var sky = skyNode.CreateComponent<Skybox>();
        sky.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        sky.SetMaterial(Resources.Get<Material>("Materials/DefaultSkybox.xml"));

        var groundNode = _scene.CreateChild("Ground");
        groundNode.SetPosition(new Vector3f(0.0f, -0.12f, 4.0f));
        groundNode.SetScale(new Vector3f(40.0f, 0.2f, 40.0f));
        var ground = groundNode.CreateComponent<StaticModel>();
        ground.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        ground.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml"));

        CreateTechnicalBox("BackGrid", new Vector3f(0.0f, 3.5f, 12.0f),
            new Vector3f(18.0f, 7.0f, 0.2f), "Materials/TechnicalDemo/BackWallGrid.xml");
        CreateTechnicalBox("LeftGrid", new Vector3f(-8.5f, 2.5f, 5.0f),
            new Vector3f(0.2f, 5.0f, 14.0f), "Materials/TechnicalDemo/SideWallGrid.xml");
        CreateTechnicalBox("RightGrid", new Vector3f(8.5f, 2.5f, 5.0f),
            new Vector3f(0.2f, 5.0f, 14.0f), "Materials/TechnicalDemo/SideWallGrid.xml");

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.7f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.92f, 0.96f, 1.0f));
        light.SetCastShadows(true);

        var font = Resources.Get<Font>("Fonts/Anonymous Pro.ttf");
        var shapedFont = Resources.Get<Font>("Fonts/NotoSans-CondensedItalic.ttf");
        var sdfFont = Resources.Get<Font>("Fonts/BlueHighway.sdf");

        var titleNode = _scene.CreateChild("Title");
        titleNode.SetPosition(new Vector3f(0.0f, 3.0f, 2.0f));
        titleNode.SetScale(1.5f);
        var title = titleNode.CreateComponent<Text3DComponent>();
        title.SetFont(font, 64.0f);
        title.SetText("TEXT / WORLD SPACE");
        title.SetColor(new Color(1.0f, 0.75f, 0.2f));
        title.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var bodyNode = _scene.CreateChild("Body");
        bodyNode.SetPosition(new Vector3f(-3.2f, 1.9f, 6.0f));
        bodyNode.SetRotation(new Quaternion(0.0f, 20.0f, 0.0f));
        bodyNode.SetScale(1.7f);
        var body = bodyNode.CreateComponent<Text3DComponent>();
        body.SetFont(shapedFont, 36.0f);
        body.SetText("office fi fl · Cafe\u0301\nNo UI subsystem");
        body.SetTextAlignment(HorizontalAlignment.Center);
        body.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var sdfNode = _scene.CreateChild("SDF");
        sdfNode.SetPosition(new Vector3f(3.2f, 1.8f, 7.5f));
        sdfNode.SetRotation(new Quaternion(0.0f, -25.0f, 0.0f));
        sdfNode.SetScale(1.6f);
        var sdf = sdfNode.CreateComponent<Text3DComponent>();
        sdf.SetFont(sdfFont, 36.0f);
        sdf.SetText("UBFG pre-baked .sdf");
        sdf.SetColor(new Color(0.4f, 0.8f, 1.0f));
        sdf.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var leftPedestalNode = _scene.CreateChild("LeftPedestal");
        leftPedestalNode.SetPosition(new Vector3f(-3.2f, 0.6f, 6.3f));
        leftPedestalNode.SetRotation(new Quaternion(0.0f, 20.0f, 0.0f));
        leftPedestalNode.SetScale(new Vector3f(4.5f, 1.2f, 0.7f));
        var leftPedestal = leftPedestalNode.CreateComponent<StaticModel>();
        leftPedestal.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        leftPedestal.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/Dark.xml"));
        leftPedestal.SetCastShadows(true);

        var rightPedestalNode = _scene.CreateChild("RightPedestal");
        rightPedestalNode.SetPosition(new Vector3f(3.2f, 0.6f, 7.8f));
        rightPedestalNode.SetRotation(new Quaternion(0.0f, -25.0f, 0.0f));
        rightPedestalNode.SetScale(new Vector3f(4.5f, 1.2f, 0.7f));
        var rightPedestal = rightPedestalNode.CreateComponent<StaticModel>();
        rightPedestal.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        rightPedestal.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/Dark.xml"));
        rightPedestal.SetCastShadows(true);

        CreateTechnicalBox("LeftAccent", new Vector3f(-3.2f, 1.28f, 6.3f),
            new Vector3f(4.7f, 0.12f, 0.9f), "Materials/TechnicalDemo/Accent.xml",
            new Quaternion(0.0f, 20.0f, 0.0f));
        CreateTechnicalBox("RightAccent", new Vector3f(3.2f, 1.28f, 7.8f),
            new Vector3f(4.7f, 0.12f, 0.9f), "Materials/TechnicalDemo/Accent.xml",
            new Quaternion(0.0f, -25.0f, 0.0f));

        for (var i = -1; i <= 1; i += 2)
        {
            CreateTechnicalBox($"Marker{i}", new Vector3f(i * 7.2f, 1.2f, 1.0f),
                new Vector3f(0.22f, 2.4f, 0.22f), "Materials/TechnicalDemo/Accent.xml");
        }

        var controlsNode = _scene.CreateChild("Controls");
        controlsNode.SetPosition(new Vector3f(0.0f, 0.25f, -4.0f));
        var controls = controlsNode.CreateComponent<Text3DComponent>();
        controls.SetFont(font, 18.0f);
        controls.SetText("Hold RMB + WASD / Q E to fly");
        controls.SetColor(new Color(0.9f, 0.95f, 1.0f));
        controls.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(100.0f);
        var controller = _cameraNode.CreateComponent<FreeFlyController>();
        controller.SetSpeed(6.0f);
        controller.SetAcceleratedSpeed(18.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 2.5f, -10.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 1.5f, 5.0f));

        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateTechnicalBox(string name, Vector3f position, Vector3f scale,
        string material, Quaternion? rotation = null)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetScale(scale);
        if (rotation is not null)
            node.SetRotation(rotation.Value);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(Resources.Get<Material>(material));
        model.SetCastShadows(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }
    private SampleInputMap? _sampleInput;
}
