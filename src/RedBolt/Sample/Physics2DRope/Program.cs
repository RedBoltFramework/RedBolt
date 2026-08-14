// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Physics2DRope;

using System;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Physics2DRopeProgram
{
    private static int Main()
    {
        try { using var application = new Physics2DRope(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates revolute joints and a maximum-length rope.</summary>
public sealed class Physics2DRope : Application
{
    private Node? _cameraNode;
    private PhysicsWorld2D? _world;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2DRope";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D Rope";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        _world = scene.CreateComponent<PhysicsWorld2D>();
        _world.DrawJoint = true;
        _cameraNode = scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * 0.05f;
        camera.SetZoom(1.5f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        var ground = scene.CreateChild("Ground");
        var groundBody = ground.CreateComponent<RigidBody2D>();
        ground.CreateComponent<CollisionEdge2D>().SetVertices(new Vector2f(-40.0f, 0.0f), new Vector2f(40.0f, 0.0f));
        const float y = 15.0f;
        var previousBody = groundBody;
        for (var i = 0; i < 10; ++i)
        {
            var node = scene.CreateChild("RigidBody");
            node.SetPosition2D(i == 9 ? i : 0.5f + i, y);
            var body = node.CreateComponent<RigidBody2D>();
            body.BodyType = BodyType2D.Dynamic;
            var box = node.CreateComponent<CollisionBox2D>();
            box.Friction = 0.2f;
            box.MaskBits = 0xffff & ~0x0002;
            if (i == 9)
            {
                body.AngularDamping = 0.4f;
                box.Size = new Vector2f(3.0f, 3.0f);
                box.Density = 100.0f;
                box.CategoryBits = 0x0002;
            }
            else
            {
                box.Size = new Vector2f(1.0f, 0.25f);
                box.Density = 20.0f;
                box.CategoryBits = 0x0001;
            }
            var joint = node.CreateComponent<ConstraintRevolute2D>();
            joint.OtherBody = previousBody;
            joint.Anchor = new Vector2f(i, y);
            joint.CollideConnected = false;
            previousBody = body;
        }
        var rope = ground.CreateComponent<ConstraintRope2D>();
        rope.OtherBody = previousBody;
        rope.OwnerBodyAnchor = new Vector2f(0.0f, y);
        rope.MaxLength = 9.01f;

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => _world.DrawDebugGeometry());
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
}
