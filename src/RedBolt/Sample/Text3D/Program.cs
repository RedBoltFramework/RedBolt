//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Sample.Text3D;

using System;

using RedBolt;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
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
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt Text3D";
    }

    protected override void Start()
    {
        base.Start();

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.25f, 0.25f, 0.3f));
        zone.SetFogColor(new Color(0.08f, 0.12f, 0.2f));
        zone.SetFogStart(35.0f);
        zone.SetFogEnd(60.0f);

        var skyNode = _scene.CreateChild("Sky");
        var sky = skyNode.CreateComponent<Skybox>();
        sky.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        sky.SetMaterial(Resources.Get<Material>("Materials/Skybox.xml"));

        var groundNode = _scene.CreateChild("Ground");
        groundNode.SetScale(new Vector3f(40.0f, 1.0f, 40.0f));
        var ground = groundNode.CreateComponent<StaticModel>();
        ground.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        ground.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.7f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);

        var font = Resources.Get<Font>("Fonts/Anonymous Pro.ttf");
        var sdfFont = Resources.Get<Font>("Fonts/BlueHighway.sdf");

        var titleNode = _scene.CreateChild("Title");
        titleNode.SetPosition(new Vector3f(0.0f, 3.0f, 2.0f));
        titleNode.SetScale(1.5f);
        var title = titleNode.CreateComponent<Text3DComponent>();
        title.SetFont(font, 64.0f);
        title.SetText("RedBolt Text3D");
        title.SetColor(new Color(1.0f, 0.75f, 0.2f));
        title.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var bodyNode = _scene.CreateChild("Body");
        bodyNode.SetPosition(new Vector3f(-3.2f, 1.9f, 6.0f));
        bodyNode.SetRotation(new Quaternion(0.0f, 20.0f, 0.0f));
        bodyNode.SetScale(1.7f);
        var body = bodyNode.CreateComponent<Text3DComponent>();
        body.SetFont(font, 36.0f);
        body.SetText("HarfBuzz GPU\nNo UI subsystem");
        body.SetTextAlignment(HorizontalAlignment.Center);
        body.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var sdfNode = _scene.CreateChild("SDF");
        sdfNode.SetPosition(new Vector3f(3.2f, 1.5f, 7.5f));
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
        leftPedestal.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
        leftPedestal.SetCastShadows(true);

        var rightPedestalNode = _scene.CreateChild("RightPedestal");
        rightPedestalNode.SetPosition(new Vector3f(3.2f, 0.6f, 7.8f));
        rightPedestalNode.SetRotation(new Quaternion(0.0f, -25.0f, 0.0f));
        rightPedestalNode.SetScale(new Vector3f(4.5f, 1.2f, 0.7f));
        var rightPedestal = rightPedestalNode.CreateComponent<StaticModel>();
        rightPedestal.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        rightPedestal.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
        rightPedestal.SetCastShadows(true);

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

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
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
}
