// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DSprite;

using System;
using System.Collections.Generic;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DSpriteProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DSprite(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates static and Spriter-animated 2D sprites.</summary>
public sealed class Urho2DSprite : Application
{
    private const int NumSprites = 200;
    private readonly List<(Node Node, Vector2f MoveSpeed, float RotateSpeed)> _sprites = [];
    private Node? _cameraNode;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DSprite";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Sprite";
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

        if (Resources.Get<Sprite2D>("Urho2D/Aster.png") is { } sprite)
        {
            var halfWidth = Graphics.Width * 0.5f * Sprite2D.PixelSize;
            var halfHeight = Graphics.Height * 0.5f * Sprite2D.PixelSize;
            for (var i = 0; i < NumSprites; ++i)
            {
                var node = scene.CreateChild("StaticSprite2D");
                node.SetPosition2D(MathDefs.Random(-halfWidth, halfWidth), MathDefs.Random(-halfHeight, halfHeight));
                var drawable = node.CreateComponent<StaticSprite2D>();
                drawable.Sprite = sprite;
                drawable.Color = new Color(MathDefs.Random(), MathDefs.Random(), MathDefs.Random());
                _sprites.Add((node, new Vector2f(MathDefs.Random(-2.0f, 2.0f), MathDefs.Random(-2.0f, 2.0f)),
                    MathDefs.Random(-90.0f, 90.0f)));
            }
        }

        if (Resources.Get<AnimationSet2D>("Urho2D/GoldIcon.scml") is { } animationSet)
        {
            var node = scene.CreateChild("AnimatedSprite2D");
            node.SetPosition(new Vector3f(0.0f, 0.0f, -1.0f));
            var animatedSprite = node.CreateComponent<AnimatedSprite2D>();
            animatedSprite.AnimationSet = animationSet;
            animatedSprite.SetAnimation("idle");
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        MoveCamera(timeStep);
        var halfWidth = Graphics.Width * 0.5f * Sprite2D.PixelSize;
        var halfHeight = Graphics.Height * 0.5f * Sprite2D.PixelSize;
        for (var i = 0; i < _sprites.Count; ++i)
        {
            var entry = _sprites[i];
            var position = entry.Node.GetPosition2D() + entry.MoveSpeed * timeStep;
            var speed = entry.MoveSpeed;
            if (position.X < -halfWidth || position.X > halfWidth) speed.X = -speed.X;
            if (position.Y < -halfHeight || position.Y > halfHeight) speed.Y = -speed.Y;
            entry.Node.SetPosition2D(position);
            entry.Node.Rotate2D(entry.RotateSpeed * timeStep);
            _sprites[i] = (entry.Node, speed, entry.RotateSpeed);
        }
    }

    private void MoveCamera(float timeStep)
    {
        const float moveSpeed = 4.0f;
        var movement = Vector2f.Zero;
        if (Input.GetKeyDown(Key.W)) movement.Y += moveSpeed * timeStep;
        if (Input.GetKeyDown(Key.S)) movement.Y -= moveSpeed * timeStep;
        if (Input.GetKeyDown(Key.A)) movement.X -= moveSpeed * timeStep;
        if (Input.GetKeyDown(Key.D)) movement.X += moveSpeed * timeStep;
        _cameraNode!.Translate2D(movement);
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (Input.GetKeyDown(Key.PageUp)) camera.SetZoom(camera.Zoom * 1.01f);
        if (Input.GetKeyDown(Key.PageDown)) camera.SetZoom(camera.Zoom * 0.99f);
    }
}
