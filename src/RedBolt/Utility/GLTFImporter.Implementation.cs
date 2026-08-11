// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Logging;
using RedBolt.Math;

internal static partial class GLTFImporterFunctions
{
    public static void ValidateExtensions(Tg.Model model)
    {
        HashSet<string> supportedExtensions = ["KHR_materials_unlit"];
        foreach (var extension in model.ExtensionsUsed)
        {
            if (!supportedExtensions.Contains(extension.ToString()))
                Log.GetLogger().Warning("Unsupported extension used: '{}'", extension);
        }
    }

    public static Tg.Model LoadGLTF(string fileName)
    {
        using var loader = new Tg.TinyGLTF();
        loader.SetImageLoader(GLTFTextureImporter.LoadImageData, IntPtr.Zero);
        using var errorMessage = new Std.String();
        using var warningMessage = new Std.String();
        using var nativeFileName = new Std.String(fileName);
        var model = new Tg.Model();
        if (fileName.EndsWith(".gltf", StringComparison.Ordinal))
        {
            if (!loader.LoadASCIIFromFile(model, errorMessage, warningMessage, nativeFileName))
                throw new InvalidOperationException($"Failed to import GLTF file '{fileName}' due to error: {errorMessage}");
        }
        else if (fileName.EndsWith(".glb", StringComparison.Ordinal))
        {
            if (!loader.LoadBinaryFromFile(model, errorMessage, warningMessage, nativeFileName))
                throw new InvalidOperationException($"Failed to import GLTF file '{fileName}' due to error: {errorMessage}");
        }
        else
            throw new InvalidOperationException($"Unknown extension of file '{fileName}'");
        ValidateExtensions(model);
        return model;
    }

    public static unsafe Tg.Model LoadGLTFBinary(ReadOnlySpan<byte> data)
    {
        using var loader = new Tg.TinyGLTF();
        loader.SetImageLoader(GLTFTextureImporter.LoadImageData, IntPtr.Zero);
        using var errorMessage = new Std.String();
        using var warningMessage = new Std.String();
        var model = new Tg.Model();
        fixed (byte* dataPointer = data)
        {
            if (!loader.LoadBinaryFromMemory(model, errorMessage, warningMessage,
                (IntPtr)dataPointer, (uint)data.Length))
                throw new InvalidOperationException($"Failed to import binary GLTF file from memory due to error: {errorMessage}");
        }
        ValidateExtensions(model);
        return model;
    }

    public static void EnumerateChildrenRecursive(List<int> nodes, Tg.Model model, int nodeIndex)
    {
        var children = model.Nodes[nodeIndex].Children;
        foreach (var childIndex in children)
            nodes.Add(childIndex);
        foreach (var childIndex in children)
            EnumerateChildrenRecursive(nodes, model, childIndex);
    }

    public static void PreprocessModel(Tg.Model model, GLTFImporterSettings settings, GLTFImporterCallback callback)
    {
        var artificialSkinNodeNames = callback.GetArtificialSkinNodes();
        if (artificialSkinNodeNames.Count == 0 || model.Skins.Count != 0)
            return;
        int? emptyMeshIndex = null;
        for (var i = 0; i < model.Nodes.Count; ++i)
        {
            var node = model.Nodes[i];
            if (node.Skin >= 0 || !artificialSkinNodeNames.Contains(node.Name.ToString()))
                continue;
            if (emptyMeshIndex is null)
            {
                emptyMeshIndex = model.Meshes.Count;
                model.Meshes.Add(new Tg.Mesh());
            }
            var skinIndex = model.Skins.Count;
            var skin = new Tg.Skin();
            model.Skins.Add(skin);
            node.Skin = skinIndex;
            node.Mesh = emptyMeshIndex.Value;
            var nodes = new List<int>();
            EnumerateChildrenRecursive(nodes, model, i);
            skin.Skeleton = i;
            skin.Joints.Clear();
            foreach (var childIndex in nodes)
                skin.Joints.Add(childIndex);
        }
    }
}

public sealed partial class GLTFImporter
{
    private sealed class Impl : IDisposable
    {
        public Impl(GLTFImporterSettings settings, Tg.Model sourceModel,
            string outputPath, string resourceNamePrefix, GLTFImporterCallback callback)
        {
            _importerContext = new GLTFImporterBase(settings, sourceModel, outputPath, resourceNamePrefix, callback);
            _bufferReader = new GLTFBufferReader(_importerContext);
            _hierarchyAnalyzer = new GLTFHierarchyAnalyzer(_importerContext, _bufferReader);
            _textureImporter = new GLTFTextureImporter(_importerContext);
            _materialImporter = new GLTFMaterialImporter(_importerContext, _textureImporter);
            _modelImporter = new GLTFModelImporter(_importerContext, _bufferReader, _hierarchyAnalyzer, _materialImporter);
            _animationImporter = new GLTFAnimationImporter(_importerContext, _hierarchyAnalyzer, _modelImporter);
            _sceneImporter = new GLTFSceneImporter(_importerContext, _hierarchyAnalyzer, _modelImporter, _animationImporter);
        }

        public void Dispose() => _importerContext.Dispose();

        public void SaveResources()
        {
            _textureImporter.SaveResources();
            _materialImporter.SaveResources();
            _modelImporter.SaveResources();
            _animationImporter.SaveResources();
            _sceneImporter.SaveResources();
        }

        public Transform ConvertTransform(Transform sourceTransform)
        {
            var inlineTransform = _hierarchyAnalyzer.GetInlineTransform();
            var position = inlineTransform.ApplyToPosition(sourceTransform.Position);
            var rotation = inlineTransform.ApplyToRotation(sourceTransform.Rotation);
            return new Transform(position, rotation, sourceTransform.Scale);
        }

        public IReadOnlyDictionary<string, string> GetResourceNames() => _importerContext.GetResourceNames();

        private readonly GLTFImporterBase _importerContext;
        private readonly GLTFBufferReader _bufferReader;
        private readonly GLTFHierarchyAnalyzer _hierarchyAnalyzer;
        private readonly GLTFTextureImporter _textureImporter;
        private readonly GLTFMaterialImporter _materialImporter;
        private readonly GLTFModelImporter _modelImporter;
        private readonly GLTFAnimationImporter _animationImporter;
        private readonly GLTFSceneImporter _sceneImporter;
    }
}
