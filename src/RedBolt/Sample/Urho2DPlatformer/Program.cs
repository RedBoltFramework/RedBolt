// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Urho2DPlatformer;

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

internal static class Urho2DPlatformerProgram
{
    private static int Main()
    {
        try { using var application = new Urho2DPlatformer(); return application.Run(); }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}

/// <summary>Playable orthogonal TMX platformer using Spriter and Box2D.</summary>
public sealed class Urho2DPlatformer : Application
{
    private Node? _cameraNode;
    private Node? _character;
    private RigidBody2D? _characterBody;
    private PhysicsWorld2D? _world;
    private int _groundContacts;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Urho2DPlatformer";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Urho2D Platformer";
    }

    protected override void Start()
    {
        Scene2D.RegisterUrho2DLibrary(Context);
        RedBolt.PhysicsBox2D.Physics2D.RegisterPhysics2DLibrary(Context);
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        scene.CreateComponent<DebugRenderer>();
        _world = scene.CreateComponent<PhysicsWorld2D>();
        _cameraNode = scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetOrthographic(true);
        camera.OrthoSize = Graphics.Height * Sprite2D.PixelSize;
        camera.SetZoom(2.0f * MathF.Min(Graphics.Width / 1280.0f, Graphics.Height / 800.0f));

        if (Resources.Get<TmxFile2D>("Urho2D/Tilesets/Ortho.tmx") is { } tmx)
        {
            var tileMapNode = scene.CreateChild("TileMap");
            var tileMap = tileMapNode.CreateComponent<TileMap2D>();
            tileMap.TmxFile = tmx;
            var utilities = new Sample2D(scene, Resources);
            _character = utilities.CreateCharacter(tileMap.Info, 0.8f, new Vector3f(1.0f, 8.0f, 0.0f), 0.2f);
            _characterBody = _character.GetComponent<RigidBody2D>();
            _characterBody!.FixedRotation = true;
            if (tileMap.NumLayers > 0)
                utilities.CreateCollisionShapesFromTmxObjects(tileMapNode, tileMap.GetLayer(tileMap.NumLayers - 1), tileMap.Info);
            if (tileMap.NumLayers > 2)
                utilities.PopulateCoins(tileMap.GetLayer(tileMap.NumLayers - 3));
        }

        Renderer.SetViewport(0, new Viewport(scene, camera));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
        SubscribeToEvent<PhysicsEvents2D.PhysicsBeginContact2DEvent>(_world, HandleBeginContact);
        SubscribeToEvent<PhysicsEvents2D.PhysicsEndContact2DEvent>(_world, HandleEndContact);
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
        var velocity = _characterBody.LinearVelocity;
        velocity.X = 0.0f;
        if (Input.GetKeyDown(Key.A)) velocity.X -= 4.0f;
        if (Input.GetKeyDown(Key.D)) velocity.X += 4.0f;
        if (Input.GetKeyPress(Key.Space) && _groundContacts > 0) velocity.Y = 7.0f;
        _characterBody.LinearVelocity = velocity;
        if (_character.GetComponent<AnimatedSprite2D>() is { } sprite)
        {
            if (velocity.X != 0.0f) sprite.FlipX = velocity.X > 0.0f;
            sprite.SetAnimation(_groundContacts == 0 ? "jump" : velocity.X == 0.0f ? "idle" : "run");
        }
        _cameraNode.SetPosition(_character.GetWorldPosition() + new Vector3f(0.0f, 1.5f, -10.0f));
        var camera = _cameraNode.GetComponent<Camera>()!;
        if (Input.GetKeyDown(Key.PageUp)) camera.SetZoom(camera.Zoom * 1.01f);
        if (Input.GetKeyDown(Key.PageDown)) camera.SetZoom(camera.Zoom * 0.99f);
    }

    private void HandleBeginContact(ref PhysicsEvents2D.PhysicsBeginContact2DEvent value)
    {
        var other = ReferenceEquals(value.NodeA, _character) ? value.NodeB
            : ReferenceEquals(value.NodeB, _character) ? value.NodeA : null;
        if (other is null) return;
        if (other.Name == "Coin") other.Remove();
        else if (!value.ShapeA.Trigger && !value.ShapeB.Trigger) ++_groundContacts;
    }

    private void HandleEndContact(ref PhysicsEvents2D.PhysicsEndContact2DEvent value)
    {
        if ((ReferenceEquals(value.NodeA, _character) || ReferenceEquals(value.NodeB, _character)) &&
            !value.ShapeA.Trigger && !value.ShapeB.Trigger)
            _groundContacts = Math.Max(0, _groundContacts - 1);
    }
}
