// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Root (builder) component for component-based CSG workflow.</summary>
/// <remarks>
/// <para>High-level behavior:</para>
/// <list type="bullet">
/// <item>Produces a combined mesh by evaluating CSG operations of all enabled <see cref="CsgBrush"/> components
/// under this node's subtree.</item>
/// <item>Ignores nested <see cref="CsgRoot"/> subtrees: traversal stops at the first nested root encountered.</item>
/// </list>
/// <para>Rebuild policy and threading:</para>
/// <list type="bullet">
/// <item>Rebuilds are async: the heavy work runs on <see cref="WorkQueue"/> worker thread(s) and the resulting
/// <see cref="Model"/> is applied on the main thread.</item>
/// <item>Rebuild requests are debounced using <see cref="DeferredDelaySec"/>. Any relevant change schedules a
/// rebuild for after the quiet period; setting delay to 0 triggers immediate rebuild.</item>
/// <item>Results are de-staled: if multiple rebuilds are requested while a build is in-flight, older results are
/// discarded and a fresh rebuild is scheduled.</item>
/// </list>
/// <para>Brush registration:</para>
/// <list type="bullet">
/// <item>Brushes register/unregister themselves with the nearest root (see <see cref="CsgBrush"/>).</item>
/// <item>The root listens to transform/dirty notifications from registered brush nodes.</item>
/// </list>
/// <para>Output ownership:</para>
/// <list type="bullet">
/// <item>The computed result is stored in a persistent <see cref="StaticModel"/> component on the same node.</item>
/// <item>Only the <see cref="Model"/> and material slots are overwritten; unrelated <see cref="StaticModel"/>
/// settings are preserved.</item>
/// </list>
/// </remarks>
[Reflected(ObjectCategory.Category_Scene, InheritAttributesOrder = 2)]
public partial class CsgRoot : CsgComponent
{
    private sealed class BrushStep
    {
        public List<CsgPolygon> Polygons = [];

        public Model? Model;
        public ResourceRefList Materials = new();
        public uint MeshId;
        public CsgOperation Operation;
        public WeakReference<CsgBrush>? Brush;
        public List<BrushStep> Children = [];
    }

    private sealed class BuildJob
    {
        public WeakReference<CsgRoot>? Root;
        public uint BuildId;
        public uint ComponentId;
        public float Epsilon;

        public Model? BaseModel;
        public ResourceRefList BaseMaterials = new();
        public BrushStep RootStep = new();

        public List<Model?> SourceModels = [];
        public List<ResourceRefList?> SourceMaterials = [];
    }

    private static Node? GetOrCreateTemporaryChild(Node? parent, string name)
    {
        if (parent is null)
            return null;

        var existing = parent.GetChild(new StringHash(name), false);
        if (existing is not null)
            return existing;

        var child = parent.CreateTemporaryChild(name);
        child.SetEnabled(false);
        return child;
    }

    private static void SetStaticModelMaterials(StaticModel? staticModel, IReadOnlyList<Material?> materials)
    {
        if (staticModel is null)
            return;

        var numGeometries = staticModel.GetNumGeometries();
        for (var i = 0u; i < numGeometries; ++i)
        {
            var material = i < materials.Count ? materials[(int)i] : null;
            staticModel.SetMaterial(i, material);
        }
    }

    private static object GetDeferredDelaySecAttributeDefault() => 0.1f;
    private static object GetEpsilonAttributeDefault() => CsgCommon.DefaultEpsilon;

    public CsgRoot()
    {
    }

    /// <summary>Rebuild immediately, ignoring scheduling policy.</summary>
    public void Rebuild()
    {
        var node = TryGetNode();
        if (node is null)
            return;

        if (_rebuildInFlight)
            return;

        var tempB = GetOrCreateTempStaticModel("Operand", ref _tempB);
        var tempBase = GetOrCreateTempStaticModel("Base", ref _tempBase);

        if (tempB is null || tempBase is null)
        {
            ApplyResultToOutput(null, new ResourceRefList());
            return;
        }

        // Base operand must be provided explicitly on CsgRoot.
        if (string.IsNullOrEmpty(GetModelAttr().Name))
        {
            Log.GetLogger().LogError("CsgRoot ({NodeName}:{NodeId}) has no <Model> set. CSG build is discarded.",
                node.Name, node.Id);
            ApplyResultToOutput(null, new ResourceRefList());
            return;
        }

        if (!SetupOperandStaticModel(tempBase, GetModelAttr(), GetMaterialsAttr()))
        {
            ApplyResultToOutput(null, new ResourceRefList());
            return;
        }

        var job = new BuildJob
        {
            Root = new WeakReference<CsgRoot>(this),
            BuildId = _rebuildRequestId,
            ComponentId = Id,
            Epsilon = _epsilon,
            BaseModel = tempBase.GetModel(),
            BaseMaterials = tempBase.GetMaterialsAttr().Clone(),
        };

        // Prepare source lookup tables for output vertex layout and materials.
        // MeshId 0 is reserved for the base operand.
        job.SourceModels.Clear();
        job.SourceMaterials.Clear();
        job.SourceModels.Add(job.BaseModel);
        job.SourceMaterials.Add(job.BaseMaterials.Clone());

        // Extract base polygons in CsgRoot local space on the main thread.
        job.RootStep.Model = job.BaseModel;
        job.RootStep.Materials = job.BaseMaterials.Clone();
        job.RootStep.MeshId = 0;
        job.RootStep.Polygons = Csg.BuildPolygonsFromModel(job.BaseModel, Matrix3x4.Identity, 0);

        // Build operation tree recursively starting from root node.
        var invRootWorld = node.GetWorldTransform().Inverse;
        if (!BuildOperationTree(job.RootStep, node, invRootWorld, tempB, job.SourceModels, job.SourceMaterials))
        {
            // No operations: return base model directly.
            var baseMaterials = tempBase.GetMaterialsAttr().Clone();
            ApplyResultToOutput(tempBase.GetModel(), baseMaterials);
            _rebuildDirty = false;
            return;
        }

        var workQueue = GetSubsystem<WorkQueue>();
        _rebuildInFlight = true;
        _rebuildInFlightId = job.BuildId;
        _rebuildDirty = false;

        workQueue.PostTask((threadIndex, queue) => ExecuteBuildJob(threadIndex, queue, job), TaskPriority.High);
    }

    /// <summary>Get or set debounce delay for rebuild scheduling. Values below 0 are clamped to 0.</summary>
    [Serialized("Deferred Delay Sec", DefaultSource = nameof(GetDeferredDelaySecAttributeDefault))]
    public float DeferredDelaySec
    {
        get => _deferredDelaySec;
        set => _deferredDelaySec = MathF.Max(0.0f, value);
    }

    /// <summary>Get or set epsilon used by polygon operations. Values below 0 are clamped to 0.</summary>
    [Serialized("Epsilon", DefaultSource = nameof(GetEpsilonAttributeDefault))]
    public float Epsilon
    {
        get => _epsilon;
        set
        {
            _epsilon = MathF.Max(0.0f, value);
            RequestRebuild();
        }
    }

    public override void ApplyAttributes()
    {
        base.ApplyAttributes();

        // Node::Load calls ApplyAttributes() after children/components exist.
        // Generated output model is not serialized as a resource reference, so we must rebuild after load.
        RequestRebuild();
    }

    protected void RequestRebuild()
    {
        _rebuildDirty = true;

        ++_rebuildRequestId;

        _lastChangeTimeMs = Time.GetSystemTime();

        ScheduleDeferredRebuild();
    }

    protected void CollectBrushesAndNestedRoots(Node? rootNode, List<CsgBrush> outBrushes,
        List<Node> outNestedRootNodes)
    {
        outBrushes.Clear();
        outNestedRootNodes.Clear();

        if (rootNode is null)
            return;

        var stack = new Stack<Node>();
        stack.Push(rootNode);

        while (stack.Count != 0)
        {
            var node = stack.Pop();

            if (!ReferenceEquals(node, rootNode))
            {
                var nestedRoot = node.GetComponent<CsgRoot>();
                if (nestedRoot is not null && !ReferenceEquals(nestedRoot, this))
                {
                    outNestedRootNodes.Add(node);
                    continue;
                }
            }

            var brush = node.GetComponent<CsgBrush>();
            if (brush is not null && brush.IsEnabledEffective)
                outBrushes.Add(brush);

            var numChildren = node.GetNumChildren();
            // Preserve child order deterministically for stable operand ordering.
            for (var i = numChildren; i-- > 0;)
            {
                var child = node.GetChild(i);
                if (child is not null && child.IsEnabled)
                    stack.Push(child);
            }
        }
    }

    protected void ApplyResultToOutput(Model? model, ResourceRefList materials)
    {
        var output = GetOrCreateOutputStaticModel();
        if (output is null)
            return;

        output.SetModel(model);

        output.SetMaterialsAttr(materials);
    }

    protected void RegisterBrush(CsgBrush? brush)
    {
        RegisterBrushInternal(brush, true);
    }

    protected void UnregisterBrush(CsgBrush? brush)
    {
        UnregisterBrushInternal(brush);
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        UpdateSubscriptions();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        UpdateSubscriptions();
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        UpdateSubscriptions();
    }

    protected override void OnMarkedDirty(Node node)
    {
        // Called by Node::MarkDirty when any listened node becomes dirty.
        // This is the most reliable way to catch editor-driven transform changes.
        if (!IsEnabledEffective)
            return;

        var time = GetSubsystem<Time>();
        var currentFrame = time.GetFrameNumber();

        // Rebuild is pointless when root node moves: it doesn't change inputs in root-local space,
        // it only moves the whole output together with the node.
        // Note: Node::MarkDirty marks children dirty too, so we must ignore the child notifications
        // caused by this same root dirty cascade.
        if (ReferenceEquals(node, TryGetNode()))
        {
            _ignoreChildDirtyFrame = currentFrame;
            return;
        }

        if (_ignoreChildDirtyFrame == currentFrame)
            return;

        _rebuildDirty = true;
        ++_rebuildRequestId;
        _lastChangeTimeMs = Time.GetSystemTime();

        ScheduleDeferredRebuild();
    }

    protected override ResourceRef GetDebugModelAttr()
    {
        return GetModelAttr();
    }

    protected override void OnModelChanged()
    {
        base.OnModelChanged();
        RequestRebuild();
    }

    protected override void OnMaterialsChanged()
    {
        base.OnMaterialsChanged();
        RequestRebuild();
    }

    private StaticModel? GetOrCreateOutputStaticModel()
    {
        var node = TryGetNode();
        if (node is null)
            return null;

        if (_output is not null && _output.TryGetTarget(out var output))
            return output;

        output = node.GetOrCreateComponent<StaticModel>();
        _output = new WeakReference<StaticModel>(output);
        return output;
    }

    private void UpdateSubscriptions()
    {
        var scene = Scene;

        var isActive = IsEnabledEffective && scene is not null;

        // Always detach any previously attached listeners.
        foreach (var weakNode in _listenedNodes)
        {
            if (weakNode.TryGetTarget(out var oldNode))
                oldNode.RemoveListener(this);
        }
        _listenedNodes.Clear();

        if (!isActive)
            return;

        PruneBrushes();

        // Rebuild listener set from registered brushes.
        _listenedNodes.EnsureCapacity(_brushes.Count + 1);

        // Listen to the root itself to detect parent/root transform cascades.
        var node = TryGetNode();
        if (node is not null)
        {
            node.AddListener(this);
            _listenedNodes.Add(new WeakReference<Node>(node));
        }

        foreach (var weakBrush in _brushes)
        {
            if (!weakBrush.TryGetTarget(out var brush) || !brush.IsEnabledEffective)
                continue;

            var brushNode = brush.TryGetNode();
            if (brushNode is null)
                continue;

            brushNode.AddListener(this);

            var alreadyTracked = false;
            foreach (var existing in _listenedNodes)
            {
                if (existing.TryGetTarget(out var existingNode) && ReferenceEquals(existingNode, brushNode))
                {
                    alreadyTracked = true;
                    break;
                }
            }
            if (!alreadyTracked)
                _listenedNodes.Add(new WeakReference<Node>(brushNode));
        }
    }

    private void RegisterBrushInternal(CsgBrush? brush, bool requestRebuild)
    {
        if (brush is null)
            return;

        PruneBrushes();

        foreach (var weakBrush in _brushes)
        {
            if (weakBrush.TryGetTarget(out var existing) && ReferenceEquals(existing, brush))
                return;
        }

        _brushes.Add(new WeakReference<CsgBrush>(brush));

        if (IsEnabledEffective && Scene is not null)
        {
            var brushNode = brush.TryGetNode();
            if (brushNode is not null)
            {
                brushNode.AddListener(this);

                var alreadyTracked = false;
                foreach (var existing in _listenedNodes)
                {
                    if (existing.TryGetTarget(out var existingNode) && ReferenceEquals(existingNode, brushNode))
                    {
                        alreadyTracked = true;
                        break;
                    }
                }
                if (!alreadyTracked)
                    _listenedNodes.Add(new WeakReference<Node>(brushNode));
            }
        }

        if (requestRebuild)
            RequestRebuild();
    }

    private void UnregisterBrushInternal(CsgBrush? brush)
    {
        if (brush is null)
            return;

        var node = TryGetNode();
        var brushNode = brush.TryGetNode();
        if (brushNode is not null && !ReferenceEquals(brushNode, node))
            brushNode.RemoveListener(this);

        for (var i = 0; i < _brushes.Count;)
        {
            if (!_brushes[i].TryGetTarget(out var existing) || ReferenceEquals(existing, brush))
                _brushes.RemoveAt(i);
            else
                ++i;
        }

        for (var i = 0; i < _listenedNodes.Count;)
        {
            if (!_listenedNodes[i].TryGetTarget(out var existing) || ReferenceEquals(existing, brushNode))
                _listenedNodes.RemoveAt(i);
            else
                ++i;
        }

        RequestRebuild();
    }

    private void PruneBrushes()
    {
        for (var i = 0; i < _brushes.Count;)
        {
            if (!_brushes[i].TryGetTarget(out _))
                _brushes.RemoveAt(i);
            else
                ++i;
        }

        for (var i = 0; i < _listenedNodes.Count;)
        {
            if (!_listenedNodes[i].TryGetTarget(out _))
                _listenedNodes.RemoveAt(i);
            else
                ++i;
        }
    }

    private void ScheduleDeferredRebuild()
    {
        if (!IsEnabledEffective || TryGetNode() is null)
            return;

        // If base model is not set, avoid scheduling rebuilds that can never succeed.
        if (string.IsNullOrEmpty(GetModelAttr().Name))
            return;

        var queue = Context.GetSubsystem<WorkQueue>();
        if (queue is null)
            return;

        if (_deferredDelaySec <= 0.0f)
        {
            Rebuild();
            return;
        }

        if (_deferredRebuildScheduled)
            return;

        _deferredRebuildScheduled = true;
        var scheduleToken = ++_deferredScheduleToken;
        var weak = new WeakReference<CsgRoot>(this);

        queue.PostDelayedTaskForMainThread((_, workQueue) =>
        {
            if (weak.TryGetTarget(out var root))
                root.DeferredRebuildTick(scheduleToken, workQueue);
        });
    }

    private void DeferredRebuildTick(uint scheduleToken, WorkQueue? queue)
    {
        if (scheduleToken != _deferredScheduleToken)
            return;

        if (!IsEnabledEffective || TryGetNode() is null)
        {
            _deferredRebuildScheduled = false;
            return;
        }

        if (!_rebuildDirty || string.IsNullOrEmpty(GetModelAttr().Name))
        {
            _deferredRebuildScheduled = false;
            return;
        }

        var nowMs = Time.GetSystemTime();
        var delayMs = checked((uint)(_deferredDelaySec * 1000.0f));
        if (nowMs - _lastChangeTimeMs >= delayMs)
        {
            _deferredRebuildScheduled = false;
            Rebuild();
            return;
        }

        if (queue is null)
        {
            _deferredRebuildScheduled = false;
            return;
        }

        var weak = new WeakReference<CsgRoot>(this);
        queue.PostDelayedTaskForMainThread((_, workQueue) =>
        {
            if (weak.TryGetTarget(out var root))
                root.DeferredRebuildTick(scheduleToken, workQueue);
        });
    }

    private StaticModel? GetOrCreateTempStaticModel(string name, ref WeakReference<StaticModel>? cache)
    {
        var node = TryGetNode();
        if (node is null)
            return null;

        Node? tempParent = null;
        if (_tempNode is not null)
            _tempNode.TryGetTarget(out tempParent);
        if (tempParent is null)
        {
            tempParent = node.CreateTemporaryChild("__CsgRootTemp");
            tempParent.SetEnabled(false);
            _tempNode = new WeakReference<Node>(tempParent);
        }

        var child = GetOrCreateTemporaryChild(tempParent, name);
        if (child is null)
            return null;

        StaticModel? staticModel = null;
        if (cache is not null)
            cache.TryGetTarget(out staticModel);
        if (staticModel is null)
        {
            staticModel = child.GetOrCreateComponent<StaticModel>();
            staticModel.SetTemporary(true);
            cache = new WeakReference<StaticModel>(staticModel);
        }

        return staticModel;
    }

    private bool SetupOperandStaticModel(StaticModel? staticModel, CsgBrush? brush)
    {
        if (staticModel is null || brush is null)
            return false;

        return SetupOperandStaticModel(staticModel, brush.GetModelAttr(), brush.GetMaterialsAttr());
    }

    private bool SetupOperandStaticModel(StaticModel? staticModel, ResourceRef modelAttr,
        ResourceRefList materialsAttr)
    {
        if (staticModel is null)
            return false;

        var modelName = modelAttr.Name;
        if (string.IsNullOrEmpty(modelName))
            return false;

        var cache = GetSubsystem<ResourceCache>();
        var model = cache.GetResourceOrNull<Model>(modelName);
        if (model is null)
            return false;

        staticModel.SetModel(model);

        // Material fallback policy:
        // If material list is empty, treat it as "use the model's default materials".
        // StaticModel::ApplyMaterialList() uses the model's resource name with extension .txt by default.
        if (materialsAttr.Names.Count == 0)
            staticModel.ApplyMaterialList();
        else
            staticModel.SetMaterialsAttr(materialsAttr);

        return true;
    }

    private bool BuildOperationTree(BrushStep rootStep, Node? node, Matrix3x4 invRootWorld, StaticModel tempModel,
        List<Model?> sourceModels, List<ResourceRefList?> sourceMaterials)
    {
        if (node is null)
            return false;

        var numChildren = node.GetNumChildren();
        for (var i = 0; i < numChildren; ++i)
        {
            var child = node.GetChild(i);
            if (child is null || !child.IsEnabled)
                continue;

            // Ignore nested CsgRoots (except this one).
            var nestedRoot = child.GetComponent<CsgRoot>();
            if (nestedRoot is not null && !ReferenceEquals(nestedRoot, this))
                continue;

            var brush = child.GetComponent<CsgBrush>();
            if (brush is not null)
            {
                if (!brush.IsEnabledEffective)
                    continue;

                // Ensure we listen to this brush even if it wasn't explicitly registered yet.
                RegisterBrushInternal(brush, false);

                if (!SetupOperandStaticModel(tempModel, brush))
                    continue;

                var step = new BrushStep
                {
                    Model = tempModel.GetModel(),
                    Materials = tempModel.GetMaterialsAttr().Clone(),
                    MeshId = checked((uint)sourceModels.Count),
                    Operation = brush.Operation,
                    Brush = new WeakReference<CsgBrush>(brush),
                };

                // Store source metadata for final model build on the main thread.
                sourceModels.Add(step.Model);
                sourceMaterials.Add(step.Materials.Clone());

                // Extract polygons in CsgRoot local space (main thread).
                var brushToRoot = invRootWorld * child.GetWorldTransform();
                step.Polygons = Csg.BuildPolygonsFromModel(step.Model, brushToRoot, step.MeshId);

                // Recurse into the brush subtree.
                BuildOperationTree(step, child, invRootWorld, tempModel, sourceModels, sourceMaterials);
                rootStep.Children.Add(step);
            }
            else
            {
                // Recurse into nodes without brushes.
                BuildOperationTree(rootStep, child, invRootWorld, tempModel, sourceModels, sourceMaterials);
            }
        }

        return rootStep.Children.Count != 0;
    }

    private static void ExecuteBuildJob(int threadIndex, WorkQueue queue, BuildJob job)
    {
        _ = threadIndex;

        var resultPolygons = new List<CsgPolygon>(job.RootStep.Polygons.Count);
        foreach (var polygon in job.RootStep.Polygons)
            resultPolygons.Add(polygon.Clone());

        var timer = new HiresTimer();
        EvaluateOperationTree(job.RootStep, ref resultPolygons, job.Epsilon);
        CsgTriangulatedModel? triangulated = null;
        if (resultPolygons.Count != 0)
            triangulated = Csg.TriangulatePolygons(resultPolygons, job.Epsilon);
        var elapsed = timer.GetUSec();
        Log.GetLogger().LogDebug("CsgRoot({ComponentId}): rebuild took {Milliseconds}.{Microseconds}ms",
            job.ComponentId, elapsed / 1000, elapsed % 1000);

        queue.PostDelayedTaskForMainThread(() => ApplyBuildJobResultOnMainThread(job, triangulated));
    }

    private static void ApplyBuildJobResultOnMainThread(BuildJob job, CsgTriangulatedModel? triangulated)
    {
        if (job.Root is null || !job.Root.TryGetTarget(out var root))
            return;

        if (root._rebuildInFlightId != job.BuildId)
            return;

        root._rebuildInFlight = false;

        // Drop stale results.
        if (root._rebuildRequestId != job.BuildId)
        {
            if (root._rebuildDirty)
                root.Rebuild();
            return;
        }

        var materials = new ResourceRefList();
        Model renderableModel;
        if (triangulated is null)
        {
            // Triangulation failed or resulted in empty output.
            renderableModel = new Model();
        }
        else
        {
            ResourceRefList? outputMaterials = materials;
            renderableModel = Csg.BuildModel(triangulated, job.SourceModels, job.SourceMaterials, ref outputMaterials);
            materials = outputMaterials!.Value;
        }

        root.ApplyResultToOutput(renderableModel, materials);

        // Acknowledge changes.
        AcknowledgeBrushes(job.RootStep);
    }

    private static List<CsgPolygon> EvaluateBooleanPolygons(List<CsgPolygon> a, List<CsgPolygon> b,
        CsgOperation operation, float epsilon)
    {
        // Handle empty operands explicitly to avoid treating "empty BSP" as a hard failure.
        if (a.Count == 0 && b.Count == 0)
            return [];

        if (a.Count == 0)
        {
            switch (operation)
            {
                case CsgOperation.Union:
                    return b;
                case CsgOperation.DifferenceAB:
                    return [];
                case CsgOperation.Intersection:
                    return [];
            }
        }

        if (b.Count == 0)
        {
            switch (operation)
            {
                case CsgOperation.Union:
                    return a;
                case CsgOperation.DifferenceAB:
                    return a;
                case CsgOperation.Intersection:
                    return [];
            }
        }

        var bspA = new CsgBsp();
        bspA.Build(a, epsilon);
        var bspB = new CsgBsp();
        bspB.Build(b, epsilon);
        if (!bspA.IsBuilt || !bspB.IsBuilt)
            return [];

        return Csg.BooleanOperation(bspA, bspB, operation, epsilon);
    }

    private static void EvaluateOperationTree(BrushStep step, ref List<CsgPolygon> currentPolygons, float epsilon)
    {
        foreach (var child in step.Children)
        {
            var childPolygons = new List<CsgPolygon>(child.Polygons.Count);
            foreach (var polygon in child.Polygons)
                childPolygons.Add(polygon.Clone());
            if (child.Children.Count != 0)
                EvaluateOperationTree(child, ref childPolygons, epsilon);

            currentPolygons = EvaluateBooleanPolygons(currentPolygons, childPolygons, child.Operation, epsilon);
        }
    }

    private static void AcknowledgeBrushes(BrushStep step)
    {
        foreach (var child in step.Children)
        {
            if (child.Brush is not null && child.Brush.TryGetTarget(out var brush))
                brush.ClearDirty();

            AcknowledgeBrushes(child);
        }
    }

    internal void RequestRebuildForCsgBrush()
    {
        RequestRebuild();
    }

    internal void RegisterBrushForCsgBrush(CsgBrush brush)
    {
        RegisterBrush(brush);
    }

    internal void UnregisterBrushForCsgBrush(CsgBrush brush)
    {
        UnregisterBrush(brush);
    }

    private float _deferredDelaySec = 0.1f;
    private float _epsilon = CsgCommon.DefaultEpsilon;
    private bool _rebuildDirty = true;
    private uint _lastChangeTimeMs;
    private uint _deferredScheduleToken;
    private bool _deferredRebuildScheduled;
    private uint _rebuildRequestId = 1;
    private uint _rebuildInFlightId;
    private bool _rebuildInFlight;

    private WeakReference<StaticModel>? _output;
    private WeakReference<Node>? _tempNode;
    private WeakReference<StaticModel>? _tempB;
    private WeakReference<StaticModel>? _tempBase;
    private readonly List<WeakReference<Node>> _listenedNodes = [];
    private readonly List<WeakReference<CsgBrush>> _brushes = [];

    private uint _ignoreChildDirtyFrame;
}
