//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;
using RedBolt.Utility;

file static class CommonEditorActionBuilderHelpers
{
    public static AttributeScopeHint GetScopeHint(Context context, StringHash componentType)
    {
        var reflection = context.GetReflection(componentType);
        return reflection is not null ? reflection.GetEffectiveScopeHint() : AttributeScopeHint.Serializable;
    }
}

/// <summary>
/// Helper factory classes to create actions.
/// These classes should be created before the action is started, and cooked after the action is finished.
/// </summary>
public class CreateNodeActionBuilder
{
    public CreateNodeActionBuilder(Scene? scene, AttributeScopeHint scopeHint)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _scopeHint = scopeHint;

        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            case AttributeScopeHint.Node:
            {
                // No need to prepare
                break;
            }
            case AttributeScopeHint.Scene:
            {
                _oldSceneData = PackedSceneData.FromScene(scene!);
                break;
            }
        }
    }

    public EditorAction? Build(Node? node)
    {
        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            case AttributeScopeHint.Node:
            {
                return new CreateRemoveNodeAction(node!, false);
            }
            case AttributeScopeHint.Scene:
            {
                var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
                return new ChangeSceneAction(scene, _oldSceneData);
            }
            default: return null;
        }
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly AttributeScopeHint _scopeHint;

    private PackedSceneData _oldSceneData = new();
}

public class RemoveNodeActionBuilder
{
    public RemoveNodeActionBuilder(Node? node)
    {
        var scene = node!.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _scopeHint = node.GetEffectiveScopeHint();

        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            case AttributeScopeHint.Node:
            {
                _action = new CreateRemoveNodeAction(node, true);
                break;
            }
            case AttributeScopeHint.Scene:
            {
                _oldSceneData = PackedSceneData.FromScene(scene!);
                break;
            }
        }
    }

    public EditorAction? Build()
    {
        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            case AttributeScopeHint.Node:
            {
                return _action;
            }
            case AttributeScopeHint.Scene:
            {
                var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
                return new ChangeSceneAction(scene, _oldSceneData);
            }
            default: return null;
        }
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly AttributeScopeHint _scopeHint;

    private EditorAction? _action;
    private PackedSceneData _oldSceneData = new();
}

public class CreateComponentActionBuilder
{
    public CreateComponentActionBuilder(Node? node, StringHash componentType)
    {
        var scene = node!.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _scopeHint = CommonEditorActionBuilderHelpers.GetScopeHint(scene!.Context, componentType);

        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            {
                // No need to prepare
                break;
            }
            case AttributeScopeHint.Node:
            {
                _oldNodeData = new PackedNodeData(node);
                break;
            }
            case AttributeScopeHint.Scene:
            {
                _oldSceneData = PackedSceneData.FromScene(scene);
                break;
            }
        }
    }

    public EditorAction? Build(Component? component)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        Debug.Assert(_scopeHint == CommonEditorActionBuilderHelpers.GetScopeHint(
            scene!.Context, component!.GetTypeNameHash()));

        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            {
                return new CreateRemoveComponentAction(component, false);
            }
            case AttributeScopeHint.Node:
            {
                var node = component.Node;
                return new ChangeNodeSubtreeAction(scene, _oldNodeData, node);
            }
            case AttributeScopeHint.Scene:
            {
                return new ChangeSceneAction(scene, _oldSceneData);
            }
            default: return null;
        }
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly AttributeScopeHint _scopeHint;

    private PackedNodeData _oldNodeData = new();
    private PackedSceneData _oldSceneData = new();
}

public class RemoveComponentActionBuilder
{
    public RemoveComponentActionBuilder(Component? component)
    {
        var scene = component!.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        var node = component.TryGetNode();
        _node = node is not null ? new WeakReference<Node>(node) : null;
        _scopeHint = CommonEditorActionBuilderHelpers.GetScopeHint(
            scene!.Context, component.GetTypeNameHash());

        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            {
                _action = new CreateRemoveComponentAction(component, true);
                break;
            }
            case AttributeScopeHint.Node:
            {
                _oldNodeData = new PackedNodeData(node!);
                break;
            }
            case AttributeScopeHint.Scene:
            {
                _oldSceneData = PackedSceneData.FromScene(scene);
                break;
            }
        }
    }

    public EditorAction? Build()
    {
        switch (_scopeHint)
        {
            case AttributeScopeHint.Attribute:
            case AttributeScopeHint.Serializable:
            {
                return _action;
            }
            case AttributeScopeHint.Node:
            {
                var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
                var node = _node is not null && _node.TryGetTarget(out var nodeTarget) ? nodeTarget : null;
                return new ChangeNodeSubtreeAction(scene, _oldNodeData, node);
            }
            case AttributeScopeHint.Scene:
            {
                var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
                return new ChangeSceneAction(scene, _oldSceneData);
            }
            default: return null;
        }
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly WeakReference<Node>? _node;
    private readonly AttributeScopeHint _scopeHint;

    private EditorAction? _action;
    private PackedNodeData _oldNodeData = new();
    private PackedSceneData _oldSceneData = new();
}

public class ChangeAttributeBuffer : ICloneable, IDisposable
{
    public VariantVector OldValues;
    public VariantVector NewValues;

    public List<PackedComponentData> OldComponents;
    public List<PackedComponentData> NewComponents;

    public List<PackedNodeData> OldNodes;
    public List<PackedNodeData> NewNodes;

    public PackedSceneData OldScene;
    public PackedSceneData NewScene;

    public ChangeAttributeBuffer()
    {
        OldValues = new VariantVector();
        NewValues = new VariantVector();
        OldComponents = [];
        NewComponents = [];
        OldNodes = [];
        NewNodes = [];
        OldScene = new PackedSceneData();
        NewScene = new PackedSceneData();
    }

    private ChangeAttributeBuffer(ChangeAttributeBuffer rhs)
    {
        OldValues = new VariantVector(rhs.OldValues);
        NewValues = new VariantVector(rhs.NewValues);
        OldComponents = [];
        foreach (var component in rhs.OldComponents)
            OldComponents.Add(component.Clone());
        NewComponents = [];
        foreach (var component in rhs.NewComponents)
            NewComponents.Add(component.Clone());
        OldNodes = [];
        foreach (var node in rhs.OldNodes)
            OldNodes.Add(node.Clone());
        NewNodes = [];
        foreach (var node in rhs.NewNodes)
            NewNodes.Add(node.Clone());
        OldScene = rhs.OldScene.Clone();
        NewScene = rhs.NewScene.Clone();
    }

    public ChangeAttributeBuffer Clone() => new(this);

    object ICloneable.Clone() => Clone();

    public void Dispose()
    {
        NewScene = new PackedSceneData();
        OldScene = new PackedSceneData();
        NewNodes.Clear();
        OldNodes.Clear();
        NewComponents.Clear();
        OldComponents.Clear();
        NewValues.Dispose();
        OldValues.Dispose();
    }
}
