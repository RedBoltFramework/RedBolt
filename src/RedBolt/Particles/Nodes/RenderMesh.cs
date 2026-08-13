//
// Copyright (c) 2021-2022 the rbfx project.
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

namespace RedBolt.Particles.ParticleGraphNodes;

using System;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Reflected]
public partial class RenderMesh : TemplateNode<RenderMeshInstance, Matrix3x4>
{
    /// <summary>Construct RenderMesh.</summary>
    public RenderMesh() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "transform", ParticleGraphContainerType.Auto)])
    {
        Model = new ResourceRef();
        _material = new ResourceRefList();
    }

    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new RenderMeshInstance(); instance.Init(this, layer); return instance; }

    private static object? GetModelDefault() => new ResourceRef();
    private static object? GetMaterialDefault() => new ResourceRefList();

    [Serialized("Model", DefaultSource = nameof(GetModelDefault))]
    public ResourceRef Model { get; set; }

    [Serialized("Material", DefaultSource = nameof(GetMaterialDefault))]
    public ResourceRefList Material
    {
        get => _material.Clone();
        set => _material = value.Clone();
    }

    [Serialized("Is Worldspace")]
    public bool IsWorldspace { get; set; }

    private ResourceRefList _material;
}

[Reflected(Factory = false)]
public partial class RenderMeshDrawable : StaticModel
{
    private static readonly Matrix3x4[] IdentityTransform = [Matrix3x4.Identity];

    /// <summary>Construct.</summary>
    public RenderMeshDrawable()
    {
    }

    /// <summary>
    /// Calculate distance and prepare batches for rendering. May be called from worker thread(s), possibly re-entrantly.
    /// </summary>
    public override void UpdateBatches(FrameInfo frame)
    {
        // Getting the world bounding box ensures the transforms are updated
        var worldBoundingBox = GetWorldBoundingBox();
        var worldTransform = Node.GetWorldTransform();
        var camera = frame.Camera!;
        SetDistance(camera.GetDistance(worldBoundingBox.Center));
        if (_batches.Count > 1)
        {
            for (var i = 0; i < _batches.Count; ++i)
            {
                var batch = _batches[i];
                batch.Distance = camera.GetDistance(worldTransform * _geometryData[i].Center);
                batch.WorldTransform = NumTransforms == 0 ? IdentityTransform : Transforms;
                batch.NumWorldTransforms = NumTransforms;
                _batches[i] = batch;
            }
        }
        else if (_batches.Count == 1)
        {
            var batch = _batches[0];
            batch.Distance = GetDistance();
            batch.WorldTransform = NumTransforms == 0 ? IdentityTransform : Transforms;
            batch.NumWorldTransforms = NumTransforms;
            _batches[0] = batch;
        }

        var scale = worldBoundingBox.Size.DotProduct(GraphicsDefs.DotScale);
        var newLodDistance = camera.GetLodDistance(GetDistance(), scale, LodBias);
        if (newLodDistance != GetLodDistance())
        {
            SetLodDistance(newLodDistance);
            CalculateLodLevels();
        }
    }

    public Matrix3x4[] Transforms = [];
    public uint NumTransforms;
}

#pragma warning disable CS0628
public sealed class RenderMeshInstance : RenderMesh.InstanceBase
{
    public override void Init(ParticleGraphNode node, ParticleGraphLayerInstance layer)
    {
        base.Init(node, layer);
        var graphNode = (RenderMesh)GetGraphNode();
        _sceneNode = new Node();
        _drawable = new RenderMeshDrawable();
        _sceneNode.AddComponent(_drawable, 0);
        _drawable.SetModelAttr(graphNode.Model);
        _drawable.SetMaterialsAttr(graphNode.Material);
        UpdateDrawableAttributes();
        OnSceneSet(GetScene());
    }

    public override void OnSceneSet(Scene? scene)
    {
        if (_octree is not null)
        {
            _octree.RemoveManualDrawable(_drawable);
            _octree = null;
        }
        if (scene is not null)
        {
            _octree = scene.GetOrCreateComponent<Octree>();
            _octree.AddManualDrawable(_drawable);
        }
    }

    public override void UpdateDrawableAttributes() => CopyDrawableAttributes(_drawable, GetEmitter());

    public override void Dispose()
    {
        OnSceneSet(null);
        _sceneNode?.Dispose();
        _drawable = null;
        _sceneNode = null;
        base.Dispose();
    }

    public override void Update(in UpdateContext context, int numParticles, SparseSpan<Matrix3x4> transforms)
    {
        var destination = Prepare(numParticles);
        var graphNode = (RenderMesh)GetGraphNode();
        if (graphNode.IsWorldspace)
        {
            for (var i = 0; i < numParticles; ++i) destination[i] = transforms[i];
        }
        else
        {
            var localToWorld = GetNode()!.GetWorldTransform();
            for (var i = 0; i < numParticles; ++i) destination[i] = localToWorld * transforms[i];
        }
    }

    public Span<Matrix3x4> Prepare(int numParticles)
    {
        if (_drawable!.Transforms.Length < numParticles)
        {
            var capacity = global::System.Math.Max(numParticles,
                _drawable.Transforms.Length == 0 ? 4 : _drawable.Transforms.Length * 2);
            Array.Resize(ref _drawable.Transforms, capacity);
        }
        _drawable.NumTransforms = checked((uint)numParticles);
        _sceneNode!.SetWorldTransform(GetNode()!.GetWorldTransform());
        // if (node_->material_ != drawable_->GetMaterial(0))
        //    drawable_->SetMaterial(node_->material_);
        return _drawable.Transforms.AsSpan(0, numParticles);
    }

    protected Node? _sceneNode;
    protected RenderMeshDrawable? _drawable;
    protected Octree? _octree;
}
#pragma warning restore CS0628
