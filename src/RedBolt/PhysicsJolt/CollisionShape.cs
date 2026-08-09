//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.PhysicsJolt;

using Color = RedBolt.Math.Color;
using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using Sphere = RedBolt.Math.Sphere;
using RedBolt.Scene;

/// <summary>Physics collision shape component.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class CollisionShape : Component
{
    private const float DefaultCollisionMargin = 0.04f;
    private const float MinimumExtent = 0.001f;

    private WeakReference<PhysicsWorld>? _physicsWorld;
    private WeakReference<RigidBody>? _rigidBody;
    private Shape? _shape;
    private ShapeType _shapeType = ShapeType.Box;
    private Vector3f _position;
    private Quaternion _rotation = Quaternion.IDENTITY;
    private Vector3f _size = Vector3f.One;
    private Vector3f _cachedWorldScale = Vector3f.One;
    private float _margin = DefaultCollisionMargin;
    private bool _recreateShape = true;

    /// <summary>Construct.</summary>
    public CollisionShape()
    {
    }

    /// <summary>Destruct. Free the native shape.</summary>
    public override void Dispose()
    {
        ReleaseShape();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
            physicsWorld.RemoveCollisionShape(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    /// <summary>Apply attribute changes that cannot be applied immediately.</summary>
    public override void ApplyAttributes()
    {
        if (_recreateShape)
            UpdateShape();
    }

    /// <summary>Handle enabled or disabled state change.</summary>
    public override void OnSetEnabled()
    {
        NotifyRigidBody();
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || _node is null || !IsEnabledEffective || _shape is null)
            return;

        var color = GetComponent<RigidBody>()?.IsActive == true ? Color.White : Color.Green;
        DrawDebugGeometry(debug, color, depthTest);
    }

    /// <summary>Draw the shape as debug geometry.</summary>
    public void DrawDebugGeometry(DebugRenderer debug, Color color, bool depthTest)
    {
        ArgumentNullException.ThrowIfNull(debug);
        if (_node is null)
            return;

        var body = GetComponent<RigidBody>();
        var worldPosition = body?.Position ?? _node.WorldPosition;
        var worldRotation = body?.Rotation ?? _node.WorldRotation;
        var worldScale = _node.WorldScale.Abs;
        var shapePosition = worldPosition + worldRotation * (worldScale * _position);
        var shapeRotation = worldRotation * _rotation;
        var scaledSize = _size.Abs * worldScale;

        switch (_shapeType)
        {
            case ShapeType.Box:
                debug.AddBoundingBox(
                    new BoundingBox(-scaledSize * 0.5f, scaledSize * 0.5f),
                    new Matrix3x4(shapePosition, shapeRotation, Vector3f.One), color, depthTest);
                break;

            case ShapeType.Sphere:
                debug.AddSphere(new Sphere(shapePosition, MathF.Max(scaledSize.X, MathF.Max(scaledSize.Y, scaledSize.Z)) * 0.5f), color, depthTest);
                break;

            case ShapeType.Cylinder:
                debug.AddCylinder(shapePosition, MathF.Max(scaledSize.X, scaledSize.Z) * 0.5f, scaledSize.Y, color, depthTest);
                break;

            case ShapeType.Capsule:
                var radius = MathF.Max(scaledSize.X, scaledSize.Z) * 0.5f;
                var cylinderHeight = MathF.Max(scaledSize.Y - 2.0f * radius, 0.0f);
                debug.AddCylinder(shapePosition, radius, cylinderHeight, color, depthTest);
                debug.AddSphere(new Sphere(shapePosition + shapeRotation * new Vector3f(0.0f, cylinderHeight * 0.5f, 0.0f), radius), color, depthTest);
                debug.AddSphere(new Sphere(shapePosition - shapeRotation * new Vector3f(0.0f, cylinderHeight * 0.5f, 0.0f), radius), color, depthTest);
                break;
        }
    }

    /// <summary>Set as a box.</summary>
    public void SetBox(Vector3f size, Vector3f position = default, Quaternion? rotation = null)
    {
        _shapeType = ShapeType.Box;
        _size = size;
        _position = position;
        _rotation = rotation ?? Quaternion.IDENTITY;
        UpdateShape();
    }

    /// <summary>Set as a sphere.</summary>
    public void SetSphere(float diameter, Vector3f position = default, Quaternion? rotation = null)
    {
        _shapeType = ShapeType.Sphere;
        _size = new Vector3f(diameter, diameter, diameter);
        _position = position;
        _rotation = rotation ?? Quaternion.IDENTITY;
        UpdateShape();
    }

    /// <summary>Set as a cylinder.</summary>
    public void SetCylinder(float diameter, float height, Vector3f position = default, Quaternion? rotation = null)
    {
        _shapeType = ShapeType.Cylinder;
        _size = new Vector3f(diameter, height, diameter);
        _position = position;
        _rotation = rotation ?? Quaternion.IDENTITY;
        UpdateShape();
    }

    /// <summary>Set as a capsule.</summary>
    public void SetCapsule(float diameter, float height, Vector3f position = default, Quaternion? rotation = null)
    {
        _shapeType = ShapeType.Capsule;
        _size = new Vector3f(diameter, height, diameter);
        _position = position;
        _rotation = rotation ?? Quaternion.IDENTITY;
        UpdateShape();
    }

    /// <summary>Return or set shape type.</summary>
    [Serialized("Shape Type")]
    public ShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (_shapeType == value)
                return;
            _shapeType = value;
            UpdateShape();
        }
    }

    /// <summary>Return or set shape size.</summary>
    [Serialized("Size")]
    public Vector3f Size
    {
        get => _size;
        set
        {
            if (_size == value)
                return;
            _size = value;
            UpdateShape();
        }
    }

    /// <summary>Return or set offset position.</summary>
    [Serialized("Offset Position")]
    public Vector3f Position
    {
        get => _position;
        set
        {
            if (_position == value)
                return;
            _position = value;
            UpdateShape();
        }
    }

    /// <summary>Return or set offset rotation.</summary>
    [Serialized("Offset Rotation")]
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;
            _rotation = value;
            UpdateShape();
        }
    }

    /// <summary>Set offset transform.</summary>
    public void SetTransform(Vector3f position, Quaternion rotation)
    {
        if (_position == position && _rotation == rotation)
            return;
        _position = position;
        _rotation = rotation;
        UpdateShape();
    }

    /// <summary>Return or set collision margin.</summary>
    [Serialized("Collision Margin")]
    public float Margin
    {
        get => _margin;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (_margin == value)
                return;
            _margin = value;
            UpdateShape();
        }
    }

    /// <summary>Return the native Jolt collision shape.</summary>
    public Shape? JoltShape => _shape;

    /// <summary>Return whether this shape type has a Jolt scene-glue implementation.</summary>
    public bool IsSupported => _shapeType is ShapeType.Box or ShapeType.Sphere or ShapeType.Cylinder or ShapeType.Capsule;

    /// <summary>Update the native collision shape and notify the rigid body.</summary>
    public void NotifyRigidBody()
    {
        if (_rigidBody is null || !_rigidBody.TryGetTarget(out var rigidBody))
        {
            rigidBody = GetComponent<RigidBody>();
            _rigidBody = rigidBody is null ? null : new WeakReference<RigidBody>(rigidBody);
        }
        rigidBody?.NotifyShapeChanged(this);
    }

    /// <summary>Release the collision shape.</summary>
    public void ReleaseShape()
    {
        _shape?.Dispose();
        _shape = null;
        NotifyRigidBody();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        if (currentNode is not null)
        {
            currentNode.AddListener(this);
            _cachedWorldScale = currentNode.WorldScale;
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is not null)
        {
            var physicsWorld = scene.GetOrCreateComponent<PhysicsWorld>();
            _physicsWorld = new WeakReference<PhysicsWorld>(physicsWorld);
            physicsWorld.AddCollisionShape(this);
            UpdateShape();
        }
        else
        {
            ReleaseShape();
            if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
                physicsWorld.RemoveCollisionShape(this);
            _physicsWorld = null;
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        var worldScale = node.WorldScale;
        if ((_cachedWorldScale - worldScale).LengthSquared <= 0.01f)
            return;
        _cachedWorldScale = worldScale;
        UpdateShape();
    }

    private void UpdateShape()
    {
        _recreateShape = false;
        _shape?.Dispose();
        _shape = null;
        if (_node is null || _physicsWorld is null || !_physicsWorld.TryGetTarget(out _))
        {
            _recreateShape = true;
            return;
        }

        var worldScale = _node.WorldScale.Abs;
        _cachedWorldScale = _node.WorldScale;
        Shape? shape = _shapeType switch
        {
            ShapeType.Box => CreateBox(_size.Abs * worldScale),
            ShapeType.Sphere => CreateSphere(_size.Abs * worldScale),
            ShapeType.Cylinder => CreateCylinder(_size.Abs * worldScale),
            ShapeType.Capsule => CreateCapsule(_size.Abs * worldScale),
            _ => null
        };

        if (shape is not null && (_position != Vector3f.Zero || !_rotation.ApproximatelyEquivalent(Quaternion.IDENTITY)))
        {
            var decorated = new RotatedTranslatedShape(worldScale * _position, _rotation, shape);
            shape.Dispose();
            shape = decorated;
        }

        _shape = shape;
        NotifyRigidBody();
    }

    private Shape CreateBox(Vector3f size)
    {
        var halfExtent = new Vector3f(
            MathF.Max(size.X * 0.5f, MinimumExtent),
            MathF.Max(size.Y * 0.5f, MinimumExtent),
            MathF.Max(size.Z * 0.5f, MinimumExtent));
        var convexRadius = MathF.Min(_margin, MathF.Min(halfExtent.X, MathF.Min(halfExtent.Y, halfExtent.Z)));
        return new BoxShape(halfExtent, convexRadius);
    }

    private static Shape CreateSphere(Vector3f size)
    {
        var sphere = new SphereShape(0.5f);
        var scale = new Vector3f(
            MathF.Max(size.X, MinimumExtent),
            MathF.Max(size.Y, MinimumExtent),
            MathF.Max(size.Z, MinimumExtent));
        if (MathF.Abs(scale.X - scale.Y) <= MathDefs.Epsilon && MathF.Abs(scale.X - scale.Z) <= MathDefs.Epsilon)
        {
            sphere.Dispose();
            return new SphereShape(scale.X * 0.5f);
        }

        var scaled = new ScaledShape(sphere, scale);
        sphere.Dispose();
        return scaled;
    }

    private Shape CreateCylinder(Vector3f size)
    {
        var halfHeight = MathF.Max(size.Y * 0.5f, MinimumExtent);
        var radius = MathF.Max(MathF.Max(size.X, size.Z) * 0.5f, MinimumExtent);
        var convexRadius = MathF.Min(_margin, MathF.Min(halfHeight, radius));
        return new CylinderShape(halfHeight, radius, convexRadius);
    }

    private static Shape CreateCapsule(Vector3f size)
    {
        var radius = MathF.Max(MathF.Max(size.X, size.Z) * 0.5f, MinimumExtent);
        var halfCylinder = MathF.Max(size.Y * 0.5f - radius, MinimumExtent);
        return new CapsuleShape(halfCylinder, radius);
    }
}
