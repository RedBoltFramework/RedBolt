// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.HelloWorld;

using System;

using RedBolt;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
internal static class HelloWorldProgram
{
    private static int Main()
    {
        try
        {
            using var application = new HelloWorld();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt HelloWorld sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>Displays a centered 3D "Hello, World!" message.</summary>
public class HelloWorld : Application
{
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        base.Start();

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var text = _scene.CreateChild("Text").CreateComponent<Text3D>();
        text.SetFont(Resources.Get<Font>("Fonts/Anonymous Pro.ttf"), 64.0f);
        text.SetText("Hello, World!");
        text.SetColor(new Color(0.35f, 0.85f, 1.0f));
        text.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);

        var cameraNode = _scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -5.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.SetOrthoSize(3.0f);

        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloWorld";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Hello World";
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
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
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Viewport? _viewport;
    private SampleInputMap? _sampleInput;
}
