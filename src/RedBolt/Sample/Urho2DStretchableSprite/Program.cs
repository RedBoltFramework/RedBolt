// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DStretchableSprite;

using System;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DStretchableSpriteProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DStretchableSprite(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates nine-slice stretchable sprites.</summary>
public sealed class Urho2DStretchableSprite : Application
{
    private Node? _referenceNode;
    private Node? _stretchNode;
    private float _elapsed;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DStretchableSprite";
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Stretchable Sprite";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;

        if (Resources.Get<Sprite2D>("Urho2D/Stretchable.png") is { } sprite)
        {
            _referenceNode = scene.CreateChild("Regular Sprite");
            _referenceNode.SetPosition2D(-2.0f, 0.0f);
            _referenceNode.CreateComponent<StaticSprite2D>().Sprite = sprite;
            _stretchNode = scene.CreateChild("Stretchable Sprite");
            _stretchNode.SetPosition2D(2.0f, 0.0f);
            var stretchable = _stretchNode.CreateComponent<StretchableSprite2D>();
            stretchable.Sprite = sprite;
            stretchable.Border = new IntRect(25, 25, 25, 25);
        }
        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        _elapsed += timeStep;
        var scale = new Vector2f(1.0f + (MathF.Sin(_elapsed) + 1.0f) * 0.75f,
            1.0f + (MathF.Cos(_elapsed * 0.7f) + 1.0f) * 0.5f);
        _stretchNode?.SetScale2D(scale);
        _referenceNode?.SetRotation2D(MathF.Sin(_elapsed * 0.5f) * 15.0f);
    }
}
