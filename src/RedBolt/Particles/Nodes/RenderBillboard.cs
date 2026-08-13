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
public partial class RenderBillboard :
    TemplateNode<RenderBillboardInstance, Vector3f, Vector2f, float, Color, float, Vector3f>
{
    /// <summary>Construct RenderBillboard.</summary>
    public RenderBillboard() : base([
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "pos", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "size", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "frame", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "color", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "rotation", ParticleGraphContainerType.Auto),
        new ParticleGraphPin(ParticleGraphPinFlag.Input, "direction", ParticleGraphContainerType.Auto)])
    {
        Material = new ResourceRef();
        _crop = Rect.Positive;
    }

    public override int EvaluateInstanceSize() => IntPtr.Size;
    public override ParticleGraphNodeInstance CreateInstance(ParticleGraphLayerInstance layer)
    { var instance = new RenderBillboardInstance(); instance.Init(this, layer); return instance; }

    private static object? GetMaterialDefault() => new ResourceRef();
    private static object? GetCropDefault() => Rect.Positive;

    [Serialized("Material", DefaultSource = nameof(GetMaterialDefault))] public ResourceRef Material { get; set; }
    [Serialized("Rows")] public int Rows { get; set; }
    [Serialized("Columns")] public int Columns { get; set; }
    [Serialized("Face Camera Mode")] public int FaceCameraMode { get; set; }
    [Serialized("Sort By Distance")] public bool SortByDistance { get; set; }
    [Serialized("Is Worldspace")] public bool IsWorldspace { get; set; }
    [Serialized("Crop", DefaultSource = nameof(GetCropDefault))]
    public Rect Crop { get => _crop; set => _crop = value; }

    private Rect _crop;
}

#pragma warning disable CS0628
public sealed class RenderBillboardInstance : RenderBillboard.InstanceBase
{
    public override void Init(ParticleGraphNode node, ParticleGraphLayerInstance layer)
    {
        base.Init(node, layer);
        var renderBillboard = (RenderBillboard)GetGraphNode();
        _sceneNode = new Node();
        _billboardSet = _sceneNode.CreateComponent<BillboardSet>();
        _billboardSet.SetMaterialAttr(renderBillboard.Material);
        _billboardSet.SetFaceCameraMode((FaceCameraMode)renderBillboard.FaceCameraMode);
        _billboardSet.SetSorted(renderBillboard.SortByDistance);
        UpdateDrawableAttributes();
        OnSceneSet(GetScene());
    }

    public override void OnSceneSet(Scene? scene)
    {
        if (_octree is not null)
        {
            _octree.RemoveManualDrawable(_billboardSet);
            _octree = null;
        }
        if (scene is not null)
        {
            _octree = scene.GetOrCreateComponent<Octree>();
            _octree.AddManualDrawable(_billboardSet);
        }
    }

    public override void UpdateDrawableAttributes() => CopyDrawableAttributes(_billboardSet, GetEmitter());

    public override void Dispose()
    {
        OnSceneSet(null);
        _sceneNode?.Dispose();
        _billboardSet = null;
        _sceneNode = null;
        base.Dispose();
    }

    public override void Update(in UpdateContext context, int numParticles, SparseSpan<Vector3f> position,
        SparseSpan<Vector2f> size, SparseSpan<float> frame, SparseSpan<Color> color,
        SparseSpan<float> rotation, SparseSpan<Vector3f> direction)
    {
        Prepare(numParticles);
        for (var i = 0; i < numParticles; ++i)
            UpdateParticle(i, position[i], size[i], frame[i], color[i], rotation[i], direction[i]);
        Commit();
    }

    public void Prepare(int numParticles)
    {
        var renderBillboard = (RenderBillboard)GetGraphNode();
        if (!renderBillboard.IsWorldspace)
            _sceneNode!.SetWorldTransform(GetNode()!.GetWorldTransform());
        var numBillboards = _billboardSet!.GetNumBillboards();
        //if (numBillboards < numParticles)
        {
            _billboardSet.SetNumBillboards(checked((uint)numParticles));
            numBillboards = checked((uint)numParticles);
        }
        var billboards = _billboardSet.GetBillboards();
        for (var i = numParticles; i < numBillboards; ++i)
        {
            var billboard = billboards[(int)i];
            billboard.Enabled = false;
            billboards[(int)i] = billboard;
        }
        _columns = checked((uint)global::System.Math.Max(1, renderBillboard.Columns));
        _rows = checked((uint)global::System.Math.Max(1, renderBillboard.Rows));
        var crop = renderBillboard.Crop;
        _cropSize = crop.Size;
        _cropOffset = crop.Min;
        _uvTileSize = new Vector2f(1.0f / _columns, 1.0f / _rows);
    }

    public void UpdateParticle(int index, in Vector3f position, in Vector2f size, float frameIndex,
        in Color color, float rotation, in Vector3f direction)
    {
        ref var billboard = ref _billboardSet!.GetBillboard(checked((uint)index));
        billboard.Enabled = true;
        billboard.Position = position;
        billboard.Size = size * _cropSize;
        billboard.Color = color;
        billboard.Rotation = rotation;
        billboard.Direction = direction;
        var frame = (int)frameIndex;
        var frameAsUnsigned = unchecked((uint)frame);
        var x = frameAsUnsigned % _columns;
        var y = frameAsUnsigned / _columns;
        var uvMin = (new Vector2f(x, y) + _cropOffset) * _uvTileSize;
        var uvMax = uvMin + _uvTileSize * _cropSize;
        billboard.Uv = new Rect(uvMin, uvMax);
    }

    public void Commit() => _billboardSet!.Commit();

    protected Node? _sceneNode;
    protected BillboardSet? _billboardSet;
    protected Octree? _octree;
    protected uint _columns;
    protected uint _rows;
    protected Vector2f _uvTileSize;
    protected Vector2f _cropOffset;
    protected Vector2f _cropSize;
}
#pragma warning restore CS0628
