namespace RedBolt.Utility;

using System.Buffers.Binary;
using System.Runtime.InteropServices;

using RedBolt.Bindings.TinyGltf;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.IO;
using RedBolt.RenderAPI;

using TinyGltfModel = RedBolt.Bindings.TinyGltf.Model;
using TinyGltfPrimitive = RedBolt.Bindings.TinyGltf.Primitive;
using TinyGltfAccessor = RedBolt.Bindings.TinyGltf.Accessor;

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
    public float KeyFrameTimeError = float.Epsilon;
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
        ArchiveSerialization.SerializeOptionalValue(archive, "keyFrameTimeError", ref KeyFrameTimeError, float.Epsilon, ArchiveSerialization.SerializeValue);
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
    private const int ComponentUnsignedByte = 5121;
    private const int ComponentUnsignedShort = 5123;
    private const int ComponentUnsignedInt = 5125;
    private const int ComponentFloat = 5126;
    private const int TypeScalar = 65;
    private const int TypeVec2 = 2;
    private const int TypeVec3 = 3;

    private TinyGltfModel? _model;
    private ModelView? _modelView;
    private bool _processed;

    public GLTFImporter(GLTFImporterSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public GLTFImporterSettings Settings { get; }
    public string LastError { get; private set; } = string.Empty;
    public string LastWarning { get; private set; } = string.Empty;
    public TinyGltfModel? SourceModel => _model;
    public ModelView? ImportedModel => _modelView;
    public IReadOnlyDictionary<string, string> SavedResources { get; } = new Dictionary<string, string>();

    /// <summary>Load a primary glTF or GLB file. A source model can only be loaded once.</summary>
    public bool LoadFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!System.IO.File.Exists(fileName))
            return Fail($"glTF file '{fileName}' does not exist.");
        if (!CanLoadPrimary())
            return false;

        using var loader = new TinyGLTF();
        using var error = new Std.String();
        using var warning = new Std.String();
        using var nativeFileName = new Std.String(fileName);
        var model = new TinyGltfModel();
        var loaded = string.Equals(Path.GetExtension(fileName), ".glb", StringComparison.OrdinalIgnoreCase)
            ? loader.LoadBinaryFromFile(model, error, warning, nativeFileName)
            : loader.LoadASCIIFromFile(model, error, warning, nativeFileName);
        LastWarning = warning.ToString();
        if (!loaded)
        {
            model.Dispose();
            return Fail(error.ToString());
        }

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
            using var error = new Std.String();
            using var warning = new Std.String();
            var model = new TinyGltfModel();
            var loaded = loader.LoadBinaryFromMemory(model, error, warning, pin.AddrOfPinnedObject(), checked((uint)bytes.Length));
            LastWarning = warning.ToString();
            if (!loaded)
            {
                model.Dispose();
                return Fail(error.ToString());
            }

            _model = model;
            return true;
        }
        finally
        {
            pin.Free();
        }
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
            var modelView = BuildModelView(_model);
            _modelView = modelView;
            _processed = true;
            callback?.OnModelLoaded(modelView);
            return true;
        }
        catch (InvalidDataException exception)
        {
            return Fail(exception.Message);
        }
    }

    /// <summary>Static mesh conversion does not create files; generated resources are immediately available from <see cref="ImportedModel"/>.</summary>
    public bool SaveResources() => _processed;

    public Transform ConvertTransform(Transform sourceTransform)
    {
        if (!_processed)
            return sourceTransform;

        var position = sourceTransform.Position * Settings.Scale;
        if (Settings.MirrorX)
            position.X = -position.X;
        return new Transform(Settings.Rotation * position, Settings.Rotation * sourceTransform.Rotation, sourceTransform.Scale);
    }

    public override void Dispose()
    {
        _modelView?.Dispose();
        _modelView = null;
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

    private bool Fail(string error)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? "TinyGLTF failed to load the document." : error;
        return false;
    }

    private ModelView BuildModelView(TinyGltfModel model)
    {
        var geometries = new List<GeometryView>();
        using var meshes = model.Meshes;
        for (var meshIndex = 0; meshIndex < meshes.Count; ++meshIndex)
        {
            var mesh = meshes[meshIndex];
            using var primitives = mesh.Primitives;
            foreach (var primitive in primitives)
            {
                var lod = BuildPrimitive(model, primitive);
                geometries.Add(new GeometryView { Lods = [lod] });
            }
        }

        var result = new ModelView();
        result.SetName(Settings.AssetName);
        result.SetGeometries(geometries);
        if (Settings.MirrorX)
            result.MirrorGeometriesX();
        if (Settings.Scale != 1.0f)
            result.ScaleGeometries(Settings.Scale);
        return result;
    }

    private static GeometryLODView BuildPrimitive(TinyGltfModel model, TinyGltfPrimitive primitive)
    {
        using var attributes = primitive.Attributes;
        var positionAccessor = GetAttribute(attributes, "POSITION", required: true);
        var normalAccessor = GetAttribute(attributes, "NORMAL", required: false);
        var uvAccessor = GetAttribute(attributes, "TEXCOORD_0", required: false);
        var positions = ReadFloatAccessor(model, positionAccessor, TypeVec3);
        var normals = normalAccessor >= 0 ? ReadFloatAccessor(model, normalAccessor, TypeVec3) : null;
        var uvs = uvAccessor >= 0 ? ReadFloatAccessor(model, uvAccessor, TypeVec2) : null;
        if ((normals is not null && normals.Count != positions.Count) ||
            (uvs is not null && uvs.Count != positions.Count))
            throw new InvalidDataException("glTF primitive attributes have incompatible vertex counts.");

        var result = new GeometryLODView { PrimitiveType = ConvertPrimitiveType(primitive.Mode) };
        result.VertexFormat.Position = VertexElementType.Vector3;
        if (normals is not null)
            result.VertexFormat.Normal = VertexElementType.Vector3;
        if (uvs is not null)
            result.VertexFormat.Uv[0] = VertexElementType.Vector2;
        for (var index = 0; index < positions.Count; ++index)
        {
            var vertex = new ModelVertex();
            var position = positions[index];
            vertex.SetPosition(new Vector3f(position[0], position[1], position[2]));
            if (normals is not null)
            {
                var normal = normals[index];
                vertex.SetNormal(new Vector3f(normal[0], normal[1], normal[2]));
            }
            if (uvs is not null)
            {
                var uv = uvs[index];
                vertex.Uv[0] = new Vector4f(uv[0], uv[1], 0, 0);
            }
            result.Vertices.Add(vertex);
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
        return result;
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
        var accessor = GetAccessor(model, accessorIndex);
        if (accessor.ComponentType != ComponentFloat || accessor.Type != expectedType)
            throw new InvalidDataException("glTF accessor has an unsupported component or value type.");

        var componentCount = expectedType == TypeVec3 ? 3 : 2;
        var bytes = GetAccessorBytes(model, accessor, componentCount * sizeof(float), out var count, out var stride);
        var result = new List<float[]>(count);
        for (var index = 0; index < count; ++index)
        {
            var value = new float[componentCount];
            var offset = checked(index * stride);
            for (var component = 0; component < componentCount; ++component)
                value[component] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset + component * sizeof(float), sizeof(float)));
            result.Add(value);
        }
        return result;
    }

    private static List<uint> ReadIndices(TinyGltfModel model, int accessorIndex)
    {
        var accessor = GetAccessor(model, accessorIndex);
        if (accessor.Type != TypeScalar)
            throw new InvalidDataException("glTF index accessor is not scalar.");
        var componentSize = accessor.ComponentType switch
        {
            ComponentUnsignedByte => sizeof(byte),
            ComponentUnsignedShort => sizeof(ushort),
            ComponentUnsignedInt => sizeof(uint),
            _ => throw new InvalidDataException("glTF index accessor has an unsupported component type.")
        };
        var bytes = GetAccessorBytes(model, accessor, componentSize, out var count, out var stride);
        var result = new List<uint>(count);
        for (var index = 0; index < count; ++index)
        {
            var offset = checked(index * stride);
            result.Add(accessor.ComponentType switch
            {
                ComponentUnsignedByte => bytes[offset],
                ComponentUnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort))),
                ComponentUnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint))),
                _ => 0
            });
        }
        return result;
    }

    private static byte[] GetAccessorBytes(TinyGltfModel model, TinyGltfAccessor accessor, int elementSize,
        out int count, out int stride)
    {
        if (accessor.BufferView < 0)
            throw new InvalidDataException("Sparse glTF accessors are not supported.");
        using var bufferViews = model.BufferViews;
        if ((uint)accessor.BufferView >= (uint)bufferViews.Count)
            throw new InvalidDataException("glTF accessor references an invalid buffer view.");
        var bufferView = bufferViews[accessor.BufferView];
        using var buffers = model.Buffers;
        if ((uint)bufferView.Buffer >= (uint)buffers.Count)
            throw new InvalidDataException("glTF buffer view references an invalid buffer.");
        var buffer = buffers[bufferView.Buffer];
        using var data = buffer.Data;
        count = checked((int)accessor.Count);
        stride = bufferView.ByteStride == 0 ? elementSize : checked((int)bufferView.ByteStride);
        if (stride < elementSize)
            throw new InvalidDataException("glTF accessor stride is smaller than its element size.");
        var start = checked((int)(bufferView.ByteOffset + accessor.ByteOffset));
        var length = count == 0 ? 0 : checked((count - 1) * stride + elementSize);
        if (accessor.ByteOffset > bufferView.ByteLength || checked((nuint)length) > bufferView.ByteLength - accessor.ByteOffset ||
            start < 0 || length < 0 || start > data.Count - length)
            throw new InvalidDataException("glTF accessor is outside its buffer.");
        var result = new byte[length];
        for (var index = 0; index < length; ++index)
            result[index] = data[start + index];
        return result;
    }

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
        3 => PrimitiveType.LineStrip,
        4 => PrimitiveType.TriangleList,
        5 => PrimitiveType.TriangleStrip,
        6 => PrimitiveType.TriangleFan,
        _ => throw new InvalidDataException($"glTF primitive mode {mode} is not supported.")
    };
}
