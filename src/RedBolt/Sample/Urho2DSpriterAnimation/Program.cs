// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DSpriterAnimation;

using System;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DSpriterAnimationProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DSpriterAnimation(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates SCML Spriter animation playback.</summary>
public sealed class Urho2DSpriterAnimation : Application
{
    private Node? _cameraNode;
    private AnimatedSprite2D? _animatedSprite;
    private AnimationSet2D? _animationSet;
    private uint _animationIndex;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DSpriterAnimation";
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Spriter Animation";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        _cameraNode = scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(1.5f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        _animationSet = Resources.Get<AnimationSet2D>("Urho2D/imp/imp.scml");
        if (_animationSet is not null && _animationSet.NumAnimations != 0)
        {
            _animatedSprite = scene.CreateChild("SpriterAnimation").CreateComponent<AnimatedSprite2D>();
            _animatedSprite.AnimationSet = _animationSet;
            _animatedSprite.SetAnimation(_animationSet.GetAnimation(0));
        }
        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        const float speed = 4.0f;
        var movement = Vector2f.Zero;
        if (Input.GetKeyDown(Key.W)) movement.Y += speed * timeStep;
        if (Input.GetKeyDown(Key.S)) movement.Y -= speed * timeStep;
        if (Input.GetKeyDown(Key.A)) movement.X -= speed * timeStep;
        if (Input.GetKeyDown(Key.D)) movement.X += speed * timeStep;
        _cameraNode!.Translate2D(movement);
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (Input.GetKeyDown(Key.PageUp)) camera.SetZoom(camera.Zoom * 1.01f);
        if (Input.GetKeyDown(Key.PageDown)) camera.SetZoom(camera.Zoom * 0.99f);
        if ((Input.GetMouseButtonPress(MouseButton.Left) || Input.GetKeyPress(Key.Space)) &&
            _animationSet is { NumAnimations: > 0 } && _animatedSprite is not null)
        {
            _animationIndex = (_animationIndex + 1) % _animationSet.NumAnimations;
            _animatedSprite.SetAnimation(_animationSet.GetAnimation(_animationIndex), LoopMode2D.ForceLooped);
        }
    }
}
