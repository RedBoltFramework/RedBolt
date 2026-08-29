// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Utility to import scenes.</summary>
internal sealed class GLTFSceneImporter
{
    public GLTFSceneImporter(GLTFImporterBase importerBase, GLTFHierarchyAnalyzer hierarchyAnalyzer,
        GLTFModelImporter modelImporter, GLTFAnimationImporter animationImporter)
    {
        _base = importerBase;
        _model = _base.GetModel();
        _hierarchyAnalyzer = hierarchyAnalyzer;
        _modelImporter = modelImporter;
        _animationImporter = animationImporter;
        ImportScenes();
    }

    public void SaveResources()
    {
        foreach (var scene in _scenes)
            _base.SaveResource(scene.Prefab!);
    }

    private sealed class ImportedScene
    {
        public int Index;
        public Scene Scene = null!;
        public PrefabResource? Prefab;
        public Dictionary<Node, int> NodeToIndex = [];
        public Dictionary<int, Node> IndexToNode = [];
    }

    private void ImportScenes()
    {
        for (var sceneIndex = 0; sceneIndex < _model.Scenes.Count; ++sceneIndex)
        {
            var sourceScene = _model.Scenes[sceneIndex];
            var scene = new ImportedScene { Index = sceneIndex, Scene = new Scene() };
            _scenes.Add(scene);
            var sceneName = _model.Scenes.Count == 1 ? "Prefab" : sourceScene.Name.ToString();
            var sceneFolder = _model.Scenes.Count == 1 ? string.Empty : "Prefabs/";
            ImportScene(scene, sceneName, sceneFolder);
        }
    }

    private void ImportScene(ImportedScene importedScene, string sceneName, string prefix)
    {
        var settings = _base.GetSettings();
        var sourceScene = _model.Scenes[importedScene.Index];
        var scene = importedScene.Scene;
        var prefabName = _base.GetResourceName(sceneName, prefix, "Prefab", ".prefab");
        scene.CreateComponent<Octree>();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        if (settings.Preview.HighRenderQuality)
        {
            var pipelineSettings = renderPipeline.Settings;
            var renderBufferManagerSettings = pipelineSettings.RenderBufferManager;
            renderBufferManagerSettings.ColorSpace = RenderPipelineColorSpace.LinearLDR;
            pipelineSettings.RenderBufferManager = renderBufferManagerSettings;
            var sceneProcessorSettings = pipelineSettings.SceneProcessor;
            sceneProcessorSettings.PcfKernelSize = 5;
            pipelineSettings.SceneProcessor = sceneProcessorSettings;
            renderPipeline.SetSettings(pipelineSettings);
            renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);
        }
        var rootNode = scene.CreateChild(settings.AssetName);
        rootNode.SetRotation(settings.Rotation);
        if (_animationImporter.HasSceneAnimations())
            InitializeAnimationController(rootNode, null);
        foreach (var sourceRootNode in _hierarchyAnalyzer.GetRootNodes())
        {
            // Preserve order and insert placeholders if Scene has root animations to keep animation track paths valid
            if (sourceScene.Nodes.Contains(sourceRootNode.Index))
                ImportNode(importedScene, rootNode, sourceRootNode);
            else if (_animationImporter.HasSceneAnimations())
                scene.CreateChild("Disabled Node Placeholder");
        }
        if (settings.SkipTags.Count != 0)
        {
            var children = rootNode.GetChildren(true).Select(child => new WeakReference<Node>(child)).ToList();
            foreach (var weakChild in children)
            {
                if (weakChild.TryGetTarget(out var child) && GLTFImporterFunctions.IsNameSkipped(child.GetName(), settings.SkipTags))
                    child.Remove();
            }
        }
        if (settings.CleanupRootNodes)
        {
            var newRootNode = rootNode;
            while (newRootNode.GetNumChildren() == 1 && newRootNode.GetNumComponents() == 0)
                newRootNode = newRootNode.GetChild(0)!;
            if (newRootNode != rootNode)
            {
                newRootNode.SetParent(scene);
                newRootNode.SetName(rootNode.GetName());
                rootNode.Remove();
            }
        }
        InitializeDefaultSceneContent(importedScene);
        importedScene.Prefab = new PrefabResource();
        scene.GeneratePrefab(importedScene.Prefab.GetMutableScenePrefab());
        importedScene.Prefab.NormalizeIds();
        importedScene.Prefab.SetName(prefabName);
        _base.AddToResourceCache(importedScene.Prefab);
    }

    private void ImportNode(ImportedScene importedScene, Node parent, GLTFNode sourceNode)
    {
        // Skip skinned mesh nodes w/o children because Urho instantiates such nodes at skeleton root.
        if (sourceNode.Mesh is not null && sourceNode.Skin is not null && sourceNode.Children.Count == 0 && sourceNode.SkinnedMeshNodes.Count == 0)
            return;
        var node = GetOrCreateNode(importedScene, parent, sourceNode);
        if (sourceNode.SkinnedMeshNodes.Count != 0)
        {
            var skeleton = _hierarchyAnalyzer.GetSkeleton(sourceNode.SkeletonIndex!.Value);
            foreach (var nodeIndex in sourceNode.SkinnedMeshNodes)
            {
                var skinnedMeshNode = _hierarchyAnalyzer.GetNode(nodeIndex);
                // Always create animated model in order to preserve morph animation order
                var animatedModel = node.CreateComponent<AnimatedModel>();
                InitializeComponentModelAndMaterials(animatedModel, skinnedMeshNode.Mesh!.Value, skinnedMeshNode.Skin!.Value);
                InitializeDefaultMorphWeights(animatedModel, skinnedMeshNode);
                TweakEmptyAnimatedModel(animatedModel);
            }
            if (_animationImporter.HasAnimations()) InitializeAnimationController(node, skeleton.Index);
            if (node.GetNumChildren() != 1)
                throw new InvalidOperationException($"Cannot connect node #{sourceNode.Index} to its children");
            // Connect bone nodes to GLTF nodes
            var skeletonRootNode = node.GetChild(0)!;
            skeletonRootNode.SetTransform(sourceNode.Position, sourceNode.Rotation, sourceNode.Scale);
            foreach (var (boneName, boneSourceNode) in skeleton.BoneNameToNode)
            {
                var boneNode = skeletonRootNode.GetName() == boneName ? skeletonRootNode : skeletonRootNode.GetChild(boneName, true);
                if (boneNode is null)
                    throw new InvalidOperationException($"Cannot connect node #{boneSourceNode.Index} to skeleton bone");
                RegisterNode(importedScene, boneNode, boneSourceNode);
            }
            foreach (var childNode in sourceNode.Children)
                ImportNode(importedScene, skeletonRootNode, childNode);
        }
        else
        {
            // Skip skinned mesh nodes w/o children because Urho instantiates such nodes at skeleton root.
            if (sourceNode.Mesh is not null && sourceNode.Skin is not null && sourceNode.Children.Count == 0)
                return;
            node.SetTransform(sourceNode.Position, sourceNode.Rotation, sourceNode.Scale);
            if (sourceNode.Mesh is not null && sourceNode.Skin is null)
            {
                if (_hierarchyAnalyzer.GetNumMorphsInMesh(sourceNode.Mesh.Value) > 0)
                {
                    var animatedModel = node.CreateComponent<AnimatedModel>();
                    InitializeComponentModelAndMaterials(animatedModel, sourceNode.Mesh.Value, -1);
                    InitializeDefaultMorphWeights(animatedModel, sourceNode);
                    TweakEmptyAnimatedModel(animatedModel);
                }
                else InitializeComponentModelAndMaterials(node.CreateComponent<StaticModel>(), sourceNode.Mesh.Value, -1);
            }
            foreach (var childNode in sourceNode.Children)
                ImportNode(importedScene, node, childNode);
        }
    }

    private void InitializeComponentModelAndMaterials(StaticModel staticModel, int meshIndex, int skinIndex)
    {
        var model = _modelImporter.GetModel(meshIndex, skinIndex);
        if (model is null)
            return;
        staticModel.SetModel(model);
        staticModel.SetCastShadows(true);
        var materialList = _modelImporter.GetModelMaterials(meshIndex, skinIndex);
        if (materialList.Type != typeof(Material).GetTypeId())
            return;
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        for (var i = 0; i < materialList.Names.Count; ++i)
            staticModel.SetMaterial(i, cache.GetResource<Material>(materialList.Names[i]));
    }

    private void InitializeAnimationController(Node node, int? groupIndex)
    {
        var animationController = node.CreateComponent<AnimationController>();
        var animation = _animationImporter.FindAnimation(_defaultAnimationIndex, groupIndex);
        if (animation is not null)
            animationController.Play(animation.GetName(), 0, true);
    }

    private void InitializeDefaultSceneContent(ImportedScene importedScene)
    {
        var defaultPosition = new Vector3f(-1.0f, 2.0f, 1.0f);
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var scene = importedScene.Scene;
        var settings = _base.GetSettings();
        if (settings.Preview.AddLights && scene.FindComponent<Light>() is null)
        {
            // Model forward is Z+, make default lighting from top right when looking at forward side of model.
            var node = scene.CreateChild("Default Light");
            node.SetPosition(defaultPosition);
            node.SetDirection(new Vector3f(1.0f, -2.0f, -1.0f));
            var light = node.CreateComponent<Light>();
            light.SetLightType(LightType.Directional);
            light.SetCastShadows(true);
        }
        if (settings.Preview.AddSkybox && scene.FindComponent<Skybox>() is null)
        {
            const string skyboxModelName = "Models/Box.mdl";
            var skyboxMaterial = cache.GetResource<Material>(settings.Preview.SkyboxMaterial);
            var boxModel = cache.GetResource<Model>(skyboxModelName);
            if (skyboxMaterial is null) Log.GetLogger().Warning("Cannot add default skybox with material '{}'", settings.Preview.SkyboxMaterial);
            else if (boxModel is null) Log.GetLogger().Warning("Cannot add default skybox with model '{}'", skyboxModelName);
            else
            {
                var skyboxNode = scene.CreateChild("Default Skybox");
                skyboxNode.SetPosition(defaultPosition);
                var skybox = skyboxNode.CreateComponent<Skybox>();
                skybox.SetModel(boxModel);
                skybox.SetMaterial(skyboxMaterial);
            }
        }
        if (settings.Preview.AddReflectionProbe && scene.FindComponent<Zone>() is null)
        {
            var skyboxTexture = cache.GetResource<TextureCube>(settings.Preview.ReflectionProbeCubemap);
            if (skyboxTexture is null) Log.GetLogger().Warning("Cannot add default reflection probe with material '{}'", settings.Preview.ReflectionProbeCubemap);
            else
            {
                var zoneNode = scene.CreateChild("Default Zone");
                zoneNode.SetPosition(defaultPosition);
                var zone = zoneNode.CreateComponent<Zone>();
                zone.SetBackgroundBrightness(0.5f);
                zone.SetZoneTexture(skyboxTexture);
            }
        }
    }

    private static void InitializeDefaultMorphWeights(AnimatedModel animatedModel, GLTFNode sourceNode)
    {
        var numMorphs = animatedModel.GetNumMorphs();
        if (numMorphs != sourceNode.MorphWeights.Count)
            throw new InvalidOperationException("Cannot setup mesh morphs");
        for (var morphIndex = 0; morphIndex < numMorphs; ++morphIndex)
            animatedModel.SetMorphWeight(morphIndex, sourceNode.MorphWeights[morphIndex]);
    }

    private static void TweakEmptyAnimatedModel(AnimatedModel animatedModel)
    {
        if (animatedModel.GetNumGeometries() == 0)
        {
            animatedModel.SetUpdateInvisible(true);
            animatedModel.SetAnimationLodBias(0.0f);
        }
    }

    private static void RegisterNode(ImportedScene importedScene, Node node, GLTFNode sourceNode)
    {
        importedScene.IndexToNode[sourceNode.Index] = node;
        importedScene.NodeToIndex[node] = sourceNode.Index;
    }

    private static Node GetOrCreateNode(ImportedScene importedScene, Node parentNode, GLTFNode sourceNode)
    {
        if (importedScene.IndexToNode.TryGetValue(sourceNode.Index, out var existingNode))
            return existingNode;
        var node = parentNode.CreateChild(sourceNode.GetEffectiveName());
        RegisterNode(importedScene, node, sourceNode);
        return node;
    }

    private readonly GLTFImporterBase _base;
    private readonly Tg.Model _model;
    private readonly GLTFHierarchyAnalyzer _hierarchyAnalyzer;
    private readonly GLTFModelImporter _modelImporter;
    private readonly GLTFAnimationImporter _animationImporter;
    private int _defaultAnimationIndex = 0;
    private readonly List<ImportedScene> _scenes = [];
}
