// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Physics2DConstraints;

using System;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Physics2DConstraintsProgram
{
    private static int Main()
    {
        try { using var application = new Physics2DConstraints(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Demonstrates every 2D constraint supported by Box2D 3.</summary>
public sealed class Physics2DConstraints : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private PhysicsWorld2D? _world;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics2DConstraints";
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics2D Constraints";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        _world = _scene.CreateComponent<PhysicsWorld2D>();
        _world.DrawJoint = true;
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(0.85f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        CreateDistance(new Vector2f(-5.0f, 2.8f));
        CreateFriction(new Vector2f(-1.8f, 2.8f));
        CreateMotor(new Vector2f(1.5f, 2.8f));
        CreateMouse(new Vector2f(4.5f, 2.8f));
        CreatePrismatic(new Vector2f(-5.0f, -0.2f));
        CreateRevolute(new Vector2f(-1.8f, -0.2f));
        CreateRope(new Vector2f(1.5f, -0.2f));
        CreateWeld(new Vector2f(4.5f, -0.2f));
        CreateWheel(new Vector2f(0.0f, -3.0f));

        Renderer.SetViewport(0, new Viewport(_scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => _world.DrawDebugGeometry());
    }

    private (Node Node, RigidBody2D Body) CreateBody(string name, Vector2f position, bool circle, BodyType2D type)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition2D(position);
        var body = node.CreateComponent<RigidBody2D>();
        body.BodyType = type;
        if (circle)
        {
            node.CreateComponent<StaticSprite2D>().Sprite = Resources.Get<Sprite2D>("Urho2D/Ball.png");
            var shape = node.CreateComponent<CollisionCircle2D>();
            shape.Radius = 0.32f;
            shape.Density = 1.0f;
        }
        else
        {
            node.CreateComponent<StaticSprite2D>().Sprite = Resources.Get<Sprite2D>("Urho2D/Box.png");
            var shape = node.CreateComponent<CollisionBox2D>();
            shape.Size = new Vector2f(0.64f, 0.64f);
            shape.Density = 1.0f;
        }
        return (node, body);
    }

    private void CreateDistance(Vector2f origin)
    {
        var a = CreateBody("Distance anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Distance body", origin + new Vector2f(1.4f, 0.0f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintDistance2D>();
        joint.OtherBody = b.Body; joint.OwnerBodyAnchor = origin; joint.OtherBodyAnchor = b.Node.GetPosition2D();
        joint.Length = 1.4f; joint.FrequencyHz = 4.0f; joint.DampingRatio = 0.5f;
    }

    private void CreateFriction(Vector2f origin)
    {
        var a = CreateBody("Friction anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Friction body", origin + new Vector2f(1.0f, 0.0f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintFriction2D>();
        joint.OtherBody = b.Body; joint.Anchor = origin; joint.MaxForce = 5.0f; joint.MaxTorque = 2.0f;
    }

    private void CreateMotor(Vector2f origin)
    {
        var a = CreateBody("Motor anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Motor body", origin + new Vector2f(0.0f, 1.0f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintMotor2D>();
        joint.OtherBody = b.Body; joint.LinearOffset = new Vector2f(0.0f, 1.0f);
        joint.AngularOffset = 0.1f; joint.MaxForce = 5.0f; joint.MaxTorque = 10.0f; joint.CorrectionFactor = 1.0f;
    }

    private void CreateMouse(Vector2f origin)
    {
        var a = CreateBody("Mouse anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Mouse body", origin + new Vector2f(0.0f, -1.0f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintMouse2D>();
        joint.OtherBody = b.Body; joint.Target = origin + new Vector2f(0.8f, 0.5f);
        joint.MaxForce = 50.0f; joint.FrequencyHz = 5.0f; joint.DampingRatio = 0.7f;
    }

    private void CreatePrismatic(Vector2f origin)
    {
        var a = CreateBody("Prismatic anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Prismatic body", origin + new Vector2f(0.8f, 0.8f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintPrismatic2D>();
        joint.OtherBody = b.Body; joint.Anchor = origin; joint.Axis = new Vector2f(1.0f, 1.0f);
        joint.EnableLimit = true; joint.LowerTranslation = -1.0f; joint.UpperTranslation = 1.0f;
    }

    private void CreateRevolute(Vector2f origin)
    {
        var a = CreateBody("Revolute anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Revolute body", origin + new Vector2f(1.0f, 0.5f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintRevolute2D>();
        joint.OtherBody = b.Body; joint.Anchor = origin + new Vector2f(0.5f, 0.0f);
        joint.EnableLimit = true; joint.LowerAngle = -1.0f; joint.UpperAngle = 0.5f;
        joint.EnableMotor = true; joint.MaxMotorTorque = 10.0f;
    }

    private void CreateRope(Vector2f origin)
    {
        var a = CreateBody("Rope anchor", origin, false, BodyType2D.Static);
        var b = CreateBody("Rope body", origin + new Vector2f(0.0f, -1.5f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintRope2D>();
        joint.OtherBody = b.Body; joint.OwnerBodyAnchor = origin; joint.OtherBodyAnchor = b.Node.GetPosition2D();
        joint.MaxLength = 1.6f; joint.CollideConnected = true;
    }

    private void CreateWeld(Vector2f origin)
    {
        var a = CreateBody("Weld body A", origin, false, BodyType2D.Dynamic);
        var b = CreateBody("Weld body B", origin + new Vector2f(1.0f, 0.0f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintWeld2D>();
        joint.OtherBody = b.Body; joint.Anchor = origin + new Vector2f(0.5f, 0.0f);
        joint.FrequencyHz = 4.0f; joint.DampingRatio = 0.5f;
    }

    private void CreateWheel(Vector2f origin)
    {
        var a = CreateBody("Wheel chassis", origin, false, BodyType2D.Dynamic);
        a.Node.SetScale2D(3.0f, 1.0f);
        var b = CreateBody("Wheel", origin + new Vector2f(1.0f, -0.6f), true, BodyType2D.Dynamic);
        var joint = a.Node.CreateComponent<ConstraintWheel2D>();
        joint.OtherBody = b.Body; joint.Anchor = b.Node.GetPosition2D(); joint.Axis = Vector2f.Up;
        joint.EnableMotor = true; joint.MaxMotorTorque = 10.0f; joint.MotorSpeed = 1.0f;
        joint.FrequencyHz = 4.0f; joint.DampingRatio = 0.5f;
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        if (Input.GetKeyPress(Key.Space)) _world!.DrawShape = !_world.DrawShape;
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
