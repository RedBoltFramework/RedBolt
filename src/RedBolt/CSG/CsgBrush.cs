// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using System;

using RedBolt.Core;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Operand component for component-based CSG workflow.</summary>
/// <remarks>
/// <para>High-level behavior:</para>
/// <list type="bullet">
/// <item>Represents one operand in a <see cref="CsgRoot"/> boolean operation tree.</item>
/// <item>The operand geometry is taken from the base <see cref="CsgComponent"/> attributes
/// (<c>Model</c> + <c>Materials</c>).</item>
/// <item>The boolean operation to apply against the accumulated result is controlled by
/// <see cref="Operation"/>.</item>
/// </list>
/// <para>Root association:</para>
/// <list type="bullet">
/// <item>A brush registers itself with the nearest containing <see cref="CsgRoot"/> (walking up parent nodes).</item>
/// <item>The association is updated when the brush is moved between nodes/scenes or enabled/disabled.</item>
/// </list>
/// <para>Dirty semantics:</para>
/// <list type="bullet">
/// <item>A brush becomes dirty when its operation/model/materials/state changes.</item>
/// <item>Dirtiness is acknowledged by the root after a successful rebuild.</item>
/// </list>
/// </remarks>
[Reflected(ObjectCategory.Category_Scene, InheritAttributesOrder = 1)]
public partial class CsgBrush : CsgComponent
{
    public CsgBrush()
    {
    }

    public override void Dispose()
    {
        if (_root is not null && _root.TryGetTarget(out var root))
            root.UnregisterBrushForCsgBrush(this);
        _root = null;
        base.Dispose();
    }

    /// <summary>Get or set boolean operation used when combining this brush with the current CSG result.</summary>
    [Serialized("Operation")]
    public CsgOperation Operation
    {
        get => _operation;
        set => SetOperation(value);
    }

    /// <summary>Whether brush state has changed since last acknowledged by a root.</summary>
    public bool IsDirty => _dirty;

    /// <summary>Acknowledge brush state change.</summary>
    public void ClearDirty()
    {
        _dirty = false;
    }

    protected override ResourceRef GetDebugModelAttr()
    {
        return GetModelAttr();
    }

    public override void ApplyAttributes()
    {
        base.ApplyAttributes();
        MarkDirty();
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        UpdateRootRegistration();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        UpdateRootRegistration();
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        UpdateRootRegistration();
    }

    protected override void OnModelChanged()
    {
        base.OnModelChanged();
        MarkDirty();
    }

    protected override void OnMaterialsChanged()
    {
        base.OnMaterialsChanged();
        MarkDirty();
    }

    private void SetOperation(CsgOperation operation)
    {
        if (_operation == operation)
            return;

        _operation = operation;
        MarkDirty();
    }

    private void MarkDirty()
    {
        _dirty = true;

        UpdateRootRegistration();
        if (_root is not null && _root.TryGetTarget(out var root))
            root.RequestRebuildForCsgBrush();
    }

    private CsgRoot? FindContainingRoot()
    {
        var current = TryGetNode();
        while (current is not null)
        {
            var root = current.GetComponent<CsgRoot>();
            if (root is not null)
                return root;
            current = current.Parent;
        }
        return null;
    }

    private void UpdateRootRegistration()
    {
        CsgRoot? newRoot = null;
        if (IsEnabledEffective && TryGetNode() is not null)
            newRoot = FindContainingRoot();

        var oldRoot = _root is not null && _root.TryGetTarget(out var root) ? root : null;
        if (ReferenceEquals(newRoot, oldRoot))
            return;

        oldRoot?.UnregisterBrushForCsgBrush(this);

        _root = newRoot is not null ? new WeakReference<CsgRoot>(newRoot) : null;

        newRoot?.RegisterBrushForCsgBrush(this);
    }

    private CsgOperation _operation = CsgOperation.Union;
    private WeakReference<CsgRoot>? _root;
    private bool _dirty;
}
