namespace RedBolt.Utility;

using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.TinyGltf;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.IO;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

using TinyGltfModel = RedBolt.Bindings.TinyGltf.Model;
using TinyGltfPrimitive = RedBolt.Bindings.TinyGltf.Primitive;
using TinyGltfAccessor = RedBolt.Bindings.TinyGltf.Accessor;
using TinyGltfNode = RedBolt.Bindings.TinyGltf.Node;

/// <summary>Options controlling glTF import.</summary>
public sealed class GLTFImporterSettings : IArchiveSerializable
{
    public string AssetName = "Asset";
    public bool MirrorX;
    public float Scale = 1.0f;
    public Quaternion Rotation = Quaternion.IDENTITY;
    public bool FadeTransparency;
    public bool CleanupBoneNames = true;
    public bool CleanupRootNodes = true;
    public bool CombineLODs = true;
    public List<string> SkipTags = [];
    public bool KeepNamesOnMerge;
    public bool AddEmptyNodesToSkeleton;
    public float OffsetMatrixError = 0.00002f;
    public float KeyFrameTimeError = MathDefs.Epsilon;
    public Dictionary<string, string> NodeRenames = new(StringComparer.Ordinal);
    public bool GpuResources;
    public PreviewOptions Preview = new();

    public sealed class PreviewOptions
    {
        public bool AddLights = true;
        public bool AddSkybox = true;
        public string SkyboxMaterial = "Materials/DefaultSkybox.xml";
        public bool AddReflectionProbe = true;
        public string ReflectionProbeCubemap = "Textures/DefaultSkybox.xml";
        public bool HighRenderQuality = true;
    }

    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "assetName", ref AssetName, "Asset", ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "mirrorX", ref MirrorX, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "scale", ref Scale, 1.0f, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "rotation", ref Rotation, Quaternion.IDENTITY, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "fadeTransparency", ref FadeTransparency, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "cleanupBoneNames", ref CleanupBoneNames, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "cleanupRootNodes", ref CleanupRootNodes, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "combineLODs", ref CombineLODs, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "skipTag", ref SkipTags, [],
            static (Archive ar, string name, ref List<string> value) => ArchiveSerialization.SerializeStringVector(ar, name, value));
        ArchiveSerialization.SerializeOptionalValue(archive, "keepNamesOnMerge", ref KeepNamesOnMerge, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addEmptyNodesToSkeleton", ref AddEmptyNodesToSkeleton, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "offsetMatrixError", ref OffsetMatrixError, 0.00002f, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "keyFrameTimeError", ref KeyFrameTimeError, MathDefs.Epsilon, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "nodeRenames", ref NodeRenames,
            new Dictionary<string, string>(StringComparer.Ordinal),
            static (Archive ar, string name, ref Dictionary<string, string> value) =>
                ArchiveSerialization.SerializeStringMap(ar, name, value));
        ArchiveSerialization.SerializeOptionalValue(archive, "gpuResources", ref GpuResources, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addLights", ref Preview.AddLights, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addSkybox", ref Preview.AddSkybox, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "skyboxMaterial", ref Preview.SkyboxMaterial, "Materials/DefaultSkybox.xml", ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addReflectionProbe", ref Preview.AddReflectionProbe, true, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "reflectionProbeCubemap", ref Preview.ReflectionProbeCubemap, "Textures/DefaultSkybox.xml", ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "highRenderQuality", ref Preview.HighRenderQuality, true, ArchiveSerialization.SerializeValue);
    }
}

/// <summary>Receives resources produced by <see cref="GLTFImporter.Process"/>.</summary>
public class GLTFImporterCallback
{
    public virtual void OnModelLoaded(ModelView modelView) { }
    public virtual void OnAnimationLoaded(RedBolt.Graphics.Animation animation) { }
    public virtual List<string> GetArtificialSkinNodes() => [];
}

/// <summary>Loads glTF assets through TinyGLTF and converts static mesh primitives into <see cref="ModelView"/>.</summary>
public sealed class GLTFImporter : RedObject
{
    private const int ComponentByte = 5120;
    private const int ComponentUnsignedByte = 5121;
    private const int ComponentShort = 5122;
    private const int ComponentUnsignedShort = 5123;
    private const int ComponentInt = 5124;
    private const int ComponentUnsignedInt = 5125;
    private const int ComponentFloat = 5126;
    private const int ComponentDouble = 5130;
    private const int TypeScalar = 65;
    private const int TypeVec2 = 2;
    private const int TypeVec3 = 3;
    private const int TypeVec4 = 4;
    private const int TypeMat4 = 36;

    private TinyGltfModel? _model;
    private ModelView? _modelView;
    private readonly List<ModelView> _modelViews = [];
    private readonly Dictionary<(int Mesh, int Skin), CookedModel> _modelsByMesh = [];
    private readonly Dictionary<int, string> _boneNamesByNode = [];
    private readonly Dictionary<string, RedBolt.Graphics.Material> _materialsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _savedResources = new(StringComparer.Ordinal);
    private readonly List<RedBolt.Graphics.Animation> _animations = [];
    private readonly List<Texture2D> _importedTextures = [];
    private readonly List<(Resource Resource, string FileName)> _cookedResources = [];
    private bool _processed;

    private sealed record ImportedTexture(Texture2D Texture, RedBolt.Resource.Image Image);
    private sealed record CookedModel(RedBolt.Graphics.Model Model, IReadOnlyList<string> Materials);

    public GLTFImporter(GLTFImporterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = CloneSettings(settings);
    }

    public GLTFImporterSettings Settings { get; }
    public string LastError { get; private set; } = string.Empty;
    public string LastWarning { get; private set; } = string.Empty;
    public TinyGltfModel? SourceModel => _model;
    public ModelView? ImportedModel => _modelView;
    public IReadOnlyList<RedBolt.Graphics.Animation> ImportedAnimations => _animations;
    public IReadOnlyDictionary<string, string> SavedResources => _savedResources;

    /// <summary>Load a primary glTF or GLB file. A source model can only be loaded once.</summary>
    public bool LoadFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!System.IO.File.Exists(fileName))
            return Fail($"glTF file '{fileName}' does not exist.");
        if (!CanLoadPrimary())
            return false;

        using var loader = new TinyGLTF();
        loader.SetStoreOriginalJSONForExtrasAndExtensions(true);
        using var error = new Std.String();
        using var warning = new Std.String();
        using var nativeFileName = new Std.String(fileName);
        var model = new TinyGltfModel();
        ConfigureImageLoader(loader);
        var extension = Path.GetExtension(fileName);
        var loaded = string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase)
            ? loader.LoadBinaryFromFile(model, error, warning, nativeFileName)
            : string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase)
                ? loader.LoadASCIIFromFile(model, error, warning, nativeFileName)
                : false;
        LastWarning = warning.ToString();
        if (!loaded)
        {
            model.Dispose();
            return Fail(string.IsNullOrEmpty(error.ToString()) ? $"Unknown glTF file extension '{extension}'." : error.ToString());
        }

        ValidateExtensions(model);
        RenameNodes(model);
        _model = model;
        return true;
    }

    /// <summary>Load a binary GLB document from memory.</summary>
    public bool LoadFileBinary(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return Fail("glTF binary data is empty.");
        if (!CanLoadPrimary())
            return false;

        var bytes = data.ToArray();
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            using var loader = new TinyGLTF();
            loader.SetStoreOriginalJSONForExtrasAndExtensions(true);
            using var error = new Std.String();
            using var warning = new Std.String();
            var model = new TinyGltfModel();
            ConfigureImageLoader(loader);
            var loaded = loader.LoadBinaryFromMemory(model, error, warning, pin.AddrOfPinnedObject(), checked((uint)bytes.Length));
            LastWarning = warning.ToString();
            if (!loaded)
            {
                model.Dispose();
                return Fail(error.ToString());
            }

            ValidateExtensions(model);
            RenameNodes(model);
            _model = model;
            return true;
        }
        finally
        {
            pin.Free();
        }
    }

    /// <summary>Load and merge animation data from a secondary glTF file.</summary>
    public bool MergeFile(string fileName, string assetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(assetName);
        if (_processed)
            return Fail("Source glTF model is already processed.");
        if (_model is null)
            return Fail("Primary source glTF model is not loaded.");
        if (!System.IO.File.Exists(fileName))
            return Fail($"glTF file '{fileName}' does not exist.");

        using var loader = new TinyGLTF();
        loader.SetStoreOriginalJSONForExtrasAndExtensions(true);
        using var error = new Std.String();
        using var warning = new Std.String();
        using var nativeFileName = new Std.String(fileName);
        using var secondaryModel = new TinyGltfModel();
        ConfigureImageLoader(loader);
        var loaded = string.Equals(Path.GetExtension(fileName), ".glb", StringComparison.OrdinalIgnoreCase)
            ? loader.LoadBinaryFromFile(secondaryModel, error, warning, nativeFileName)
            : loader.LoadASCIIFromFile(secondaryModel, error, warning, nativeFileName);
        LastWarning = warning.ToString();
        if (!loaded)
            return Fail(error.ToString());

        RenameNodes(secondaryModel);
        MergeModels(_model, secondaryModel, Settings.KeepNamesOnMerge ? null : assetName);
        return true;
    }

    /// <summary>Convert the primary source model into a static <see cref="ModelView"/>.</summary>
    public bool Process(string outputPath, string resourceNamePrefix, GLTFImporterCallback? callback = null)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(resourceNamePrefix);
        if (_model is null)
            return Fail("Primary source model is not loaded.");
        if (_processed)
            return Fail("Primary source model is already processed.");

        try
        {
            PreprocessModel(_model, callback);
            var materialNames = ImportMaterials(_model, outputPath, resourceNamePrefix);
            var models = BuildModelViews(_model, materialNames, BuildSkinBones(_model));
            if (models.Count == 0)
            {
                _modelView = new ModelView();
                _modelView.SetName(Settings.AssetName);
                _modelViews.Add(_modelView);
            }
            var usedModelResourceNames = new HashSet<string>(_savedResources.Keys, StringComparer.Ordinal);
            foreach (var (modelPairs, modelView, materialList, nameHint) in models)
            {
                var localResourceName = CreateUniqueResourceName("Models", nameHint, ".mdl", usedModelResourceNames);
                var modelResource = modelView.ExportModel(
                    Settings.GpuResources ? ModelViewExportFlag.None : ModelViewExportFlag.Headless,
                    resourceNamePrefix + localResourceName);
                var importedModel = new CookedModel(modelResource, materialList);
                foreach (var modelPair in modelPairs)
                    _modelsByMesh.Add(modelPair, importedModel);
                _modelViews.Add(modelView);
                _modelView ??= modelView;
                AddCookedResource(modelResource, outputPath, localResourceName);
                callback?.OnModelLoaded(modelView);
            }
            ImportAnimations(_model, outputPath, resourceNamePrefix, callback);
            ImportPrefabs(_model, outputPath, resourceNamePrefix, _modelsByMesh);
            _model.Dispose();
            _model = null;
            _processed = true;
            return true;
        }
        catch (InvalidDataException exception)
        {
            ClearCookedResources();
            return Fail(exception.Message);
        }
    }

    private static void PreprocessModel(TinyGltfModel model, GLTFImporterCallback? callback)
    {
        if (callback is null)
            return;
        var artificialSkinNodes = callback.GetArtificialSkinNodes();
        if (artificialSkinNodes.Count == 0)
            return;

        using var skins = model.Skins;
        if (skins.Count != 0)
            return;
        using var nodes = model.Nodes;
        using var meshes = model.Meshes;
        var skinNames = new HashSet<string>(artificialSkinNodes, StringComparer.Ordinal);
        var emptyMeshIndex = -1;
        for (var nodeIndex = 0; nodeIndex < nodes.Count; ++nodeIndex)
        {
            using var node = nodes[nodeIndex];
            using var name = node.Name;
            if (node.Skin >= 0 || !skinNames.Contains(name.ToString()))
                continue;
            if (emptyMeshIndex < 0)
            {
                emptyMeshIndex = meshes.Count;
                using var emptyMesh = new RedBolt.Bindings.TinyGltf.Mesh();
                meshes.Add(emptyMesh);
            }

            var skinIndex = skins.Count;
            using var skin = new RedBolt.Bindings.TinyGltf.Skin();
            skin.Skeleton = nodeIndex;
            using (var joints = skin.Joints)
            {
                foreach (var childIndex in EnumerateDescendants(nodes, nodeIndex, new HashSet<int>()))
                    joints.Add(childIndex);
            }
            skins.Add(skin);
            node.Skin = skinIndex;
            node.Mesh = emptyMeshIndex;
            nodes[nodeIndex] = node;
        }
    }

    private static IEnumerable<int> EnumerateDescendants(Std.Vector<TinyGltfNode> nodes, int nodeIndex, ISet<int> visiting)
    {
        if ((uint)nodeIndex >= (uint)nodes.Count || !visiting.Add(nodeIndex))
            throw new InvalidDataException("glTF node hierarchy is invalid or cyclic.");
        using var node = nodes[nodeIndex];
        using var children = node.Children;
        foreach (var childIndex in children)
        {
            yield return childIndex;
            foreach (var descendant in EnumerateDescendants(nodes, childIndex, visiting))
                yield return descendant;
        }
        visiting.Remove(nodeIndex);
    }

    /// <summary>Save cooked model resources to the output paths provided to <see cref="Process"/>.</summary>
    public bool SaveResources()
    {
        if (!_processed || _cookedResources.Count == 0)
            return Fail("Imported assets weren't cooked.");

        foreach (var (resource, fileName) in _cookedResources)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            using var file = new File(fileName, FileMode.Write);
            if (!file.IsOpen || !resource.Save(file))
                return Fail($"Failed to save imported resource '{resource.Name}'.");
        }
        return true;
    }

    public Transform ConvertTransform(Transform sourceTransform)
    {
        if (!_processed)
            return sourceTransform;

        var position = sourceTransform.Position * Settings.Scale;
        if (Settings.MirrorX)
        {
            position.X = -position.X;
            sourceTransform.Rotation = new Quaternion(sourceTransform.Rotation.W, sourceTransform.Rotation.X,
                -sourceTransform.Rotation.Y, -sourceTransform.Rotation.Z);
        }
        return new Transform(position, sourceTransform.Rotation, sourceTransform.Scale);
    }

    public override void Dispose()
    {
        ClearCookedResources();
        _model?.Dispose();
        _model = null;
        base.Dispose();
    }

    private bool CanLoadPrimary()
    {
        if (_model is null && !_processed)
            return true;
        return Fail("Primary source model is already loaded or processed.");
    }

    private static unsafe void ConfigureImageLoader(TinyGLTF loader) => loader.SetImageLoader(
        (nint)(delegate* unmanaged[Cdecl]<nint, int, nint, nint, int, int, byte*, int, nint, byte>)&LoadImageData, 0);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe byte LoadImageData(nint imageHandle, int imageIndex, nint error, nint warning,
        int requestedWidth, int requestedHeight, byte* bytes, int size, nint userData)
    {
        if (imageHandle == 0 || bytes is null || size < 0)
            return 0;
        using var image = RedBolt.Bindings.TinyGltf.Image.CreateManagedWrapper(imageHandle, ownsHandle: false);
        using var imageData = new Std.Vector<byte>(new ReadOnlySpan<byte>(bytes, size));
        image.AsIs = true;
        image.ImageValue = imageData;
        return 1;
    }

    private bool Fail(string error)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? "TinyGLTF failed to load the document." : error;
        return false;
    }

    private void ValidateExtensions(TinyGltfModel model)
    {
        using var extensions = model.ExtensionsUsed;
        foreach (var extension in extensions)
        {
            using (extension)
            {
                if (extension.ToString() != "KHR_materials_unlit")
                    Warn($"Unsupported extension used: '{extension}'.");
            }
        }
    }

    private void ImportAnimations(TinyGltfModel model, string outputPath, string resourceNamePrefix,
        GLTFImporterCallback? callback)
    {
        using var sourceAnimations = model.Animations;
        using var sourceNodes = model.Nodes;
        var usedResourceNames = new HashSet<string>(StringComparer.Ordinal);
        for (var animationIndex = 0; animationIndex < sourceAnimations.Count; ++animationIndex)
        {
            using var sourceAnimation = sourceAnimations[animationIndex];
            using var sourceName = sourceAnimation.Name;
            var sourceNameText = sourceName.ToString();
            if (IsNameSkipped(sourceNameText))
                continue;
            var tracks = ReadAnimationTracks(model, sourceAnimation, sourceNodes, animationIndex);
            var nameHint = string.IsNullOrEmpty(sourceNameText) ? $"Animation_{animationIndex}" : sourceNameText;
            var localResourceName = CreateUniqueResourceName("Animations", nameHint, ".ani", usedResourceNames);
            var animation = new RedBolt.Graphics.Animation();
            animation.SetName(resourceNamePrefix + localResourceName);
            animation.SetAnimationName(Path.GetFileNameWithoutExtension(localResourceName));
            foreach (var (trackName, sourceTrack) in tracks)
                CreateAnimationTrack(animation, trackName, sourceTrack);
            ReadMorphAnimationTracks(animation, model, sourceAnimation, sourceNodes, animationIndex);
            if (animation.Tracks.Count == 0 && animation.VariantTracks.Count == 0)
            {
                animation.Dispose();
                continue;
            }

            AddCookedResource(animation, outputPath, localResourceName);
            _animations.Add(animation);
            callback?.OnAnimationLoaded(animation);
        }
    }

    private void ImportPrefabs(TinyGltfModel model, string outputPath, string resourceNamePrefix,
        IReadOnlyDictionary<(int Mesh, int Skin), CookedModel> modelsByMesh)
    {
        using var sourceScenes = model.Scenes;
        using var sourceNodes = model.Nodes;
        var usedResourceNames = new HashSet<string>(StringComparer.Ordinal);
        for (var sceneIndex = 0; sceneIndex < sourceScenes.Count; ++sceneIndex)
        {
            using var sourceScene = sourceScenes[sceneIndex];
            using var sourceName = sourceScene.Name;
            var localResourceName = sourceScenes.Count == 1
                ? "Prefab.prefab"
                : CreateUniqueResourceName("Prefabs", sourceName.ToString(), ".prefab", usedResourceNames);
            using var scene = new RedBolt.Scene.Scene();
            scene.CreateComponent<Octree>();
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            if (Settings.Preview.HighRenderQuality)
            {
                var pipelineSettings = renderPipeline.Settings;
                var renderBufferManager = pipelineSettings.RenderBufferManager;
                renderBufferManager.ColorSpace = RenderPipelineColorSpace.LinearLDR;
                pipelineSettings.RenderBufferManager = renderBufferManager;
                var sceneProcessor = pipelineSettings.SceneProcessor;
                sceneProcessor.PcfKernelSize = 5;
                pipelineSettings.SceneProcessor = sceneProcessor;
                renderPipeline.SetSettings(in pipelineSettings);
                renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);
            }
            var root = scene.CreateChild(Settings.AssetName);
            root.SetRotation(Settings.Rotation);
            using var roots = sourceScene.Nodes;
            var visiting = new HashSet<int>();
            foreach (var rootIndex in roots)
                ImportPrefabNode(root, rootIndex, sourceNodes, modelsByMesh, visiting);

            if (_animations.Count != 0)
                root.CreateComponent<AnimationController>().PlayNew(new AnimationParameters(_animations[0]).Looped());
            RemoveSkippedPrefabNodes(root);
            CleanupPrefabRoot(scene, root);
            InitializeDefaultSceneContent(scene);

            var prefab = new PrefabResource();
            scene.GeneratePrefab(prefab.GetMutableScenePrefab());
            prefab.NormalizeIds();
            prefab.SetName(resourceNamePrefix + localResourceName);
            AddCookedResource(prefab, outputPath, localResourceName);
        }
    }

    private void InitializeDefaultSceneContent(RedBolt.Scene.Scene scene)
    {
        var cache = GetSubsystem<ResourceCache>();
        var defaultPosition = new Vector3f(-1.0f, 2.0f, 1.0f);
        if (Settings.Preview.AddLights && scene.FindComponent<RedBolt.Graphics.Light>() is null)
        {
            var node = scene.CreateChild("Default Light");
            node.SetPosition(defaultPosition);
            node.SetDirection(new Vector3f(1.0f, -2.0f, -1.0f));
            var light = node.CreateComponent<RedBolt.Graphics.Light>();
            light.SetLightType(LightType.Directional);
            light.SetCastShadows(true);
        }

        if (Settings.Preview.AddSkybox && scene.FindComponent<Skybox>() is null
            && cache?.GetResourceOrNull<RedBolt.Graphics.Material>(Settings.Preview.SkyboxMaterial) is { } skyboxMaterial
            && cache.GetResourceOrNull<RedBolt.Graphics.Model>("Models/Box.mdl") is { } boxModel)
        {
            var node = scene.CreateChild("Default Skybox");
            node.SetPosition(defaultPosition);
            var skybox = node.CreateComponent<Skybox>();
            skybox.SetModel(boxModel);
            skybox.SetMaterial(skyboxMaterial);
        }

        if (Settings.Preview.AddReflectionProbe && scene.FindComponent<Zone>() is null
            && cache?.GetResourceOrNull<TextureCube>(Settings.Preview.ReflectionProbeCubemap) is { } skyboxTexture)
        {
            var node = scene.CreateChild("Default Zone");
            node.SetPosition(defaultPosition);
            var zone = node.CreateComponent<Zone>();
            zone.SetBackgroundBrightness(0.5f);
            zone.SetZoneTexture(skyboxTexture);
        }
    }

    private void ImportPrefabNode(RedBolt.Scene.Node parent, int nodeIndex, Std.Vector<TinyGltfNode> sourceNodes,
        IReadOnlyDictionary<(int Mesh, int Skin), CookedModel> modelsByMesh, ISet<int> visiting)
    {
        if ((uint)nodeIndex >= (uint)sourceNodes.Count || !visiting.Add(nodeIndex))
            throw new InvalidDataException($"Scene references invalid or cyclic node #{nodeIndex}.");
        using var source = sourceNodes[nodeIndex];
        using var sourceName = source.Name;
        var node = parent.CreateChild(string.IsNullOrEmpty(sourceName.ToString()) ? $"Node_{nodeIndex}" : sourceName.ToString());
        var transform = ReadNodeTransform(source);
        node.SetTransform(ConvertScenePosition(transform.Position), ConvertSceneRotation(transform.Rotation), transform.Scale);
        if (modelsByMesh.TryGetValue((source.Mesh, source.Skin), out var importedModel))
        {
            StaticModel modelComponent = source.Skin < 0 && importedModel.Model.GetNumMorphs() == 0
                ? node.CreateComponent<StaticModel>()
                : node.CreateComponent<AnimatedModel>();
            modelComponent.SetModel(importedModel.Model);
            modelComponent.SetCastShadows(true);
            for (var materialIndex = 0; materialIndex < importedModel.Materials.Count; ++materialIndex)
            {
                if (_materialsByName.TryGetValue(importedModel.Materials[materialIndex], out var material))
                    modelComponent.SetMaterial((uint)materialIndex, material);
            }
            if (modelComponent is AnimatedModel animatedModel)
            {
                using var weights = source.Weights;
                if (weights.Count != 0 && weights.Count != animatedModel.GetNumMorphs())
                    throw new InvalidDataException("glTF node morph weight count does not match its mesh.");
                for (var morphIndex = 0; morphIndex < weights.Count; ++morphIndex)
                    animatedModel.SetMorphWeight(morphIndex, (float)weights[morphIndex]);
            }
        }
        using var children = source.Children;
        foreach (var childIndex in children)
            ImportPrefabNode(node, childIndex, sourceNodes, modelsByMesh, visiting);
        visiting.Remove(nodeIndex);
    }

    private void RemoveSkippedPrefabNodes(RedBolt.Scene.Node root)
    {
        if (Settings.SkipTags.Count == 0)
            return;

        foreach (var node in root.GetChildren(recursive: true))
        {
            if (!IsNameSkipped(node.Name) || HasSkippedAncestor(node, root))
                continue;
            node.Remove();
        }
    }

    private void CleanupPrefabRoot(RedBolt.Scene.Scene scene, RedBolt.Scene.Node root)
    {
        if (!Settings.CleanupRootNodes || root.GetNumComponents() != 0)
            return;

        var newRoot = root;
        while (newRoot.GetNumChildren() == 1 && newRoot.GetNumComponents() == 0)
            newRoot = newRoot.GetChild(0)!;

        if (ReferenceEquals(newRoot, root))
            return;
        newRoot.SetParent(scene);
        newRoot.SetName(root.Name);
        root.Remove();
    }

    private bool IsNameSkipped(string name) => Settings.SkipTags.Any(tag => !string.IsNullOrEmpty(tag)
        && name.Contains(tag, StringComparison.Ordinal));

    private bool HasSkippedAncestor(RedBolt.Scene.Node node, RedBolt.Scene.Node root)
    {
        for (var ancestor = node.Parent; ancestor is not null && !ReferenceEquals(ancestor, root); ancestor = ancestor.Parent)
        {
            if (IsNameSkipped(ancestor.Name))
                return true;
        }
        return false;
    }

    private Vector3f ConvertScenePosition(Std.Vector<double> position)
    {
        return ConvertScenePosition(new Vector3f((float)position[0], (float)position[1], (float)position[2]));
    }

    private Vector3f ConvertScenePosition(Vector3f position)
    {
        var result = position * Settings.Scale;
        if (Settings.MirrorX)
            result.X = -result.X;
        return result;
    }

    private Quaternion ConvertSceneRotation(Quaternion rotation) => Settings.MirrorX
        ? new Quaternion(rotation.W, rotation.X, -rotation.Y, -rotation.Z)
        : rotation;

    private Dictionary<string, TransformTrackSource> ReadAnimationTracks(TinyGltfModel model,
        RedBolt.Bindings.TinyGltf.Animation sourceAnimation, Std.Vector<RedBolt.Bindings.TinyGltf.Node> sourceNodes,
        int animationIndex)
    {
        using var channels = sourceAnimation.Channels;
        using var samplers = sourceAnimation.Samplers;
        var result = new Dictionary<string, TransformTrackSource>(StringComparer.Ordinal);
        for (var channelIndex = 0; channelIndex < channels.Count; ++channelIndex)
        {
            using var channel = channels[channelIndex];
            using var path = channel.TargetPath;
            var targetPath = path.ToString();
            if (targetPath == "weights")
                continue;
            if (targetPath is not ("translation" or "rotation" or "scale"))
            {
                Warn($"Unsupported animation channel '{targetPath}' in animation #{animationIndex}.");
                continue;
            }
            if ((uint)channel.TargetNode >= (uint)sourceNodes.Count || (uint)channel.Sampler >= (uint)samplers.Count)
                throw new InvalidDataException("glTF animation references an invalid node or sampler.");

            using var node = sourceNodes[channel.TargetNode];
            using var nodeName = node.Name;
            var trackName = _boneNamesByNode.GetValueOrDefault(channel.TargetNode,
                string.IsNullOrEmpty(nodeName.ToString()) ? $"Node_{channel.TargetNode}" : nodeName.ToString());
            using var sampler = samplers[channel.Sampler];
            using var interpolation = sampler.Interpolation;
            var interpolationName = interpolation.ToString();
            if (interpolationName is not ("" or "LINEAR" or "STEP" or "CUBICSPLINE"))
                throw new InvalidDataException($"glTF animation uses unsupported interpolation '{interpolationName}'.");

            var times = ReadFloatAccessor(model, sampler.Input, TypeScalar).Select(static value => value[0]).ToList();
            if (!result.TryGetValue(trackName, out var track))
            {
                track = new TransformTrackSource();
                result.Add(trackName, track);
            }

            var step = interpolationName == "STEP";
            var cubicSpline = interpolationName == "CUBICSPLINE";
            switch (targetPath)
            {
                case "translation":
                    if (track.Position is not null)
                        throw new InvalidDataException($"Duplicate translation animation channel in animation #{animationIndex}.");
                    AddAnimationChannel(track, times, ReadCubicSplineValues(ReadAnimationVectors(model, sampler.Output, TypeVec3, animationIndex), times.Count, cubicSpline), step,
                        static (source, value) => source.Position = value);
                    break;
                case "rotation":
                    if (track.Rotation is not null)
                        throw new InvalidDataException($"Duplicate rotation animation channel in animation #{animationIndex}.");
                    AddAnimationChannel(track, times, ReadCubicSplineValues(ReadAnimationRotations(model, sampler.Output, animationIndex), times.Count, cubicSpline), step,
                        static (source, value) => source.Rotation = value);
                    break;
                case "scale":
                    if (track.Scale is not null)
                        throw new InvalidDataException($"Duplicate scale animation channel in animation #{animationIndex}.");
                    AddAnimationChannel(track, times, ReadCubicSplineValues(ReadAnimationVectors(model, sampler.Output, TypeVec3, animationIndex), times.Count, cubicSpline), step,
                        static (source, value) => source.Scale = value);
                    break;
            }
        }
        return result;
    }

    private static void ReadMorphAnimationTracks(RedBolt.Graphics.Animation animation, TinyGltfModel model,
        RedBolt.Bindings.TinyGltf.Animation sourceAnimation, Std.Vector<TinyGltfNode> sourceNodes, int animationIndex)
    {
        using var channels = sourceAnimation.Channels;
        using var samplers = sourceAnimation.Samplers;
        for (var channelIndex = 0; channelIndex < channels.Count; ++channelIndex)
        {
            using var channel = channels[channelIndex];
            using var path = channel.TargetPath;
            if (path.ToString() != "weights")
                continue;
            if ((uint)channel.TargetNode >= (uint)sourceNodes.Count || (uint)channel.Sampler >= (uint)samplers.Count)
                throw new InvalidDataException("glTF animation references an invalid node or sampler.");

            using var node = sourceNodes[channel.TargetNode];
            if (node.Mesh < 0)
                throw new InvalidDataException($"Animation #{animationIndex} weights channel targets a node without a mesh.");
            var morphCount = GetMorphCount(model, node.Mesh);
            if (morphCount == 0)
                throw new InvalidDataException($"Animation #{animationIndex} weights channel targets a mesh without morphs.");
            using var nodeName = node.Name;
            var targetName = string.IsNullOrEmpty(nodeName.ToString()) ? $"Node_{channel.TargetNode}" : nodeName.ToString();
            using var sampler = samplers[channel.Sampler];
            using var interpolation = sampler.Interpolation;
            var interpolationName = interpolation.ToString();
            if (interpolationName is not ("" or "LINEAR" or "STEP" or "CUBICSPLINE"))
                throw new InvalidDataException($"glTF animation uses unsupported interpolation '{interpolationName}'.");

            var times = ReadFloatAccessor(model, sampler.Input, TypeScalar).Select(static value => value[0]).ToList();
            var values = ReadFloatAccessor(model, sampler.Output, TypeScalar).Select(static value => value[0]).ToList();
            var valuesPerKey = interpolationName == "CUBICSPLINE" ? morphCount * 3 : morphCount;
            if (times.Count == 0 || values.Count != times.Count * valuesPerKey)
                throw new InvalidDataException("glTF morph animation channel input and output counts differ.");

            for (var morphIndex = 0; morphIndex < morphCount; ++morphIndex)
            {
                var track = animation.CreateVariantTrack($"{targetName}/@AnimatedModel/Morphs/{morphIndex}");
                track.Interpolation = interpolationName switch
                {
                    "STEP" => KeyFrameInterpolation.None,
                    "CUBICSPLINE" => KeyFrameInterpolation.TangentSpline,
                    _ => KeyFrameInterpolation.Linear
                };
                for (var keyIndex = 0; keyIndex < times.Count; ++keyIndex)
                {
                    var valueOffset = keyIndex * valuesPerKey + (interpolationName == "CUBICSPLINE" ? morphCount : 0) + morphIndex;
                    track.KeyFrames.Add(new VariantCurvePoint { Time = times[keyIndex], Value = new Variant(values[valueOffset]) });
                    if (interpolationName == "CUBICSPLINE")
                    {
                        track.InTangents.Add(new Variant(values[keyIndex * valuesPerKey + morphIndex]));
                        track.OutTangents.Add(new Variant(values[keyIndex * valuesPerKey + morphCount * 2 + morphIndex]));
                    }
                }
                track.Commit();
            }
        }
    }

    private static int GetMorphCount(TinyGltfModel model, int meshIndex)
    {
        using var meshes = model.Meshes;
        if ((uint)meshIndex >= (uint)meshes.Count)
            throw new InvalidDataException("glTF animation references an invalid mesh.");
        using var mesh = meshes[meshIndex];
        using var primitives = mesh.Primitives;
        var morphCount = -1;
        for (var primitiveIndex = 0; primitiveIndex < primitives.Count; ++primitiveIndex)
        {
            using var primitive = primitives[primitiveIndex];
            using var targets = primitive.Targets;
            if (morphCount >= 0 && morphCount != targets.Count)
                throw new InvalidDataException("glTF mesh primitives use incompatible morph targets.");
            morphCount = targets.Count;
        }
        return System.Math.Max(morphCount, 0);
    }

    private static List<Vector3f> ReadAnimationVectors(TinyGltfModel model, int accessorIndex, int expectedType,
        int animationIndex)
    {
        var values = ReadFloatAccessor(model, accessorIndex, expectedType);
        if (values.Count == 0)
            throw new InvalidDataException($"glTF animation #{animationIndex} has no values.");
        return values.Select(static value => new Vector3f(value[0], value[1], value[2])).ToList();
    }

    private static List<Quaternion> ReadAnimationRotations(TinyGltfModel model, int accessorIndex, int animationIndex)
    {
        var values = ReadFloatAccessor(model, accessorIndex, TypeVec4);
        if (values.Count == 0)
            throw new InvalidDataException($"glTF animation #{animationIndex} has no values.");
        return values.Select(static value => new Quaternion(value[3], value[0], value[1], value[2])).ToList();
    }

    private static List<T> ReadCubicSplineValues<T>(List<T> values, int keyCount, bool cubicSpline)
    {
        if (!cubicSpline)
            return values;
        if (values.Count != keyCount * 3)
            throw new InvalidDataException("glTF cubic spline animation channel input and output counts differ.");
        return values.Where((_, index) => index % 3 == 1).ToList();
    }

    private static void AddAnimationChannel<T>(TransformTrackSource track, List<float> times, List<T> values, bool step,
        Action<TransformTrackSource, ChannelSamples<T>> assign)
    {
        if (times.Count != values.Count)
            throw new InvalidDataException("glTF animation channel input and output counts differ.");
        var samples = new ChannelSamples<T>(times, values, step);
        assign(track, samples);
    }

    private void CreateAnimationTrack(RedBolt.Graphics.Animation animation, string name, TransformTrackSource source)
    {
        var times = MergeAnimationTimes(source.Position?.Times, source.Rotation?.Times, source.Scale?.Times);
        if (times.Count == 0)
            return;

        var track = animation.CreateTrack(name);
        if (source.Position is not null)
            track.ChannelMask |= RedBolt.Graphics.AnimationChannel.Position;
        if (source.Rotation is not null)
            track.ChannelMask |= RedBolt.Graphics.AnimationChannel.Rotation;
        if (source.Scale is not null)
            track.ChannelMask |= RedBolt.Graphics.AnimationChannel.Scale;
        foreach (var time in times)
        {
            var keyFrame = new AnimationKeyFrame { Time = time };
            if (source.Position is { } position)
            {
                keyFrame.Position = position.Sample(time, static (first, second, factor) => first.Lerp(second, factor)) * Settings.Scale;
                if (Settings.MirrorX)
                    keyFrame.Position.X = -keyFrame.Position.X;
            }
            if (source.Rotation is { } rotation)
            {
                keyFrame.Rotation = rotation.Sample(time, static (first, second, factor) => first.Slerp(second, factor));
                if (Settings.MirrorX)
                    keyFrame.Rotation = new Quaternion(keyFrame.Rotation.W, keyFrame.Rotation.X,
                        -keyFrame.Rotation.Y, -keyFrame.Rotation.Z);
            }
            if (source.Scale is { } scale)
                keyFrame.Scale = scale.Sample(time, static (first, second, factor) => first.Lerp(second, factor));
            track.KeyFrames.Add(keyFrame);
        }
        animation.SetLength(times[^1]);
    }

    private List<float> MergeAnimationTimes(params List<float>?[] sourceTimes)
    {
        var result = sourceTimes.Where(static times => times is not null).SelectMany(static times => times!).ToList();
        result.Sort();
        if (result.Count < 2)
            return result;

        var lastValidIndex = 0;
        for (var index = 1; index < result.Count; ++index)
        {
            if (result[index] - result[lastValidIndex] < Settings.KeyFrameTimeError)
                result[index] = -MathDefs.LargeValue;
            else
                lastValidIndex = index;
        }
        result.RemoveAll(static time => time < 0.0f);
        return result;
    }

    private void AddCookedResource(Resource resource, string outputPath, string localResourceName)
    {
        var fileName = Path.Combine(outputPath, localResourceName);
        _savedResources.Add(resource.Name, fileName);
        _cookedResources.Add((resource, fileName));
    }

    private void ClearCookedResources()
    {
        foreach (var modelView in _modelViews)
            modelView.Dispose();
        _modelViews.Clear();
        _modelView = null;
        _modelsByMesh.Clear();
        _boneNamesByNode.Clear();
        _materialsByName.Clear();
        foreach (var (resource, _) in _cookedResources)
            resource.Dispose();
        _cookedResources.Clear();
        foreach (var texture in _importedTextures)
            texture.Dispose();
        _importedTextures.Clear();
        _animations.Clear();
        _savedResources.Clear();
    }

    private static string CreateUniqueResourceName(string directory, string nameHint, string extension, ISet<string> usedNames)
    {
        var name = SanitizeResourceName(nameHint, "Resource");
        for (var index = 0; ; ++index)
        {
            var result = $"{directory}/{name}{(index == 0 ? string.Empty : $"_{index}")}{extension}";
            if (usedNames.Add(result))
                return result;
        }
    }

    private void Warn(string warning) => LastWarning = string.IsNullOrEmpty(LastWarning) ? warning : $"{LastWarning}\n{warning}";

    private sealed class TransformTrackSource
    {
        public ChannelSamples<Vector3f>? Position;
        public ChannelSamples<Quaternion>? Rotation;
        public ChannelSamples<Vector3f>? Scale;
    }

    private sealed class ChannelSamples<T>(List<float> times, List<T> values, bool step)
    {
        public List<float> Times { get; } = times;
        private List<T> Values { get; } = values;
        private bool Step { get; } = step;

        public T Sample(float time, Func<T, T, float, T> interpolate)
        {
            var index = Times.BinarySearch(time);
            if (index >= 0)
                return Values[index];
            index = ~index;
            if (index == 0)
                return Values[0];
            if (index == Times.Count)
                return Values[^1];
            if (Step)
                return Values[index - 1];
            var factor = (time - Times[index - 1]) / (Times[index] - Times[index - 1]);
            return interpolate(Values[index - 1], Values[index], factor);
        }
    }

    private List<string> ImportMaterials(TinyGltfModel model, string outputPath, string resourceNamePrefix)
    {
        using var sourceMaterials = model.Materials;
        var resourceNames = new List<string>(sourceMaterials.Count);
        var usedResourceNames = new HashSet<string>(StringComparer.Ordinal);
        var importedTextures = new Dictionary<int, ImportedTexture>();
        var usedTextureNames = new HashSet<string>(StringComparer.Ordinal);
        for (var materialIndex = 0; materialIndex < sourceMaterials.Count; ++materialIndex)
        {
            using var source = sourceMaterials[materialIndex];
            using var sourceName = source.Name;
            var localResourceName = CreateUniqueResourceName("Materials",
                string.IsNullOrEmpty(sourceName.ToString()) ? $"Material_{materialIndex}" : sourceName.ToString(),
                ".xml", usedResourceNames);
            var material = new RedBolt.Graphics.Material();
            material.SetName(resourceNamePrefix + localResourceName);
            InitializeMaterialTechnique(material, source);
            using var baseColor = source.PbrMetallicRoughnessBaseColorFactor;
            if (baseColor.Count == 4)
            {
                var color = new Color(new Vector4f((float)baseColor[0], (float)baseColor[1], (float)baseColor[2], (float)baseColor[3]));
                material.SetShaderParameter("MatDiffColor", color.LinearToGamma().ToVector4());
            }
            material.SetShaderParameter("Metallic", (float)source.PbrMetallicRoughnessMetallicFactor);
            material.SetShaderParameter("Roughness", (float)source.PbrMetallicRoughnessRoughnessFactor);
            material.SetShaderParameter("AlphaCutoff", (float)source.AlphaCutoff);
            SetMaterialTexture(material, ShaderResources.Albedo, source.PbrMetallicRoughnessBaseColorTextureIndex,
                model, outputPath, resourceNamePrefix, importedTextures, usedTextureNames);
            var occlusionTextureIndex = source.OcclusionTextureIndex;
            if (source.PbrMetallicRoughnessMetallicRoughnessTextureIndex >= 0 && occlusionTextureIndex >= 0
                && source.PbrMetallicRoughnessMetallicRoughnessTextureTexCoord != source.OcclusionTextureTexCoord)
            {
                Warn($"Material '{sourceName}' uses different UVs for metallic-roughness and occlusion textures; occlusion is ignored.");
                occlusionTextureIndex = -1;
            }
            SetPropertiesTexture(material, source.PbrMetallicRoughnessMetallicRoughnessTextureIndex,
                occlusionTextureIndex, model, outputPath, resourceNamePrefix, importedTextures, usedTextureNames);
            if (occlusionTextureIndex >= 0)
                material.SetShaderParameter("OcclusionStrength", (float)source.OcclusionTextureStrength);
            SetMaterialTexture(material, ShaderResources.Normal, source.NormalTextureIndex,
                model, outputPath, resourceNamePrefix, importedTextures, usedTextureNames);
            material.SetShaderParameter("NormalScale", (float)source.NormalTextureScale);
            using var emissiveFactor = source.EmissiveFactor;
            if (emissiveFactor.Count == 3)
            {
                var color = new Color(new Vector3f((float)emissiveFactor[0], (float)emissiveFactor[1], (float)emissiveFactor[2]));
                material.SetShaderParameter("MatEmissiveColor", color.LinearToGamma().ToVector3());
            }
            SetMaterialTexture(material, ShaderResources.Emission, source.EmissiveTextureIndex,
                model, outputPath, resourceNamePrefix, importedTextures, usedTextureNames);
            if (source.DoubleSided)
            {
                material.SetCullMode(CullMode.None);
                material.SetShadowCullMode(CullMode.None);
            }
            AddCookedResource(material, outputPath, localResourceName);
            _materialsByName.Add(material.Name, material);
            resourceNames.Add(material.Name);
        }
        return resourceNames;
    }

    private void InitializeMaterialTechnique(RedBolt.Graphics.Material material, RedBolt.Bindings.TinyGltf.Material source)
    {
        using var alphaMode = source.AlphaMode;
        var isOpaque = alphaMode.ToString() == "OPAQUE";
        var isAlphaMask = alphaMode.ToString() == "MASK";
        var isTransparent = alphaMode.ToString() == "BLEND";
        if (!isOpaque && !isAlphaMask && !isTransparent)
            throw new InvalidDataException($"glTF material has an unknown alpha mode '{alphaMode}'.");

        using var extensionsJson = source.ExtensionsJsonString;
        var isUnlit = extensionsJson.ToString().Contains("KHR_materials_unlit", StringComparison.Ordinal);
        var normalMap = source.NormalTextureIndex >= 0 && !isUnlit;
        var techniqueName = isUnlit
            ? isTransparent ? "Techniques/UnlitTransparent.xml" : "Techniques/UnlitOpaque.xml"
            : normalMap
                ? isTransparent
                    ? Settings.FadeTransparency ? "Techniques/LitTransparentFadeNormalMap.xml" : "Techniques/LitTransparentNormalMap.xml"
                    : "Techniques/LitOpaqueNormalMap.xml"
                : isTransparent
                    ? Settings.FadeTransparency ? "Techniques/LitTransparentFade.xml" : "Techniques/LitTransparent.xml"
                    : "Techniques/LitOpaque.xml";
        var technique = GetSubsystem<ResourceCache>()?.GetResourceOrNull<Technique>(techniqueName)
            ?? throw new InvalidDataException($"Cannot find standard technique '{techniqueName}'.");
        material.SetTechnique(0, technique, normalMap ? MaterialQuality.Medium : MaterialQuality.Low);
        if (normalMap)
        {
            var fallbackTechniqueName = isTransparent
                ? Settings.FadeTransparency ? "Techniques/LitTransparentFade.xml" : "Techniques/LitTransparent.xml"
                : "Techniques/LitOpaque.xml";
            var fallbackTechnique = GetSubsystem<ResourceCache>()?.GetResourceOrNull<Technique>(fallbackTechniqueName)
                ?? throw new InvalidDataException($"Cannot find standard technique '{fallbackTechniqueName}'.");
            material.SetTechnique(1, fallbackTechnique, MaterialQuality.Low);
        }

        var shaderDefines = isAlphaMask ? "ALPHAMASK " : string.Empty;
        if (!isUnlit)
            shaderDefines += "PBR ";
        material.SetVertexShaderDefines(shaderDefines);
        material.SetPixelShaderDefines(shaderDefines);
    }

    private void SetMaterialTexture(RedBolt.Graphics.Material material, string slot, int textureIndex,
        TinyGltfModel model, string outputPath, string resourceNamePrefix, Dictionary<int, ImportedTexture> importedTextures,
        ISet<string> usedTextureNames)
    {
        if (textureIndex < 0)
            return;
        if (!importedTextures.TryGetValue(textureIndex, out var texture))
        {
            texture = ImportTexture(model, textureIndex, outputPath, resourceNamePrefix, usedTextureNames);
            importedTextures.Add(textureIndex, texture);
        }
        material.SetTexture(slot, texture.Texture);
    }

    private ImportedTexture ImportTexture(TinyGltfModel model, int textureIndex, string outputPath, string resourceNamePrefix,
        ISet<string> usedTextureNames)
    {
        using var sourceTextures = model.Textures;
        if ((uint)textureIndex >= (uint)sourceTextures.Count)
            throw new InvalidDataException($"Material references invalid texture #{textureIndex}.");
        using var sourceTexture = sourceTextures[textureIndex];
        using var sourceImages = model.Images;
        if ((uint)sourceTexture.Source >= (uint)sourceImages.Count)
            throw new InvalidDataException($"Texture #{textureIndex} references invalid image #{sourceTexture.Source}.");
        using var sourceImage = sourceImages[sourceTexture.Source];
        using var imageData = sourceImage.ImageValue;
        var pixels = imageData.ToArray();
        var components = sourceImage.Component;

        using var sourceName = sourceImage.Name;
        using var sourceUri = sourceImage.Uri;
        var nameHint = sourceName.ToString();
        if (string.IsNullOrEmpty(nameHint) && !sourceUri.ToString().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            nameHint = Path.GetFileNameWithoutExtension(sourceUri.ToString());
        var localResourceName = CreateUniqueResourceName("Textures",
            string.IsNullOrEmpty(nameHint) ? $"Texture_{textureIndex}" : nameHint,
            ".png", usedTextureNames);
        var image = new RedBolt.Resource.Image();
        image.SetName(resourceNamePrefix + localResourceName);
        if (sourceImage.AsIs)
        {
            using var source = new MemoryBuffer(pixels);
            if (!image.Load(source))
            {
                image.Dispose();
                throw new InvalidDataException($"Texture #{textureIndex} has invalid image data.");
            }
        }
        else if (sourceImage.Width <= 0 || sourceImage.Height <= 0 || components is < 1 or > 4
            || pixels.Length < checked(sourceImage.Width * sourceImage.Height * components)
            || !image.SetSize(sourceImage.Width, sourceImage.Height, components))
        {
            image.Dispose();
            throw new InvalidDataException($"Texture #{textureIndex} has invalid decoded image data.");
        }
        else
            image.SetData(pixels);
        AddCookedResource(image, outputPath, localResourceName);
        CookSamplerParameters(model, sourceTexture, outputPath, resourceNamePrefix, localResourceName);

        var texture = new Texture2D();
        texture.SetName(image.Name);
        if (Settings.GpuResources && !texture.SetData(image))
        {
            texture.Dispose();
            throw new InvalidDataException($"Texture #{textureIndex} could not be uploaded.");
        }
        _importedTextures.Add(texture);
        return new ImportedTexture(texture, image);
    }

    private void SetPropertiesTexture(RedBolt.Graphics.Material material, int metallicRoughnessTextureIndex,
        int occlusionTextureIndex, TinyGltfModel model, string outputPath, string resourceNamePrefix,
        Dictionary<int, ImportedTexture> importedTextures, ISet<string> usedTextureNames)
    {
        if (metallicRoughnessTextureIndex < 0 && occlusionTextureIndex < 0)
            return;
        var metallicRoughness = GetImportedTexture(metallicRoughnessTextureIndex);
        var occlusion = GetImportedTexture(occlusionTextureIndex);
        var width = System.Math.Max(metallicRoughness?.Image.Width ?? 0, occlusion?.Image.Width ?? 0);
        var height = System.Math.Max(metallicRoughness?.Image.Height ?? 0, occlusion?.Image.Height ?? 0);
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("Metallic-roughness-occlusion texture has invalid dimensions.");

        var source = metallicRoughness ?? occlusion!;
        var localResourceName = CreateUniqueResourceName("Textures", $"{Path.GetFileNameWithoutExtension(source.Image.Name)}_Properties",
            ".png", usedTextureNames);
        var image = new RedBolt.Resource.Image();
        image.SetName(resourceNamePrefix + localResourceName);
        image.SetSize(width, height, occlusion is null ? 3 : 4);
        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                var color = metallicRoughness is { } metallic ? (SamplePixel(metallic.Image, x, y, width, height) >> 8) & 0xffffu : 0xffffu;
                color |= occlusion is { } occlusionImage ? (SamplePixel(occlusionImage.Image, x, y, width, height) & 0xffu) << 24 : 0xff000000u;
                image.SetPixelInt(x, y, color);
            }
        }
        AddCookedResource(image, outputPath, localResourceName);

        var texture = new Texture2D();
        texture.SetName(image.Name);
        if (Settings.GpuResources && !texture.SetData(image))
        {
            texture.Dispose();
            throw new InvalidDataException("Metallic-roughness-occlusion texture could not be uploaded.");
        }
        _importedTextures.Add(texture);
        material.SetTexture(ShaderResources.Properties, texture);

        ImportedTexture? GetImportedTexture(int index)
        {
            if (index < 0)
                return null;
            if (!importedTextures.TryGetValue(index, out var texture))
            {
                texture = ImportTexture(model, index, outputPath, resourceNamePrefix, usedTextureNames);
                importedTextures.Add(index, texture);
            }
            return texture;
        }
    }

    private static uint SamplePixel(RedBolt.Resource.Image image, int x, int y, int width, int height) =>
        image.GetPixelInt(x * image.Width / width, y * image.Height / height);

    private void CookSamplerParameters(TinyGltfModel model, RedBolt.Bindings.TinyGltf.Texture sourceTexture,
        string outputPath, string resourceNamePrefix, string imageResourceName)
    {
        if (sourceTexture.Sampler < 0)
            return;
        using var samplers = model.Samplers;
        if ((uint)sourceTexture.Sampler >= (uint)samplers.Count)
            throw new InvalidDataException($"Texture references invalid sampler #{sourceTexture.Sampler}.");
        using var sampler = samplers[sourceTexture.Sampler];
        var parameters = new XMLFile();
        var root = parameters.CreateRoot("texture");
        AddAddress(root, "u", sampler.WrapS);
        AddAddress(root, "v", sampler.WrapT);
        if (GetFilterMode(sampler.MinFilter, sampler.MagFilter) is { } filterMode)
            root.CreateChild("filter").SetAttribute("mode", filterMode);
        if (!HasMipmaps(sampler.MinFilter, sampler.MagFilter))
            root.CreateChild("mipmap").SetBool("enable", false);
        if (!root.GetChild().NotNull)
        {
            parameters.Dispose();
            return;
        }

        var localResourceName = Path.ChangeExtension(imageResourceName, ".xml");
        parameters.SetName(resourceNamePrefix + localResourceName);
        AddCookedResource(parameters, outputPath, localResourceName);
    }

    private static void AddAddress(XMLElement root, string coordinate, int sourceMode)
    {
        var mode = sourceMode switch
        {
            33071 => "clamp",
            33648 => "mirror",
            _ => "wrap"
        };
        if (mode != "wrap")
        {
            var address = root.CreateChild("address");
            address.SetAttribute("coord", coordinate);
            address.SetAttribute("mode", mode);
        }
    }

    private static string? GetFilterMode(int minFilter, int magFilter)
    {
        if (minFilter < 0 || magFilter < 0)
            return null;
        if (magFilter == 9728)
            return minFilter is 9728 or 9984 ? "nearest" : "nearestanisotropic";
        return minFilter == 9985 ? "bilinear" : null;
    }

    private static bool HasMipmaps(int minFilter, int magFilter) => minFilter < 0 || magFilter < 0
        || minFilter is 9984 or 9985 or 9986 or 9987;

    private List<(List<(int Mesh, int Skin)> ModelPairs, ModelView View, IReadOnlyList<string> Materials, string NameHint)> BuildModelViews(
        TinyGltfModel model, IReadOnlyList<string> materialNames, IReadOnlyDictionary<int, List<BoneView>> skinBonesByIndex)
    {
        using var meshes = model.Meshes;
        using var sourceNodes = model.Nodes;
        var meshSkinPairs = new HashSet<(int Mesh, int Skin)>();
        for (var nodeIndex = 0; nodeIndex < sourceNodes.Count; ++nodeIndex)
        {
            using var node = sourceNodes[nodeIndex];
            if (node.Mesh < 0)
                continue;
            if ((uint)node.Mesh >= (uint)meshes.Count || node.Skin >= 0 && !skinBonesByIndex.ContainsKey(node.Skin))
                throw new InvalidDataException("glTF node references an invalid mesh or skin.");
            meshSkinPairs.Add((node.Mesh, node.Skin));
        }
        for (var meshIndex = 0; meshIndex < meshes.Count; ++meshIndex)
        {
            if (!meshSkinPairs.Any(pair => pair.Mesh == meshIndex))
                meshSkinPairs.Add((meshIndex, -1));
        }

        var modelSkinByPair = new Dictionary<(int Mesh, int Skin), int>();
        foreach (var pair in meshSkinPairs.OrderBy(static pair => pair.Skin < 0).ThenBy(static pair => pair.Mesh)
                     .ThenBy(static pair => pair.Skin))
        {
            var matchingSkin = -1;
            foreach (var existing in modelSkinByPair.Where(existing => existing.Key.Mesh == pair.Mesh))
            {
                if (pair.Skin < 0 || existing.Value >= 0
                    && AreSkinBonesCompatible(skinBonesByIndex[existing.Value], skinBonesByIndex[pair.Skin]))
                {
                    matchingSkin = existing.Value;
                    break;
                }
            }
            modelSkinByPair.Add(pair, matchingSkin < 0 ? pair.Skin : matchingSkin);
        }

        var meshGroups = new Dictionary<(string Name, int Skin), List<(int Index, float Distance)>>();
        foreach (var (pair, skinIndex) in modelSkinByPair)
        {
            using var mesh = meshes[pair.Mesh];
            using var name = mesh.Name;
            var groupName = $"#{pair.Mesh}";
            var distance = 0.0f;
            if (Settings.CombineLODs && TryParseLodName(name.ToString(), out var baseName, out var lodDistance))
            {
                groupName = baseName;
                distance = lodDistance;
            }
            if (!meshGroups.TryGetValue((groupName, skinIndex), out var group))
            {
                group = [];
                meshGroups.Add((groupName, skinIndex), group);
            }
            if (!group.Any(value => value.Index == pair.Mesh))
                group.Add((pair.Mesh, distance));
        }

        var result = new List<(List<(int Mesh, int Skin)>, ModelView, IReadOnlyList<string>, string)>();
        foreach (var ((groupName, skinIndex), group) in meshGroups.OrderBy(static pair => pair.Key.Name)
                     .ThenBy(static pair => pair.Key.Skin))
        {
            group.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
            var lodsByGeometry = new List<List<GeometryLODView>>();
            var materialsByGeometry = new List<string>();
            var morphWeights = new List<float>();
            var numMorphs = -1;
            foreach (var (meshIndex, distance) in group)
            {
                using var mesh = meshes[meshIndex];
                using var primitives = mesh.Primitives;
                using var weights = mesh.Weights;
                var meshMorphs = 0;
                if (primitives.Count != 0)
                {
                    using var firstPrimitive = primitives[0];
                    using var targets = firstPrimitive.Targets;
                    meshMorphs = targets.Count;
                }
                if (numMorphs >= 0 && numMorphs != meshMorphs)
                    throw new InvalidDataException("glTF LOD meshes use incompatible morph targets.");
                numMorphs = meshMorphs;
                if (weights.Count != 0 && weights.Count != meshMorphs)
                    throw new InvalidDataException("glTF mesh morph weight count does not match its targets.");
                if (morphWeights.Count == 0)
                    morphWeights.AddRange(weights.Select(static weight => (float)weight));
                while (lodsByGeometry.Count < primitives.Count)
                {
                    lodsByGeometry.Add([]);
                    materialsByGeometry.Add(string.Empty);
                }
                for (var primitiveIndex = 0; primitiveIndex < primitives.Count; ++primitiveIndex)
                {
                    using var primitive = primitives[primitiveIndex];
                    var lod = BuildPrimitive(model, primitive, meshMorphs);
                    lod.LodDistance = distance;
                    lodsByGeometry[primitiveIndex].Add(lod);
                    if (primitive.Material >= 0)
                    {
                        if ((uint)primitive.Material >= (uint)materialNames.Count)
                            throw new InvalidDataException("glTF primitive references an invalid material.");
                        if (materialsByGeometry[primitiveIndex].Length == 0)
                            materialsByGeometry[primitiveIndex] = materialNames[primitive.Material];
                    }
                }
            }
            numMorphs = System.Math.Max(numMorphs, 0);
            var view = new ModelView();
            view.SetName(groupName.StartsWith('#') ? Settings.AssetName : groupName);
            view.SetMorphs(Enumerable.Range(0, numMorphs).Select(index => new ModelMorphView
            {
                Name = string.Empty,
                InitialWeight = index < morphWeights.Count ? morphWeights[index] : 0.0f
            }).ToList());
            view.SetGeometries(lodsByGeometry.Select((lods, index) => new GeometryView
            {
                Lods = lods,
                Material = materialsByGeometry[index]
            }).ToList());
            if (skinIndex >= 0)
                view.SetBones(skinBonesByIndex[skinIndex]);
            if (Settings.MirrorX)
                view.MirrorGeometriesX();
            if (Settings.Scale != 1.0f)
                view.ScaleGeometries(Settings.Scale);
            view.CalculateMissingNormals(flatNormals: true);
            view.CalculateMissingTangents();
            view.RecalculateBoneBoundingBoxes();
            view.RepairBoneWeights();
            view.Normalize();
            var modelPairs = modelSkinByPair.Where(pair => pair.Value == skinIndex
                    && group.Any(mesh => mesh.Index == pair.Key.Mesh))
                .Select(static pair => pair.Key).ToList();
            result.Add((modelPairs, view, materialsByGeometry,
                groupName.StartsWith('#') ? Settings.AssetName : groupName));
        }
        return result;
    }

    private Dictionary<int, List<BoneView>> BuildSkinBones(TinyGltfModel model)
    {
        using var sourceNodes = model.Nodes;
        var parents = Enumerable.Repeat(-1, sourceNodes.Count).ToArray();
        for (var nodeIndex = 0; nodeIndex < sourceNodes.Count; ++nodeIndex)
        {
            using var sourceNode = sourceNodes[nodeIndex];
            using var children = sourceNode.Children;
            foreach (var childIndex in children)
            {
                if ((uint)childIndex >= (uint)parents.Length || parents[childIndex] >= 0)
                    throw new InvalidDataException("glTF skin hierarchy has invalid or shared nodes.");
                parents[childIndex] = nodeIndex;
            }
        }

        using var skins = model.Skins;
        var result = new Dictionary<int, List<BoneView>>();
        for (var nodeIndex = 0; nodeIndex < sourceNodes.Count; ++nodeIndex)
        {
            using var meshNode = sourceNodes[nodeIndex];
            if (meshNode.Skin < 0)
                continue;
            if (meshNode.Mesh < 0 || (uint)meshNode.Skin >= (uint)skins.Count)
                throw new InvalidDataException("glTF skinned node references an invalid mesh or skin.");
            if (result.ContainsKey(meshNode.Skin))
                continue;
            using var skin = skins[meshNode.Skin];
            result.Add(meshNode.Skin, BuildSkinBones(model, sourceNodes, parents, skin));
        }
        return result;
    }

    private List<BoneView> BuildSkinBones(TinyGltfModel model, Std.Vector<TinyGltfNode> sourceNodes, IReadOnlyList<int> parents,
        RedBolt.Bindings.TinyGltf.Skin skin)
    {
        using var joints = skin.Joints;
        if (joints.Count == 0)
            throw new InvalidDataException("glTF skin has no joints.");
        var offsetMatrices = skin.InverseBindMatrices >= 0
            ? ReadMatrix4Accessor(model, skin.InverseBindMatrices)
            : [];
        if (offsetMatrices.Count != 0 && offsetMatrices.Count != joints.Count)
            throw new InvalidDataException("glTF skin inverse bind matrix count does not match its joints.");
        var jointToBone = new Dictionary<int, int>();
        for (var boneIndex = 0; boneIndex < joints.Count; ++boneIndex)
        {
            var jointIndex = joints[boneIndex];
            if ((uint)jointIndex >= (uint)sourceNodes.Count || !jointToBone.TryAdd(jointIndex, boneIndex))
                throw new InvalidDataException("glTF skin references an invalid or duplicate joint.");
        }

        var skeletonNodes = new HashSet<int>(jointToBone.Keys);
        var skeletonRoot = GetSkinRoot(skin, jointToBone.Keys, parents);
        foreach (var jointIndex in jointToBone.Keys)
        {
            for (var nodeIndex = jointIndex; nodeIndex != skeletonRoot; nodeIndex = parents[nodeIndex])
            {
                if (nodeIndex < 0)
                    throw new InvalidDataException("glTF skin joint is not below its skeleton root.");
                skeletonNodes.Add(nodeIndex);
            }
            skeletonNodes.Add(skeletonRoot);
        }
        if (Settings.AddEmptyNodesToSkeleton)
            AppendEmptySkeletonNodes(sourceNodes, skeletonNodes);
        var boneNodes = jointToBone.Keys.Concat(Enumerable.Range(0, sourceNodes.Count)
            .Where(nodeIndex => skeletonNodes.Contains(nodeIndex) && !jointToBone.ContainsKey(nodeIndex))).ToList();
        var nodeToBone = boneNodes.Select((nodeIndex, boneIndex) => (nodeIndex, boneIndex))
            .ToDictionary(static pair => pair.nodeIndex, static pair => pair.boneIndex);

        var result = new List<BoneView>(boneNodes.Count);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var boneIndex = 0; boneIndex < boneNodes.Count; ++boneIndex)
        {
            var nodeIndex = boneNodes[boneIndex];
            using var node = sourceNodes[nodeIndex];
            using var sourceName = node.Name;
            var nameHint = string.IsNullOrEmpty(sourceName.ToString()) ? $"Bone_{nodeIndex}" : sourceName.ToString();
            var name = nameHint;
            for (var suffix = 1; !usedNames.Add(name); ++suffix)
                name = $"{nameHint}_{suffix}";
            var transform = ReadNodeTransform(node);
            var bone = new BoneView
            {
                Name = name,
                ParentIndex = nodeToBone.GetValueOrDefault(parents[nodeIndex], -1),
                Position = ConvertScenePosition(transform.Position),
                Rotation = ConvertSceneRotation(transform.Rotation),
                Scale = transform.Scale
            };
            if (boneIndex < joints.Count && boneIndex < offsetMatrices.Count)
                bone.OffsetMatrix = offsetMatrices[boneIndex];
            result.Add(bone);
        }
        if (Settings.CleanupBoneNames)
            CleanupBoneNames(result);
        for (var boneIndex = 0; boneIndex < boneNodes.Count; ++boneIndex)
            _boneNamesByNode.TryAdd(boneNodes[boneIndex], result[boneIndex].Name);
        return result;
    }

    private bool AreSkinBonesCompatible(IReadOnlyList<BoneView> first, IReadOnlyList<BoneView> second)
    {
        if (first.Count != second.Count)
            return false;
        for (var index = 0; index < first.Count; ++index)
        {
            if (first[index].Name != second[index].Name || first[index].ParentIndex != second[index].ParentIndex
                || !first[index].OffsetMatrix.ApproximatelyEquivalent(second[index].OffsetMatrix, Settings.OffsetMatrixError))
                return false;
        }
        return true;
    }

    private static void CleanupBoneNames(List<BoneView> bones)
    {
        var prefixLength = FindCommonPrefixWord(bones.Select(static bone => bone.Name).ToList());
        if (prefixLength == 0)
            return;
        for (var index = 0; index < bones.Count; ++index)
        {
            var bone = bones[index];
            bone.Name = bone.Name[prefixLength..];
            bones[index] = bone;
        }
    }

    private static int FindCommonPrefixWord(IReadOnlyList<string> names)
    {
        if (names.Count <= 1)
            return 0;
        var prefix = names[0][..FindCommonPrefixWord(names[0], names[1])];
        for (var index = 2; index < names.Count; ++index)
            prefix = prefix[..FindCommonPrefixWord(prefix, names[index], preserveNonEmptyLeft: false)];
        return prefix.Length;
    }

    private static int FindCommonPrefixWord(string left, string right, bool preserveNonEmptyLeft = true,
        bool preserveNonEmptyRight = true)
    {
        if (left.Length == 0 || right.Length == 0)
            return 0;
        var maxLength = System.Math.Min(left.Length - (preserveNonEmptyLeft ? 1 : 0),
            right.Length - (preserveNonEmptyRight ? 1 : 0));
        var length = 0;
        while (length < maxLength && left[length] == right[length])
            ++length;
        while (length > 0)
        {
            if (length < left.Length && IsWordBorder(left[length - 1], left[length]))
                break;
            --length;
        }
        return length;
    }

    private static bool IsWordBorder(char first, char second) => char.IsWhiteSpace(first) || char.IsPunctuation(first)
        || char.IsLetter(first) != char.IsLetter(second) || char.IsLower(first) && char.IsUpper(second);

    private static void AppendEmptySkeletonNodes(Std.Vector<TinyGltfNode> sourceNodes, ISet<int> skeletonNodes)
    {
        foreach (var nodeIndex in skeletonNodes.ToArray())
        {
            using var node = sourceNodes[nodeIndex];
            using var children = node.Children;
            foreach (var childIndex in children)
            {
                if ((uint)childIndex < (uint)sourceNodes.Count && !skeletonNodes.Contains(childIndex)
                    && IsRecursivelyEmpty(sourceNodes, childIndex, new HashSet<int>()))
                    AddNodeSubtree(sourceNodes, childIndex, skeletonNodes);
            }
        }
    }

    private static bool IsRecursivelyEmpty(Std.Vector<TinyGltfNode> sourceNodes, int nodeIndex, ISet<int> visiting)
    {
        if (!visiting.Add(nodeIndex))
            return false;
        using var node = sourceNodes[nodeIndex];
        if (node.Mesh >= 0 || node.Skin >= 0)
            return false;
        using var children = node.Children;
        return children.All(childIndex => (uint)childIndex < (uint)sourceNodes.Count
            && IsRecursivelyEmpty(sourceNodes, childIndex, visiting));
    }

    private static void AddNodeSubtree(Std.Vector<TinyGltfNode> sourceNodes, int nodeIndex, ISet<int> destination)
    {
        if (!destination.Add(nodeIndex))
            return;
        using var node = sourceNodes[nodeIndex];
        using var children = node.Children;
        foreach (var childIndex in children)
        {
            if ((uint)childIndex >= (uint)sourceNodes.Count)
                throw new InvalidDataException("glTF skin hierarchy references an invalid child node.");
            AddNodeSubtree(sourceNodes, childIndex, destination);
        }
    }

    private static int GetSkinRoot(RedBolt.Bindings.TinyGltf.Skin skin, IEnumerable<int> joints,
        IReadOnlyList<int> parents)
    {
        if (skin.Skeleton >= 0 && skin.Skeleton < parents.Count && joints.All(joint => IsDescendantOf(joint, skin.Skeleton, parents)))
            return skin.Skeleton;

        using var enumerator = joints.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new InvalidDataException("glTF skin has no joints.");
        var root = enumerator.Current;
        while (enumerator.MoveNext())
        {
            root = GetCommonParent(root, enumerator.Current, parents);
            if (root < 0)
                throw new InvalidDataException("glTF skin joints do not have a common root.");
        }
        return root;
    }

    private static bool IsDescendantOf(int nodeIndex, int ancestorIndex, IReadOnlyList<int> parents)
    {
        for (; nodeIndex >= 0; nodeIndex = parents[nodeIndex])
        {
            if (nodeIndex == ancestorIndex)
                return true;
        }
        return false;
    }

    private static int GetCommonParent(int first, int second, IReadOnlyList<int> parents)
    {
        var ancestors = new HashSet<int>();
        for (; first >= 0; first = parents[first])
            ancestors.Add(first);
        for (; second >= 0; second = parents[second])
        {
            if (ancestors.Contains(second))
                return second;
        }
        return -1;
    }

    private static Transform ReadNodeTransform(TinyGltfNode source)
    {
        using var matrix = source.Matrix;
        if (matrix.Count != 0)
        {
            if (matrix.Count != 16)
                throw new InvalidDataException("glTF node matrix does not contain 16 values.");
            Span<float> values = stackalloc float[16];
            for (var index = 0; index < values.Length; ++index)
                values[index] = (float)matrix[index];
            return Transform.FromMatrix3x4(new Matrix3x4(new Matrix4(values).Transpose));
        }

        var transform = new Transform();
        using var translation = source.Translation;
        if (translation.Count != 0)
        {
            if (translation.Count != 3)
                throw new InvalidDataException("glTF node translation does not contain 3 values.");
            transform.Position = new Vector3f((float)translation[0], (float)translation[1], (float)translation[2]);
        }
        using var rotation = source.Rotation;
        if (rotation.Count != 0)
        {
            if (rotation.Count != 4)
                throw new InvalidDataException("glTF node rotation does not contain 4 values.");
            transform.Rotation = new Quaternion((float)rotation[3], (float)rotation[0], (float)rotation[1], (float)rotation[2]);
        }
        using var scale = source.Scale;
        if (scale.Count != 0)
        {
            if (scale.Count != 3)
                throw new InvalidDataException("glTF node scale does not contain 3 values.");
            transform.Scale = new Vector3f((float)scale[0], (float)scale[1], (float)scale[2]);
        }
        return transform;
    }

    private static bool TryParseLodName(string name, out string baseName, out float distance)
    {
        var marker = name.LastIndexOf("_LOD", StringComparison.Ordinal);
        if (marker > 0 && float.TryParse(name.AsSpan(marker + 4), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out distance))
        {
            baseName = name[..marker];
            return true;
        }
        baseName = string.Empty;
        distance = 0.0f;
        return false;
    }

    private static GeometryLODView BuildPrimitive(TinyGltfModel model, TinyGltfPrimitive primitive, int expectedMorphs)
    {
        using var attributes = primitive.Attributes;
        var positionAccessor = GetAttribute(attributes, "POSITION", required: true);
        var normalAccessor = GetAttribute(attributes, "NORMAL", required: false);
        var tangentAccessor = GetAttribute(attributes, "TANGENT", required: false);
        var uvAccessors = Enumerable.Range(0, ModelVertex.MaxUVs)
            .Select(index => GetAttribute(attributes, $"TEXCOORD_{index}", required: false)).ToArray();
        var colorAccessors = Enumerable.Range(0, ModelVertex.MaxColors)
            .Select(index => GetAttribute(attributes, $"COLOR_{index}", required: false)).ToArray();
        var jointsAccessor = GetAttribute(attributes, "JOINTS_0", required: false);
        var weightsAccessor = GetAttribute(attributes, "WEIGHTS_0", required: false);
        var positions = ReadFloatAccessor(model, positionAccessor, TypeVec3);
        var normals = normalAccessor >= 0 ? ReadFloatAccessor(model, normalAccessor, TypeVec3) : null;
        var tangents = tangentAccessor >= 0 ? ReadFloatAccessor(model, tangentAccessor, TypeVec4) : null;
        var uvs = uvAccessors.Select(accessor => accessor >= 0 ? ReadFloatAccessor(model, accessor, TypeVec2) : null).ToArray();
        var colors = new (List<float[]> Values, int Components)?[ModelVertex.MaxColors];
        for (var colorIndex = 0; colorIndex < colors.Length; ++colorIndex)
        {
            if (colorAccessors[colorIndex] >= 0)
                colors[colorIndex] = ReadColorAccessor(model, colorAccessors[colorIndex]);
        }
        var joints = jointsAccessor >= 0 ? ReadFloatAccessor(model, jointsAccessor, TypeVec4) : null;
        var weights = weightsAccessor >= 0 ? ReadFloatAccessor(model, weightsAccessor, TypeVec4) : null;
        if ((normals is not null && normals.Count != positions.Count) ||
            (tangents is not null && tangents.Count != positions.Count) ||
            uvs.Any(uv => uv is not null && uv.Count != positions.Count) ||
            colors.Any(color => color is { } value && value.Values.Count != positions.Count) ||
            (joints is not null && joints.Count != positions.Count) ||
            (weights is not null && weights.Count != positions.Count))
            throw new InvalidDataException("glTF primitive attributes have incompatible vertex counts.");

        var result = new GeometryLODView { PrimitiveType = ConvertPrimitiveType(primitive.Mode) };
        result.VertexFormat.Position = VertexElementType.Vector3;
        if (normals is not null)
            result.VertexFormat.Normal = VertexElementType.Vector3;
        if (tangents is not null)
            result.VertexFormat.Tangent = VertexElementType.Vector4;
        for (var uvIndex = 0; uvIndex < uvs.Length; ++uvIndex)
        {
            if (uvs[uvIndex] is not null)
                result.VertexFormat.Uv[uvIndex] = VertexElementType.Vector2;
        }
        for (var colorIndex = 0; colorIndex < colors.Length; ++colorIndex)
        {
            if (colors[colorIndex] is { } color)
                result.VertexFormat.Color[colorIndex] = color.Components == 3 ? VertexElementType.Vector3 : VertexElementType.Vector4;
        }
        if (joints is not null)
            result.VertexFormat.BlendIndices = VertexElementType.UByte4;
        if (weights is not null)
            result.VertexFormat.BlendWeights = VertexElementType.UByte4Norm;
        for (var index = 0; index < positions.Count; ++index)
        {
            var vertex = new ModelVertex();
            var position = positions[index];
            vertex.SetPosition(new Vector3f(position[0], position[1], position[2]));
            if (normals is not null)
            {
                var normal = normals[index];
                vertex.SetNormal(new Vector3f(normal[0], normal[1], normal[2]).Normalized);
            }
            if (tangents is not null)
            {
                var tangent = tangents[index];
                vertex.Tangent = new Vector4f(tangent[0], tangent[1], tangent[2], tangent[3]);
            }
            for (var uvIndex = 0; uvIndex < uvs.Length; ++uvIndex)
            {
                if (uvs[uvIndex] is { } uv)
                    vertex.Uv[uvIndex] = new Vector4f(uv[index][0], uv[index][1], 0, 0);
            }
            for (var colorIndex = 0; colorIndex < colors.Length; ++colorIndex)
            {
                if (colors[colorIndex] is { } color)
                {
                    var value = color.Values[index];
                    vertex.Color[colorIndex] = new Vector4f(value[0], value[1], value[2], color.Components == 3 ? 1 : value[3]);
                }
            }
            if (joints is not null)
                vertex.BlendIndices = ToVector4(joints[index]);
            if (weights is not null)
                vertex.BlendWeights = ToVector4(weights[index]);
            result.Vertices.Add(vertex);
        }

        using (var targets = primitive.Targets)
        {
            if (targets.Count != expectedMorphs)
                throw new InvalidDataException("glTF mesh primitives use incompatible morph targets.");
            for (var morphIndex = 0; morphIndex < targets.Count; ++morphIndex)
            {
                using var target = targets[morphIndex];
                result.Morphs[(uint)morphIndex] = ReadVertexMorphs(model, target, positions.Count);
            }
        }

        if (primitive.Indices >= 0)
        {
            var indices = ReadIndices(model, primitive.Indices);
            if (indices.Any(index => index >= positions.Count))
                throw new InvalidDataException("glTF primitive index is outside its vertex range.");
            result.Indices.AddRange(indices);
        }
        else
            result.Indices.AddRange(Enumerable.Range(0, positions.Count).Select(static index => checked((uint)index)));
        if (primitive.Mode == 2)
            result.Indices.Add(0);
        return result;
    }

    private static List<ModelVertexMorph> ReadVertexMorphs(TinyGltfModel model, Std.Map<Std.String, int> attributes,
        int vertexCount)
    {
        var positionAccessor = GetAttribute(attributes, "POSITION", required: false);
        var normalAccessor = GetAttribute(attributes, "NORMAL", required: false);
        var tangentAccessor = GetAttribute(attributes, "TANGENT", required: false);
        var positions = positionAccessor >= 0 ? ReadFloatAccessor(model, positionAccessor, TypeVec3) : null;
        var normals = normalAccessor >= 0 ? ReadFloatAccessor(model, normalAccessor, TypeVec3) : null;
        var tangents = tangentAccessor >= 0 ? ReadFloatAccessor(model, tangentAccessor, TypeVec3) : null;
        if ((positions is not null && positions.Count != vertexCount) ||
            (normals is not null && normals.Count != vertexCount) ||
            (tangents is not null && tangents.Count != vertexCount))
            throw new InvalidDataException("glTF morph target attributes have incompatible vertex counts.");

        return Enumerable.Range(0, vertexCount).Select(index => new ModelVertexMorph
        {
            Index = (uint)index,
            PositionDelta = positions is null ? Vector3f.Zero : new Vector3f(positions[index][0], positions[index][1], positions[index][2]),
            NormalDelta = normals is null ? Vector3f.Zero : new Vector3f(normals[index][0], normals[index][1], normals[index][2]),
            TangentDelta = tangents is null ? Vector3f.Zero : new Vector3f(tangents[index][0], tangents[index][1], tangents[index][2])
        }).ToList();
    }

    private static int GetAttribute(Std.Map<Std.String, int> attributes, string name, bool required)
    {
        using var key = new Std.String(name);
        if (attributes.TryGetValue(key, out var accessor))
            return accessor;
        if (required)
            throw new InvalidDataException($"glTF primitive is missing required {name} attribute.");
        return -1;
    }

    private static List<float[]> ReadFloatAccessor(TinyGltfModel model, int accessorIndex, int expectedType)
    {
        using var accessor = GetAccessor(model, accessorIndex);
        if (accessor.Type != expectedType)
            throw new InvalidDataException("glTF accessor has an unexpected value type.");

        var componentCount = GetComponentCount(expectedType);
        var count = checked((int)accessor.Count);
        var result = accessor.BufferView >= 0
            ? ReadFloatBufferView(model, accessor.BufferView, checked((int)accessor.ByteOffset), accessor.ComponentType,
                componentCount, count, accessor.Normalized)
            : Enumerable.Range(0, count).Select(_ => new float[componentCount]).ToList();
        ApplySparseFloatValues(model, accessor, result, componentCount);

        return result;
    }

    private static List<Matrix3x4> ReadMatrix4Accessor(TinyGltfModel model, int accessorIndex)
    {
        using var accessor = GetAccessor(model, accessorIndex);
        if (accessor.Type != TypeMat4 || accessor.ComponentType != ComponentFloat)
            throw new InvalidDataException("glTF inverse bind matrices have an unsupported accessor format.");
        var values = ReadFloatAccessor(model, accessorIndex, TypeMat4);
        return values.Select(static value => new Matrix3x4(new Matrix4(value).Transpose)).ToList();
    }

    private static (List<float[]> Values, int Components) ReadColorAccessor(TinyGltfModel model, int accessorIndex)
    {
        using var accessor = GetAccessor(model, accessorIndex);
        return accessor.Type switch
        {
            TypeVec3 => (ReadFloatAccessor(model, accessorIndex, TypeVec3), 3),
            TypeVec4 => (ReadFloatAccessor(model, accessorIndex, TypeVec4), 4),
            _ => throw new InvalidDataException("glTF vertex color has an unexpected value type.")
        };
    }

    private static List<uint> ReadIndices(TinyGltfModel model, int accessorIndex)
    {
        using var accessor = GetAccessor(model, accessorIndex);
        if (accessor.Type != TypeScalar)
            throw new InvalidDataException("glTF index accessor is not scalar.");
        if (accessor.ComponentType is not (ComponentUnsignedByte or ComponentUnsignedShort or ComponentUnsignedInt))
            throw new InvalidDataException("glTF index accessor has an unsupported component type.");

        var count = checked((int)accessor.Count);
        var result = accessor.BufferView >= 0
            ? ReadUnsignedBufferView(model, accessor.BufferView, checked((int)accessor.ByteOffset), accessor.ComponentType, count)
            : Enumerable.Repeat(0u, count).ToList();
        ApplySparseIndices(model, accessor, result);
        return result;
    }

    private static void ApplySparseFloatValues(TinyGltfModel model, TinyGltfAccessor accessor, List<float[]> destination,
        int componentCount)
    {
        if (!accessor.SparseIsSparse)
            return;
        var indices = ReadSparseIndices(model, accessor, destination.Count);
        var values = ReadFloatBufferView(model, accessor.SparseValuesBufferView, accessor.SparseValuesByteOffset,
            accessor.ComponentType, componentCount, indices.Count, accessor.Normalized);
        for (var index = 0; index < indices.Count; ++index)
            destination[(int)indices[index]] = values[index];
    }

    private static void ApplySparseIndices(TinyGltfModel model, TinyGltfAccessor accessor, List<uint> destination)
    {
        if (!accessor.SparseIsSparse)
            return;
        var indices = ReadSparseIndices(model, accessor, destination.Count);
        var values = ReadUnsignedBufferView(model, accessor.SparseValuesBufferView, accessor.SparseValuesByteOffset,
            accessor.ComponentType, indices.Count);
        for (var index = 0; index < indices.Count; ++index)
            destination[(int)indices[index]] = values[index];
    }

    private static List<uint> ReadSparseIndices(TinyGltfModel model, TinyGltfAccessor accessor, int destinationCount)
    {
        if (accessor.SparseCount < 0 || accessor.SparseCount > destinationCount)
            throw new InvalidDataException("glTF sparse accessor count is invalid.");
        var indices = ReadUnsignedBufferView(model, accessor.SparseIndicesBufferView, accessor.SparseIndicesByteOffset,
            accessor.SparseIndicesComponentType, accessor.SparseCount);
        if (indices.Any(index => index >= (uint)destinationCount))
            throw new InvalidDataException("glTF sparse accessor index is outside its accessor.");
        return indices;
    }

    private static List<float[]> ReadFloatBufferView(TinyGltfModel model, int bufferViewIndex, int byteOffset,
        int componentType, int componentCount, int count, bool normalized)
    {
        var componentSize = GetComponentSize(componentType);
        var bytes = GetBufferViewBytes(model, bufferViewIndex, byteOffset, componentSize * componentCount, count, out var stride);
        var result = new List<float[]>(count);
        for (var index = 0; index < count; ++index)
        {
            var value = new float[componentCount];
            var offset = checked(index * stride);
            for (var component = 0; component < componentCount; ++component)
                value[component] = ReadFloatComponent(bytes.AsSpan(offset + component * componentSize, componentSize), componentType, normalized);
            result.Add(value);
        }
        return result;
    }

    private static List<uint> ReadUnsignedBufferView(TinyGltfModel model, int bufferViewIndex, int byteOffset,
        int componentType, int count)
    {
        if (componentType is not (ComponentUnsignedByte or ComponentUnsignedShort or ComponentUnsignedInt))
            throw new InvalidDataException("glTF accessor has an unsupported unsigned component type.");
        var componentSize = GetComponentSize(componentType);
        var bytes = GetBufferViewBytes(model, bufferViewIndex, byteOffset, componentSize, count, out var stride);
        var result = new List<uint>(count);
        for (var index = 0; index < count; ++index)
        {
            var value = bytes.AsSpan(checked(index * stride), componentSize);
            result.Add(componentType switch
            {
                ComponentUnsignedByte => value[0],
                ComponentUnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(value),
                ComponentUnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(value),
                _ => throw new InvalidDataException("glTF accessor has an unsupported unsigned component type.")
            });
        }
        return result;
    }

    private static byte[] GetBufferViewBytes(TinyGltfModel model, int bufferViewIndex, int byteOffset,
        int elementSize, int count, out int stride)
    {
        using var bufferViews = model.BufferViews;
        if ((uint)bufferViewIndex >= (uint)bufferViews.Count)
            throw new InvalidDataException("glTF accessor references an invalid buffer view.");
        using var bufferView = bufferViews[bufferViewIndex];
        using var buffers = model.Buffers;
        if ((uint)bufferView.Buffer >= (uint)buffers.Count)
            throw new InvalidDataException("glTF buffer view references an invalid buffer.");
        using var buffer = buffers[bufferView.Buffer];
        using var data = buffer.Data;
        stride = bufferView.ByteStride == 0 ? elementSize : checked((int)bufferView.ByteStride);
        if (stride < elementSize)
            throw new InvalidDataException("glTF accessor stride is smaller than its element size.");
        var start = checked((int)bufferView.ByteOffset + byteOffset);
        var length = count == 0 ? 0 : checked((count - 1) * stride + elementSize);
        if ((nuint)byteOffset > bufferView.ByteLength || checked((nuint)length) > bufferView.ByteLength - (nuint)byteOffset ||
            start < 0 || length < 0 || start > data.Count - length)
            throw new InvalidDataException("glTF accessor is outside its buffer.");
        var result = new byte[length];
        for (var index = 0; index < length; ++index)
            result[index] = data[start + index];
        return result;
    }

    private static int GetComponentCount(int type) => type switch
    {
        TypeScalar => 1,
        TypeVec2 => 2,
        TypeVec3 => 3,
        TypeVec4 => 4,
        TypeMat4 => 16,
        _ => throw new InvalidDataException("glTF accessor has an unsupported value type.")
    };

    private static int GetComponentSize(int componentType) => componentType switch
    {
        ComponentUnsignedByte or ComponentByte => sizeof(byte),
        ComponentShort or ComponentUnsignedShort => sizeof(short),
        ComponentInt or ComponentUnsignedInt or ComponentFloat => sizeof(int),
        ComponentDouble => sizeof(double),
        _ => throw new InvalidDataException("glTF accessor has an unsupported component type.")
    };

    private static float ReadFloatComponent(ReadOnlySpan<byte> value, int componentType, bool normalized)
    {
        var result = componentType switch
        {
            ComponentByte => (float)(sbyte)value[0],
            ComponentUnsignedByte => value[0],
            ComponentShort => BinaryPrimitives.ReadInt16LittleEndian(value),
            ComponentUnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(value),
            ComponentInt => BinaryPrimitives.ReadInt32LittleEndian(value),
            ComponentUnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(value),
            ComponentFloat => BinaryPrimitives.ReadSingleLittleEndian(value),
            ComponentDouble => (float)BinaryPrimitives.ReadDoubleLittleEndian(value),
            _ => throw new InvalidDataException("glTF accessor has an unsupported component type.")
        };
        return normalized ? componentType switch
        {
            ComponentByte => System.Math.Max(-1.0f, result / sbyte.MaxValue),
            ComponentUnsignedByte => result / byte.MaxValue,
            ComponentShort => System.Math.Max(-1.0f, result / short.MaxValue),
            ComponentUnsignedShort => result / ushort.MaxValue,
            _ => result
        } : result;
    }

    private static Vector4f ToVector4(float[] value) => new(value[0], value[1], value[2], value[3]);

    private static TinyGltfAccessor GetAccessor(TinyGltfModel model, int index)
    {
        using var accessors = model.Accessors;
        if ((uint)index >= (uint)accessors.Count)
            throw new InvalidDataException("glTF primitive references an invalid accessor.");
        return accessors[index];
    }

    private static PrimitiveType ConvertPrimitiveType(int mode) => mode switch
    {
        0 => PrimitiveType.PointList,
        1 => PrimitiveType.LineList,
        2 => PrimitiveType.LineStrip,
        3 => PrimitiveType.LineStrip,
        4 => PrimitiveType.TriangleList,
        5 => PrimitiveType.TriangleStrip,
        6 => PrimitiveType.TriangleFan,
        _ => throw new InvalidDataException($"glTF primitive mode {mode} is not supported.")
    };

    private static GLTFImporterSettings CloneSettings(GLTFImporterSettings source) => new()
    {
        AssetName = source.AssetName,
        MirrorX = source.MirrorX,
        Scale = source.Scale,
        Rotation = source.Rotation,
        FadeTransparency = source.FadeTransparency,
        CleanupBoneNames = source.CleanupBoneNames,
        CleanupRootNodes = source.CleanupRootNodes,
        CombineLODs = source.CombineLODs,
        SkipTags = [.. source.SkipTags],
        KeepNamesOnMerge = source.KeepNamesOnMerge,
        AddEmptyNodesToSkeleton = source.AddEmptyNodesToSkeleton,
        OffsetMatrixError = source.OffsetMatrixError,
        KeyFrameTimeError = source.KeyFrameTimeError,
        NodeRenames = new(source.NodeRenames, StringComparer.Ordinal),
        GpuResources = source.GpuResources,
        Preview = new GLTFImporterSettings.PreviewOptions
        {
            AddLights = source.Preview.AddLights,
            AddSkybox = source.Preview.AddSkybox,
            SkyboxMaterial = source.Preview.SkyboxMaterial,
            AddReflectionProbe = source.Preview.AddReflectionProbe,
            ReflectionProbeCubemap = source.Preview.ReflectionProbeCubemap,
            HighRenderQuality = source.Preview.HighRenderQuality
        }
    };

    private static string SanitizeResourceName(string value, string fallback)
    {
        var result = string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_'));
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private void RenameNodes(TinyGltfModel model)
    {
        if (Settings.NodeRenames.Count == 0)
            return;

        using var nodes = model.Nodes;
        for (var index = 0; index < nodes.Count; ++index)
        {
            using var node = nodes[index];
            using var name = node.Name;
            if (!Settings.NodeRenames.TryGetValue(name.ToString(), out var replacement))
                continue;

            using var nativeReplacement = new Std.String(replacement);
            node.Name = nativeReplacement;
            nodes[index] = node;
        }
    }

    private static void MergeModels(TinyGltfModel outputModel, TinyGltfModel inputModel, string? overrideName)
    {
        var sourceToDestinationNodes = MapNodesByNames(inputModel, outputModel);

        using var outputBuffers = outputModel.Buffers;
        var startBufferIndex = outputBuffers.Count;
        using var inputBuffers = inputModel.Buffers;
        for (var index = 0; index < inputBuffers.Count; ++index)
        {
            using var buffer = inputBuffers[index];
            outputBuffers.Add(buffer);
        }

        using var outputBufferViews = outputModel.BufferViews;
        var startBufferViewIndex = outputBufferViews.Count;
        using var inputBufferViews = inputModel.BufferViews;
        for (var index = 0; index < inputBufferViews.Count; ++index)
        {
            using var bufferView = inputBufferViews[index];
            bufferView.Buffer += startBufferIndex;
            outputBufferViews.Add(bufferView);
        }

        using var outputAccessors = outputModel.Accessors;
        var startAccessorIndex = outputAccessors.Count;
        using var inputAccessors = inputModel.Accessors;
        for (var index = 0; index < inputAccessors.Count; ++index)
        {
            using var accessor = inputAccessors[index];
            accessor.BufferView += startBufferViewIndex;
            outputAccessors.Add(accessor);
        }

        using var outputAnimations = outputModel.Animations;
        var startAnimationIndex = outputAnimations.Count;
        using var inputAnimations = inputModel.Animations;
        for (var index = 0; index < inputAnimations.Count; ++index)
        {
            using var animation = inputAnimations[index];
            outputAnimations.Add(animation);
        }

        for (var animationIndex = startAnimationIndex; animationIndex < outputAnimations.Count; ++animationIndex)
        {
            using var animation = outputAnimations[animationIndex];
            if (overrideName is not null && inputAnimations.Count == 1)
            {
                using var name = new Std.String(overrideName);
                animation.Name = name;
            }

            using var channels = animation.Channels;
            for (var channelIndex = channels.Count - 1; channelIndex >= 0; --channelIndex)
            {
                using var channel = channels[channelIndex];
                if (sourceToDestinationNodes.TryGetValue(channel.TargetNode, out var destinationNode))
                {
                    channel.TargetNode = destinationNode;
                    channels[channelIndex] = channel;
                    continue;
                }

                channels.RemoveAt(channelIndex);
            }

            using var samplers = animation.Samplers;
            for (var samplerIndex = 0; samplerIndex < samplers.Count; ++samplerIndex)
            {
                using var sampler = samplers[samplerIndex];
                sampler.Input += startAccessorIndex;
                sampler.Output += startAccessorIndex;
                samplers[samplerIndex] = sampler;
            }
            outputAnimations[animationIndex] = animation;
        }
    }

    private static Dictionary<int, int> MapNodesByNames(TinyGltfModel sourceModel, TinyGltfModel destinationModel)
    {
        var destinationNodes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (index, name) in GetNodeNames(destinationModel))
        {
            if (name.Length != 0)
                destinationNodes.TryAdd(name, index);
        }

        var result = new Dictionary<int, int>();
        foreach (var (sourceIndex, name) in GetNodeNames(sourceModel))
        {
            if (name.Length != 0 && destinationNodes.TryGetValue(name, out var destinationIndex))
                result[sourceIndex] = destinationIndex;
        }
        return result;
    }

    private static Dictionary<int, string> GetNodeNames(TinyGltfModel model)
    {
        var result = new Dictionary<int, string>();
        using var nodes = model.Nodes;
        for (var index = 0; index < nodes.Count; ++index)
        {
            using var node = nodes[index];
            using var name = node.Name;
            result[index] = name.ToString();
        }
        return result;
    }
}
