// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Physics2D;

using System;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Physics2DProgram
{
    private static int Main()
    {
        try { using var application = new Physics2D(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates sprites driven by Box2D rigid bodies.</summary>
public sealed class Physics2D : Application
{
    private Node? _cameraNode;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2D";
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        scene.CreateComponent<PhysicsWorld2D>();
        _cameraNode = scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(1.2f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        var boxSprite = Resources.Get<Sprite2D>("Urho2D/Box.png");
        var ballSprite = Resources.Get<Sprite2D>("Urho2D/Ball.png");
        var ground = scene.CreateChild("Ground");
        ground.SetPosition2D(0.0f, -3.0f);
        ground.SetScale(new Vector3f(200.0f, 1.0f, 1.0f));
        ground.CreateComponent<RigidBody2D>();
        ground.CreateComponent<StaticSprite2D>().Sprite = boxSprite;
        var groundShape = ground.CreateComponent<CollisionBox2D>();
        groundShape.Size = new Vector2f(0.32f, 0.32f);
        groundShape.Friction = 0.5f;

        for (var i = 0; i < 30; ++i)
        {
            var node = scene.CreateChild("RigidBody");
            node.SetPosition2D(MathDefs.Random(-0.1f, 0.1f), 5.0f + i * 0.4f);
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Dynamic;
            var drawable = node.CreateComponent<StaticSprite2D>();
            if (i % 2 == 0)
            {
                drawable.Sprite = boxSprite;
                var box = node.CreateComponent<CollisionBox2D>();
                box.Size = new Vector2f(0.32f, 0.32f);
                SetShapeProperties(box);
            }
            else
            {
                drawable.Sprite = ballSprite;
                var circle = node.CreateComponent<CollisionCircle2D>();
                circle.Radius = 0.16f;
                SetShapeProperties(circle);
            }
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
    }

    private static void SetShapeProperties(CollisionShape2D shape)
    {
        shape.Density = 1.0f;
        shape.Friction = 0.5f;
        shape.Restitution = 0.1f;
    }
}
