// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Sample.Utilities2D;

using System;
using System.Globalization;
using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.PhysicsBox2D;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Scene2D;

/// <summary>Shared scene construction helpers used by the 2D game samples.</summary>
public sealed class Sample2D
{
    private readonly Scene _scene;
    private readonly ResourceCache _resources;

    public Sample2D(Scene scene, ResourceCache resources)
    {
        _scene = scene;
        _resources = resources;
    }

    public void CreateCollisionShapesFromTmxObjects(Node tileMapNode, TileMapLayer2D? tileMapLayer, TileMapInfo2D info)
    {
        if (tileMapLayer is null)
            return;
        tileMapNode.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Static;
        for (uint i = 0; i < tileMapLayer.NumObjects; ++i)
        {
            if (tileMapLayer.GetObject(i) is not { } value)
                continue;
            CollisionShape2D? shape = value.ObjectType switch
            {
                TileMapObjectType2D.Rectangle => CreateRectangleShape(tileMapNode, value, info),
                TileMapObjectType2D.Ellipse => CreateCircleShape(tileMapNode, value, info),
                TileMapObjectType2D.Polygon => CreatePolygonShape(tileMapNode, value),
                TileMapObjectType2D.Polyline => CreatePolylineShape(tileMapNode, value),
                _ => null,
            };
            if (shape is null)
                continue;
            shape.Friction = value.HasProperty("Friction") &&
                float.TryParse(value.GetProperty("Friction"), NumberStyles.Float, CultureInfo.InvariantCulture, out var friction)
                    ? friction : 0.8f;
        }
    }

    public Node CreateCharacter(TileMapInfo2D info, float friction, Vector3f position, float scale)
    {
        var node = _scene.CreateChild("Characters").CreateChild("Imp");
        node.SetPosition(position);
        node.SetScale(scale);
        var animatedSprite = node.CreateComponent<AnimatedSprite2D>();
        animatedSprite.AnimationSet = _resources.Get<AnimationSet2D>("Urho2D/imp/imp.scml");
        animatedSprite.SetAnimation("idle");
        animatedSprite.Layer = 3;
        var body = node.CreateComponent<RigidBody2D>();
        body.BodyType = BodyType2D.Dynamic;
        body.AllowSleep = false;
        var shape = node.CreateComponent<CollisionCircle2D>();
        shape.Radius = 1.1f;
        shape.Friction = friction;
        shape.Restitution = 0.1f;
        return node;
    }

    public void PopulateCoins(TileMapLayer2D? layer)
    {
        if (layer is null)
            return;
        for (uint i = 0; i < layer.NumObjects; ++i)
        {
            if (layer.GetObject(i) is not { } value)
                continue;
            var node = _scene.CreateChild("Coin");
            node.SetScale(0.5f);
            node.SetPosition2D(value.Position + value.Size * 0.5f + new Vector2f(0.0f, 0.16f));
            var sprite = node.CreateComponent<AnimatedSprite2D>();
            sprite.AnimationSet = _resources.Get<AnimationSet2D>("Urho2D/GoldIcon.scml");
            sprite.SetAnimation("idle");
            sprite.Layer = 4;
            node.CreateComponent<RigidBody2D>().BodyType = BodyType2D.Static;
            var shape = node.CreateComponent<CollisionCircle2D>();
            shape.Radius = 0.32f;
            shape.Trigger = true;
        }
    }

    private static CollisionShape2D CreateRectangleShape(Node node, TileMapObject2D value, TileMapInfo2D info)
    {
        var shape = node.CreateComponent<CollisionBox2D>();
        shape.Size = value.Size;
        if (info.Orientation == Orientation2D.Orthogonal)
            shape.Center = value.Position + value.Size * 0.5f;
        else
        {
            shape.Center = value.Position + new Vector2f(info.TileWidth * 0.5f, 0.0f);
            shape.Angle = 45.0f;
        }
        return shape;
    }

    private static CollisionShape2D CreateCircleShape(Node node, TileMapObject2D value, TileMapInfo2D info)
    {
        var shape = node.CreateComponent<CollisionCircle2D>();
        shape.Center = info.Orientation == Orientation2D.Orthogonal
            ? value.Position + value.Size * 0.5f
            : value.Position + new Vector2f(info.TileWidth * 0.5f, 0.0f);
        shape.Radius = value.Size.X * 0.5f;
        return shape;
    }

    private static CollisionShape2D CreatePolygonShape(Node node, TileMapObject2D value)
    {
        var shape = node.CreateComponent<CollisionPolygon2D>();
        var points = new Vector2f[value.NumPoints];
        for (uint i = 0; i < value.NumPoints; ++i)
            points[i] = value.GetPoint(i);
        shape.SetVertices(points);
        return shape;
    }

    private static CollisionShape2D CreatePolylineShape(Node node, TileMapObject2D value)
    {
        var shape = node.CreateComponent<CollisionChain2D>();
        var points = new Vector2f[value.NumPoints];
        for (uint i = 0; i < value.NumPoints; ++i)
            points[i] = value.GetPoint(i);
        shape.SetVertices(points);
        return shape;
    }
}
