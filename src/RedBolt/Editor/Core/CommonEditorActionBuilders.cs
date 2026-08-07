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

using RedBolt.Core;
using RedBolt.Scene;
using RedBolt.Utility;

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
