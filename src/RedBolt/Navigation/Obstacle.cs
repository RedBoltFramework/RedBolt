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

using Microsoft.Extensions.Logging;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = RedBolt.Scene.Scene;

namespace RedBolt.Navigation;

/// <summary>Obstacle for dynamic navigation mesh.</summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class Obstacle : Component
{
    /// <summary>Radius of this obstacle.</summary>
    private float _radius = 5.0f;
    /// <summary>Height of this obstacle, extends 1/2 height below and 1/2 height above the owning node's position.</summary>
    private float _height = 5.0f;

    /// <summary>Id received from tile cache.</summary>
    private long _obstacleId;
    /// <summary>Pointer to the navigation mesh we belong to.</summary>
    private WeakReference<DynamicNavigationMesh>? _ownerMesh;

    /// <summary>Construct.</summary>
    public Obstacle()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_obstacleId > 0 && TryGetOwnerMesh(out var ownerMesh))
            ownerMesh.RemoveObstacle(this);
        _ownerMesh = null;
        base.Dispose();
    }

    /// <summary>Update the owning mesh when enabled status has changed.</summary>
    public override void OnSetEnabled()
    {
        if (TryGetOwnerMesh(out var ownerMesh))
        {
            if (IsEnabledEffective)
                ownerMesh.AddObstacle(this);
            else
                ownerMesh.RemoveObstacle(this);
        }
    }

    /// <summary>Get or set the height of this obstacle.</summary>
    [Serialized("Height", DefaultSource = nameof(GetDefaultHeight))]
    public float Height
    {
        get => _height;
        set
        {
            _height = value;
            if (IsEnabledEffective && TryGetOwnerMesh(out var ownerMesh))
                ownerMesh.ObstacleChanged(this);
        }
    }

    /// <summary>Get or set the blocking radius of this obstacle.</summary>
    [Serialized("Radius", DefaultSource = nameof(GetDefaultRadius))]
    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            if (IsEnabledEffective && TryGetOwnerMesh(out var ownerMesh))
                ownerMesh.ObstacleChanged(this);
        }
    }

    /// <summary>Get the internal obstacle ID.</summary>
    public long ObstacleID
    {
        get => _obstacleId;
        internal set => _obstacleId = value;
    }

    /// <summary>Render debug information.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
            debug.AddCylinder(Node.GetWorldPosition(), _radius, _height, new Color(0.0f, 1.0f, 1.0f), depthTest);
    }

    /// <summary>Simplified rendering of debug information for script usage.</summary>
    public void DrawDebugGeometry(bool depthTest)
    {
        var scene = Scene;
        if (scene is not null)
            DrawDebugGeometry(scene.GetComponent<DebugRenderer>(), depthTest);
    }

    /// <summary>Handle node being assigned.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (_node is not null)
            _node.AddListener(this);
    }

    /// <summary>Handle scene being assigned, identify our DynamicNavigationMesh.</summary>
    protected override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        if (scene is not null)
        {
            if (ReferenceEquals(scene, _node))
            {
                Log.GetLogger().LogWarning("{TypeName} should not be created to the root scene node", GetTypeName());
                return;
            }
            if (!TryGetOwnerMesh(out _))
            {
                var foundOwnerMesh = Node.FindComponent<DynamicNavigationMesh>(ComponentSearchFlag.ParentRecursive);
                _ownerMesh = foundOwnerMesh is not null ? new WeakReference<DynamicNavigationMesh>(foundOwnerMesh) : null;
            }
            if (TryGetOwnerMesh(out var ownerMesh))
            {
                if (IsEnabledEffective)
                    ownerMesh.AddObstacle(this);
                SubscribeToEvent<NavigationEvents.NavigationTileAddedEvent>(ownerMesh, HandleNavigationTileAdded);
            }
        }
        else
        {
            if (_obstacleId > 0 && TryGetOwnerMesh(out var ownerMesh))
                ownerMesh.RemoveObstacle(this);

            UnsubscribeFromEvent<NavigationEvents.NavigationTileAddedEvent>();
            _ownerMesh = null;
        }
    }

    /// <summary>Handle node transform being dirtied.</summary>
    protected override void OnMarkedDirty(Node node)
    {
        if (IsEnabledEffective && TryGetOwnerMesh(out var ownerMesh))
        {
            var scene = Scene;
            /// \hack If scene already unassigned, or if it's being destroyed, do nothing
            if (scene is null)
                return;

            // If within threaded update, update later
            if (scene.IsThreadedUpdate())
            {
                scene.DelayedMarkedDirty(this);
                return;
            }

            ownerMesh.ObstacleChanged(this);
        }
    }

    /// <summary>Handle navigation mesh tile added.</summary>
    private void HandleNavigationTileAdded(ref NavigationEvents.NavigationTileAddedEvent eventData)
    {
        // Re-add obstacle if it is intersected with newly added tile
        var tile = eventData.Tile;
        if (IsEnabledEffective && TryGetOwnerMesh(out var ownerMesh) && ownerMesh.IsObstacleInTile(this, tile))
            ownerMesh.ObstacleChanged(this);
    }

    private bool TryGetOwnerMesh(out DynamicNavigationMesh ownerMesh)
    {
        if (_ownerMesh is not null && _ownerMesh.TryGetTarget(out var target))
        {
            ownerMesh = target;
            return true;
        }
        ownerMesh = null!;
        return false;
    }

    private static float GetDefaultRadius() => 5.0f;
    private static float GetDefaultHeight() => 5.0f;
}
