// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Graphics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

/// <summary>2D renderer component and material cache.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class Renderer2D : Drawable
{
    private const VertexMask VertexMask2D = VertexMask.Position | VertexMask.Color | VertexMask.TexCoord1;

    private sealed class ViewBatchInfo : IDisposable
    {
        public readonly VertexBuffer VertexBuffer = new();
        public readonly List<Geometry> Geometries = [];
        public readonly List<Vertex2D> Vertices = [];

        public ViewBatchInfo() => VertexBuffer.SetShadowed(true);

        public void Dispose()
        {
            foreach (var geometry in Geometries)
                geometry.Dispose();
            Geometries.Clear();
            Vertices.Clear();
            VertexBuffer.Dispose();
        }
    }

    private sealed class SourceBatchComparer : IComparer<SourceBatch2D>
    {
        public static readonly SourceBatchComparer Instance = new();

        public int Compare(SourceBatch2D? left, SourceBatch2D? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            var result = left.DrawOrder.CompareTo(right.DrawOrder);
            if (result != 0)
                return result;
            result = right.Distance.CompareTo(left.Distance);
            if (result != 0)
                return result;
            result = (left.Material?.NameHash.Value ?? 0).CompareTo(right.Material?.NameHash.Value ?? 0);
            return result != 0 ? result : RuntimeHelpers.GetHashCode(left).CompareTo(RuntimeHelpers.GetHashCode(right));
        }
    }

    private readonly List<WeakReference<Drawable2D>> _drawables = [];
    private readonly Dictionary<(Texture2D Texture, BlendMode BlendMode), Material> _materials = [];
    private readonly Dictionary<BlendMode, Technique> _techniques = [];
    private readonly Dictionary<Camera, ViewBatchInfo> _viewBatchInfos = [];
    private readonly List<SourceBatch2D> _sourceBatches = [];
    private readonly Matrix3x4[] _identityWorldTransform = [Matrix3x4.Identity];
    private Frustum _frustum = new();
    private uint _viewMask = DefaultViewMask;

    public Renderer2D()
        : base(DrawableFlags.Geometry)
    {
        SubscribeToEvent<GraphicsEvents.BeginViewUpdateEvent>(HandleBeginViewUpdate);
    }

    public override void Dispose()
    {
        foreach (var batchInfo in _viewBatchInfos.Values)
            batchInfo.Dispose();
        foreach (var material in _materials.Values)
            material.Dispose();
        foreach (var technique in _techniques.Values)
            technique.Dispose();
        _viewBatchInfos.Clear();
        _materials.Clear();
        _techniques.Clear();
        _sourceBatches.Clear();
        _drawables.Clear();
        base.Dispose();
    }

    public void AddDrawable(Drawable2D drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        if (!_drawables.Any(item => item.TryGetTarget(out var value) && ReferenceEquals(value, drawable)))
            _drawables.Add(new WeakReference<Drawable2D>(drawable));
    }

    public void RemoveDrawable(Drawable2D drawable) =>
        _drawables.RemoveAll(item => !item.TryGetTarget(out var value) || ReferenceEquals(value, drawable));

    public Material GetMaterial(Texture2D texture, BlendMode blendMode)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var key = (texture, blendMode);
        if (_materials.TryGetValue(key, out var material))
            return material;
        if (!_techniques.TryGetValue(blendMode, out var technique))
        {
            technique = new Technique();
            var pass = technique.CreatePass("alpha");
            pass.SetVertexShader("Urho2D");
            pass.SetPixelShader("Urho2D");
            pass.SetDepthWrite(false);
            pass.SetBlendMode(blendMode);
            _techniques[blendMode] = technique;
        }
        material = new Material();
        material.SetTechnique(0, technique);
        material.SetCullMode(CullMode.None);
        material.SetName($"{texture.Name}_{blendMode}");
        material.SetTexture(ShaderResources.Albedo, texture);
        _materials[key] = material;
        return material;
    }

    public bool CheckVisibility(Drawable2D drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return drawable.IsEnabledEffective && (_viewMask & drawable.ViewMask) != 0 &&
            _frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside;
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        var resultStart = results.Count;
        foreach (var reference in _drawables)
        {
            if (reference.TryGetTarget(out var drawable) && (drawable.ViewMask & query.ViewMask) != 0)
                drawable.ProcessRayQuery(query, results);
        }
        if (results.Count == resultStart)
            return;
        results.Sort(resultStart, results.Count - resultStart, Comparer<RayQueryResult>.Create(static (left, right) =>
        {
            var leftDrawable = (Drawable2D)left.Drawable!;
            var rightDrawable = (Drawable2D)right.Drawable!;
            var result = rightDrawable.Layer.CompareTo(leftDrawable.Layer);
            if (result != 0)
                return result;
            result = rightDrawable.OrderInLayer.CompareTo(leftDrawable.OrderInLayer);
            return result != 0 ? result : rightDrawable.Id.CompareTo(leftDrawable.Id);
        }));
    }

    public override void UpdateBatches(FrameInfo frame)
    {
        SetDistance(frame.Camera!.GetDistance(GetWorldBoundingBox().Center));
        for (var index = 0; index < _batches.Count; ++index)
        {
            var batch = _batches[index];
            batch.Distance = 10.0f + (_batches.Count - index) * 0.001f;
            batch.WorldTransform = _identityWorldTransform;
            _batches[index] = batch;
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (previousScene is not null)
        {
            UnsubscribeFromEvent<SceneEvents.WorldOriginUpdateEvent>(previousScene);
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>(previousScene);
        }
        if (scene is not null)
        {
            SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
        }
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        _boundingBox = new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue);
        SetWorldBoundingBox(_boundingBox);
    }

    private void HandleBeginViewUpdate(ref GraphicsEvents.BeginViewUpdateEvent eventData)
    {
        if (!ReferenceEquals(Scene, eventData.Scene) || eventData.Camera is not { } camera ||
            eventData.RenderPipelineView is not { } view)
            return;

        var frame = view.GetFrameInfo();
        _frustum = camera.GetFrustum();
        _viewMask = camera.GetViewMask();
        _sourceBatches.Clear();
        _drawables.RemoveAll(static item => !item.TryGetTarget(out _));

        foreach (var reference in _drawables)
        {
            if (!reference.TryGetTarget(out var drawable) || !CheckVisibility(drawable))
                continue;
            drawable.MarkInView(frame);
            foreach (var sourceBatch in drawable.GetSourceBatches())
            {
                if (sourceBatch.Material is null || sourceBatch.Vertices.Count == 0)
                    continue;
                sourceBatch.Distance = camera.GetDistance(drawable.Node.GetWorldPosition());
                _sourceBatches.Add(sourceBatch);
            }
        }
        _sourceBatches.Sort(SourceBatchComparer.Instance);

        if (!_viewBatchInfos.TryGetValue(camera, out var batchInfo))
        {
            batchInfo = new ViewBatchInfo();
            _viewBatchInfos[camera] = batchInfo;
        }
        UpdateViewBatchInfo(batchInfo);
    }

    private void UpdateViewBatchInfo(ViewBatchInfo batchInfo)
    {
        batchInfo.Vertices.Clear();
        _batches.Clear();

        Material? currentMaterial = null;
        var vertexStart = 0;
        var vertexCount = 0;
        var distance = float.PositiveInfinity;
        var batchCount = 0;

        foreach (var sourceBatch in _sourceBatches)
        {
            if (!ReferenceEquals(currentMaterial, sourceBatch.Material))
            {
                if (currentMaterial is not null)
                    AddViewBatch(batchInfo, currentMaterial, vertexStart, vertexCount, distance, ref batchCount);
                vertexStart += vertexCount;
                vertexCount = 0;
                distance = float.PositiveInfinity;
                currentMaterial = sourceBatch.Material;
            }

            var worldTransform = sourceBatch.Owner!.Node.GetWorldTransform();
            foreach (var sourceVertex in sourceBatch.Vertices)
            {
                var vertex = sourceVertex;
                vertex.Position = worldTransform * vertex.Position;
                batchInfo.Vertices.Add(vertex);
            }
            vertexCount += sourceBatch.Vertices.Count;
            distance = MathF.Min(distance, sourceBatch.Distance);
        }
        if (currentMaterial is not null && vertexCount != 0)
            AddViewBatch(batchInfo, currentMaterial, vertexStart, vertexCount, distance, ref batchCount);

        UploadVertices(batchInfo);
        // Geometry may have been created before the dynamic buffer had GPU storage. Rebind it after upload so
        // the graphics backend observes the current non-headless buffer state.
        for (var index = 0; index < batchCount; ++index)
            batchInfo.Geometries[index].SetVertexBuffer(0, batchInfo.VertexBuffer);
    }

    private void AddViewBatch(ViewBatchInfo batchInfo, Material material, int vertexStart, int vertexCount,
        float distance, ref int batchCount)
    {
        if (vertexCount == 0)
            return;
        if (batchInfo.Geometries.Count == batchCount)
        {
            var geometry = new Geometry();
            geometry.SetVertexBuffer(0, batchInfo.VertexBuffer);
            batchInfo.Geometries.Add(geometry);
        }
        var batchGeometry = batchInfo.Geometries[batchCount++];
        batchGeometry.SetDrawRange(PrimitiveType.TriangleList, 0, 0, (uint)vertexStart, (uint)vertexCount);
        _batches.Add(new SourceBatch { Distance = distance, Material = material, Geometry = batchGeometry });
    }

    private static void UploadVertices(ViewBatchInfo batchInfo)
    {
        var vertexCount = (uint)batchInfo.Vertices.Count;
        if (batchInfo.VertexBuffer.GetVertexCount() < vertexCount || batchInfo.VertexBuffer.IsDataLost())
            batchInfo.VertexBuffer.SetSize(vertexCount, VertexMask2D, true);
        if (vertexCount == 0)
            return;

        var destination = batchInfo.VertexBuffer.Map();
        var offset = 0;
        foreach (var vertex in batchInfo.Vertices)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.Position);
            offset += 12;
            MemoryMarshal.Write(destination[offset..], in vertex.Color);
            offset += 4;
            MemoryMarshal.Write(destination[offset..], in vertex.Uv);
            offset += 8;
        }
        batchInfo.VertexBuffer.Unmap();
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        foreach (var reference in _drawables)
        {
            if (reference.TryGetTarget(out var drawable))
                drawable.UpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
        }
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        foreach (var reference in _drawables)
        {
            if (reference.TryGetTarget(out var drawable))
                drawable.PostUpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
        }
    }
}
