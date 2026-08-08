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

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Intermediate class for request processing.</summary>
public class PrefabFromNodeFactory : BaseResourceFactory
{
    public PrefabFromNodeFactory()
        : base(0, "Prefab from Node")
    {
        using var prefabBase = CreatePrefabBase();
        _prefab.GetMutableScenePrefab().CopyFrom(prefabBase);
    }

    public override void Dispose()
    {
        _tab = null;
        _nodes.Clear();
        _prefab.Dispose();
        base.Dispose();
    }

    public void Setup(SceneViewTab tab, IEnumerable<WeakReference<Node>> nodes)
    {
        _tab = new WeakReference<SceneViewTab>(tab);
        _nodes.Clear();
        _nodes.AddRange(nodes);
    }

    /// <summary>Implement ResourceFactory.</summary>
    public override string DefaultFileName
    {
        get
        {
            if (_nodes.Count == 0)
                return "(none)";
            if (_nodes.Count > 1)
                return "(automatic)";

            _nodes[0].TryGetTarget(out var node);
            var name = node!.Name;
            return name.Length == 0 ? "Prefab.prefab" : $"{name}.prefab";
        }
    }

    public override bool IsFileNameEditable => _nodes.Count == 1;

    public override void Render(
        ResourceFactory.FileNameChecker checker,
        ref bool canCommit,
        ref bool shouldCommit)
    {
        base.Render(checker, ref canCommit, ref shouldCommit);

        if (_nodes.Count > 1)
            canCommit = true;
        if (!IsFileNameEditable && IsKeyPressed(Key.Return))
            shouldCommit = true;
    }

    public override void RenderAuxilary()
    {
        ImGui.Checkbox("Replace with PrefabReference"u8, ref _replaceWithReference);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Replace node contents with PrefabReference component that references created prefab."u8);
        }
    }

    public override void CommitAndClose()
    {
        base.CommitAndClose();

        if (_nodes.Count == 1 && _nodes[0].TryGetTarget(out var node))
        {
            SaveNodeAsPrefab(node, FinalResourceName, FinalFileName);
        }
        else
        {
            var filePath = FinalFilePath;
            foreach (var nodeReference in _nodes)
            {
                if (!nodeReference.TryGetTarget(out node))
                    continue;

                var fileName = FindBestFileName(node, filePath);
                if (fileName.Length != 0)
                {
                    var resourceName = FinalResourcePath + fileName[filePath.Length..];
                    SaveNodeAsPrefab(node, resourceName, fileName);
                }
            }
        }
    }

    private NodePrefab CreatePrefabBase()
    {
        var cache = GetSubsystem<ResourceCache>();
        using var scene = new Scene();

        scene.CreateComponent<Octree>();

        scene.CreateChild();

        var skyboxNode = scene.CreateChild("Default Skybox");
        var skybox = skyboxNode.CreateComponent<Skybox>();
        skybox.SetModel(cache.GetResource<Model>("Models/Box.mdl"));
        skybox.SetMaterial(cache.GetResource<Material>("Materials/DefaultSkybox.xml"));

        var zoneNode = scene.CreateChild("Default Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(Color.Black);
        zone.SetBackgroundBrightness(1.0f);
        zone.SetZoneTexture(
            cache.GetResource<TextureCube>("Textures/DefaultSkybox.xml"));

        return scene.GeneratePrefab();
    }

    private static NodePrefab CreatePrefabFromNode(Node node)
    {
        var result = node.GeneratePrefab();

        // Discard enabled flag, position, rotation and name of the root node.
        // Keep the scale and the rest.
        var nodeAttributes = result.Node.Attributes;
        var enabledHash = new StringHash("Is Enabled");
        var positionHash = new StringHash("Position");
        var rotationHash = new StringHash("Rotation");
        var nameHash = new StringHash("Name");
        for (var index = nodeAttributes.Count - 1; index >= 0; --index)
        {
            var attribute = nodeAttributes[index];
            if (attribute.NameHash == enabledHash
                || attribute.NameHash == positionHash
                || attribute.NameHash == rotationHash
                || attribute.NameHash == nameHash)
            {
                attribute.Dispose();
                nodeAttributes.RemoveAt(index);
            }
        }

        return result;
    }

    private static string FindBestFileName(Node node, string filePath)
    {
        static string? GetAvailableFileName(string path, string prefabName)
        {
            var fileName = path + prefabName + ".prefab";
            return global::System.IO.File.Exists(fileName) || Directory.Exists(fileName)
                ? null
                : fileName;
        }

        var prefabName = GetSanitizedName(node.Name).Trim();
        if (prefabName.Length == 0)
            prefabName = "Prefab";

        if (GetAvailableFileName(filePath, prefabName) is { } fileName)
            return fileName;

        const int maxAttempts = 100;
        for (var i = 1; i < maxAttempts; ++i)
        {
            if (GetAvailableFileName(filePath, $"{prefabName}_{i}")
                is { } indexedFileName)
            {
                return indexedFileName;
            }
        }

        Log.GetLogger().LogError("Cannot find available file name for prefab");
        return string.Empty;
    }

    private void SaveNodeAsPrefab(
        Node node, string resourceName, string fileName)
    {
        var nodePrefab = _prefab.GetMutableNodePrefab();
        using (var sourcePrefab = CreatePrefabFromNode(node))
            nodePrefab.CopyFrom(sourcePrefab);
        _prefab.NormalizeIds();
        _prefab.SaveFile(new FileIdentifier(fileName));

        if (_replaceWithReference
            && _tab is not null
            && _tab.TryGetTarget(out var tab)
            && !ReferenceEquals(node, node.Scene))
        {
            var scene = node.Scene!;
            var parentNode = node.Parent!;
            var nodeId = node.Id;
            var indexInParent = node.GetIndexInParent();
            var scopeHint = node.GetEffectiveScopeHint();

            using var nodeAttributes = new SerializablePrefab();
            nodeAttributes.Import(node);

            {
                var builder = new RemoveNodeActionBuilder(node);

                node.Remove();

                tab.PushAction(builder.Build()!);
            }

            {
                var builder = new CreateNodeActionBuilder(scene, scopeHint);

                var newNode = parentNode.CreateChild(nodeId);
                parentNode.ReorderChild(newNode, indexInParent);

                nodeAttributes.Export(newNode);

                var prefabReference = newNode.CreateComponent<PrefabReference>();
                prefabReference.SetPrefabAttr(new ResourceRef(
                    ObjectReflection<PrefabResource>.TypeId, resourceName));
                prefabReference.ApplyAttributes();

                tab.PushAction(builder.Build(newNode)!);
            }
        }
    }

    private static string GetSanitizedName(string value)
    {
        const string forbiddenCharacters = "<>:\"/\\|?*";
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; ++index)
        {
            var character = characters[index];
            if (character < 31 || forbiddenCharacters.Contains(character))
                characters[index] = character < 31 ? ' ' : '_';
        }
        return new string(characters);
    }

    private static bool IsKeyPressed(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyPressed(imguiKey);
    }

    private WeakReference<SceneViewTab>? _tab;
    private readonly List<WeakReference<Node>> _nodes = [];
    private readonly PrefabResource _prefab = new();
    private bool _replaceWithReference = true;
}

/// <summary>Add-on to manage scene selection with mouse and render debug geometry.</summary>
public class CreatePrefabFromNode : SceneViewAddon
{
    public CreatePrefabFromNode(SceneViewTab owner)
        : base(owner)
    {
        owner.OnSelectionEditMenu += RenderMenu;
    }

    public override void Dispose()
    {
        if (_owner.TryGetTarget(out var owner))
            owner.OnSelectionEditMenu -= RenderMenu;
        _factory.Dispose();
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "CreatePrefabFrom";

    private void RenderMenu(
        SceneViewPage page, Scene scene, SceneSelection selection)
    {
        _ = page;
        _ = scene;
        var hasNodesSelected = selection.Nodes.Count != 0;
        if (ImGui.MenuItem(
                "Create Prefab"u8, ReadOnlySpan<byte>.Empty,
                false, hasNodesSelected))
        {
            CreatePrefabs(selection);
        }
    }

    private void CreatePrefabs(SceneSelection selection)
    {
        var nodes = new List<WeakReference<Node>?>(selection.Nodes);
        for (var index = 0; index < nodes.Count; ++index)
        {
            if (nodes[index] is not { } nodeReference
                || !nodeReference.TryGetTarget(out var node))
            {
                nodes[index] = null;
                continue;
            }

            foreach (var otherReference in nodes)
            {
                if (otherReference is not null
                    && otherReference.TryGetTarget(out var otherNode)
                    && node.IsChildOf(otherNode))
                {
                    nodes[index] = null;
                    break;
                }
            }
        }
        nodes.RemoveAll(static node => node is null);

        _owner.TryGetTarget(out var owner);
        _factory.Setup(owner!, nodes!);

        var project = owner!.GetProject();
        var request = new CreateResourceRequest(_factory);
        project.ProcessRequest(request, owner);
    }

    private readonly PrefabFromNodeFactory _factory = new();
}
