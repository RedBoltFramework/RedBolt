// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DParticle;

using System;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DParticleProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DParticle(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates PEX particle effects in a 2D scene.</summary>
public sealed class Urho2DParticle : Application
{
    private Node? _particleNode;
    private Camera? _camera;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DParticle";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Particle";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        _camera = cameraNode.CreateComponent<Camera>();
        _camera.SetOrthographic(true);
        _camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        _camera.SetZoom(1.2f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        if (Resources.Get<ParticleEffect2D>("Urho2D/sun.pex") is { } sun)
        {
            _particleNode = scene.CreateChild("ParticleEmitter2D");
            _particleNode.CreateComponent<ParticleEmitter2D>().Effect = sun;
        }
        if (Resources.Get<ParticleEffect2D>("Urho2D/greenspiral.pex") is { } spiral)
            scene.CreateChild("GreenSpiral").CreateComponent<ParticleEmitter2D>().Effect = spiral;

        Renderer.SetViewport(0, new Viewport(scene, _camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        if (_particleNode is null || _camera is null || Graphics.Width == 0 || Graphics.Height == 0) return;
        var mouse = Input.GetMousePosition();
        var world = _camera.ScreenToWorldPoint(new Vector3f(mouse.X / (float)Graphics.Width,
            mouse.Y / (float)Graphics.Height, 10.0f));
        _particleNode.SetPosition(world);
    }
}
