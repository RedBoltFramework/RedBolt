// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DIsometricDemo;

using System;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Sample.Utilities2D;
using RedBolt.Scene;
using RedBolt.Scene2D;

internal static class Urho2DIsometricDemoProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DIsometricDemo(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Playable isometric TMX, Spriter and Box2D demonstration.</summary>
public sealed class Urho2DIsometricDemo : Application
{
    private Node? _cameraNode;
    private Node? _character;
    private RigidBody2D? _characterBody;
    private PhysicsWorld2D? _world;
    private float _verticalSpeedScale = 0.5f;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DIsometricDemo";
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Isometric Demo";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        _world = scene.CreateComponent<PhysicsWorld2D>();
        _world.Gravity = Vector2f.Zero;
        _cameraNode = scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(2.0f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        if (Resources.Get<TmxFile2D>("Urho2D/Tilesets/atrium.tmx") is { } tmx)
        {
            var tileMapNode = scene.CreateChild("TileMap");
            var tileMap = tileMapNode.CreateComponent<TileMap2D>();
            tileMap.TmxFile = tmx;
            var info = tileMap.Info;
            var utilities = new Sample2D(scene, Resources);
            _character = utilities.CreateCharacter(info, 0.0f, new Vector3f(-5.0f, 11.0f, 0.0f), 0.15f);
            _characterBody = _character.GetComponent<RigidBody2D>();
            _verticalSpeedScale = info.TileWidth != 0.0f ? info.TileHeight / info.TileWidth : 0.5f;
            if (tileMap.NumLayers > 0)
                utilities.CreateCollisionShapesFromTmxObjects(tileMapNode, tileMap.GetLayer(tileMap.NumLayers - 1), info);
            if (tileMap.NumLayers > 2)
                utilities.PopulateCoins(tileMap.GetLayer(tileMap.NumLayers - 3));
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<PhysicsEvents2D.PhysicsBeginContact2DEvent>(_world, HandleBeginContact);
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) =>
        {
            if (_drawDebug) _world.DrawDebugGeometry();
        });
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        if (Input.GetKeyPress(Key.Z)) _drawDebug = !_drawDebug;
        if (_characterBody is null || _character is null || _cameraNode is null) return;
        const float speed = 3.0f;
        var velocity = Vector2f.Zero;
        if (Input.GetKeyDown(Key.W)) velocity.Y += speed * _verticalSpeedScale;
        if (Input.GetKeyDown(Key.S)) velocity.Y -= speed * _verticalSpeedScale;
        if (Input.GetKeyDown(Key.A)) velocity.X -= speed;
        if (Input.GetKeyDown(Key.D)) velocity.X += speed;
        _characterBody.LinearVelocity = velocity;
        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite)
        {
            if (velocity.X != 0.0f) sprite.FlipX = velocity.X < 0.0f;
            sprite.SetAnimation(velocity == Vector2f.Zero ? "idle" : "run");
        }
        _cameraNode.SetPosition(_character.GetWorldPosition() + new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (Input.GetKeyDown(Key.PageUp)) camera.SetZoom(camera.Zoom * 1.01f);
        if (Input.GetKeyDown(Key.PageDown)) camera.SetZoom(camera.Zoom * 0.99f);
    }

    private void HandleBeginContact(ref PhysicsEvents2D.PhysicsBeginContact2DEvent value)
    {
        var other = ReferenceEquals(value.NodeA, _character) ? value.NodeB
            : ReferenceEquals(value.NodeB, _character) ? value.NodeA : null;
        if (other?.Name == "Coin")
            other.Remove();
    }
}
