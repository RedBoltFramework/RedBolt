// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Tg = RedBolt.Bindings.TinyGltf;
using TgNative = RedBolt.Bindings.TinyGltf.TinyGltf;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;

internal static partial class GLTFImporterFunctions
{
    public const int MaxNameAssignTries = 64 * 1024;

    public static T[] ToArray<T>(Std.Vector<double> vector, int size)
    {
        var result = new T[size];
        for (var i = 0; i < Math.Min(checked((int)vector.Count), size); ++i)
            result[i] = (T)Convert.ChangeType(vector[i], typeof(T));
        return result;
    }

    public static bool IsNameSkipped(string name, List<string> skipTags) =>
        skipTags.Any(tag => name.Contains(tag, StringComparison.Ordinal));

    public static string GetSanitizedName(string name)
    {
        const string forbiddenSymbols = "<>:\"/\\|?*";
        return new string(name.Select(character => character < 31
            ? ' '
            : forbiddenSymbols.Contains(character, StringComparison.Ordinal) ? '_' : character).ToArray());
    }

    public static bool IsWordBorder(char first, char second)
    {
        // Blanks and punctuation is always considered to be a word border
        if (char.IsWhiteSpace(first) || char.IsPunctuation(first))
            return true;

        // If symbol class changed, it is the word border
        if (char.IsLetter(first) != char.IsLetter(second))
            return true;

        // If was lower case and became upper case, it is the word border
        if (char.IsLower(first) && char.IsUpper(second))
            return true;

        return false;
    }

    public static int FindCommonPrefixWord(string lhs, string rhs,
        bool preserveNonEmptyLeft = true, bool preserveNonEmptyRight = true)
    {
        if (lhs.Length == 0 || rhs.Length == 0)
            return 0;

        var minLength = Math.Min(lhs.Length - (preserveNonEmptyLeft ? 1 : 0),
            rhs.Length - (preserveNonEmptyRight ? 1 : 0));
        var index = 0;
        while (index < minLength && lhs[index] == rhs[index])
            ++index;

        while (index != 0)
        {
            var previous = index - 1;
            if (IsWordBorder(lhs[previous], lhs[index]))
                break;
            --index;
        }
        return index;
    }

    public static int FindCommonPrefixWord(List<string> strings)
    {
        if (strings.Count <= 1)
            return 0;

        var prefix = strings[0][..FindCommonPrefixWord(strings[0], strings[1])];
        for (var i = 2; i < strings.Count; ++i)
            prefix = prefix[..FindCommonPrefixWord(prefix, strings[i], false)];
        return prefix.Length;
    }

    public static bool IsNegativeScale(Vector3f scale) => scale.X * scale.Y * scale.Y < 0.0f;
    public static Vector3f MirrorX(Vector3f vector) => new(-vector.X, vector.Y, vector.Z);
    public static Quaternion RotationFromVector(Vector4f vector) => new(vector.W, vector.X, vector.Y, vector.Z);
    public static Quaternion MirrorX(Quaternion rotation) =>
        new(rotation.W, rotation.X, -rotation.Y, -rotation.Z);

    public static Matrix3x4 MirrorX(Matrix3x4 matrix)
    {
        matrix.M01 = -matrix.M01;
        matrix.M10 = -matrix.M10;
        matrix.M02 = -matrix.M02;
        matrix.M20 = -matrix.M20;
        matrix.M03 = -matrix.M03;
        return matrix;
    }

    public static Vector3f ConditionalMirrorX(Vector3f value, bool mirror) => mirror ? MirrorX(value) : value;
    public static Quaternion ConditionalMirrorX(Quaternion value, bool mirror) => mirror ? MirrorX(value) : value;
    public static Matrix3x4 ConditionalMirrorX(Matrix3x4 value, bool mirror) => mirror ? MirrorX(value) : value;

    public static Dictionary<int, int> MapNodesByNames(Tg.Model sourceModel, Tg.Model destinationModel)
    {
        var result = new Dictionary<int, int>();
        for (var sourceIndex = 0; sourceIndex < sourceModel.Nodes.Count; ++sourceIndex)
        {
            var sourceNode = sourceModel.Nodes[sourceIndex];
            if (sourceNode.Name.ToString().Length == 0)
                continue;

            for (var destinationIndex = 0; destinationIndex < destinationModel.Nodes.Count; ++destinationIndex)
            {
                if (destinationModel.Nodes[destinationIndex].Name != sourceNode.Name)
                    continue;
                result[sourceIndex] = destinationIndex;
                break;
            }
        }
        return result;
    }

    public static int CountMergeableResources(Tg.Model inputModel) => inputModel.Animations.Count;

    public static void MergeModels(Tg.Model outputModel, Tg.Model inputModel, string? overrideName)
    {
        var sourceToDestinationNodeIndex = MapNodesByNames(inputModel, outputModel);
        var sourceNodeNames = inputModel.Nodes.Select(node => node.Name.ToString()).ToList();

        if (CountMergeableResources(inputModel) != 0 && overrideName is not null && inputModel.Animations.Count == 1)
            inputModel.Animations[0].Name = overrideName;

        // Consume buffers
        var startBufferIndex = outputModel.Buffers.Count;
        foreach (var buffer in inputModel.Buffers)
            outputModel.Buffers.Add(buffer);

        // Consume and patch buffer views
        var startBufferViewIndex = outputModel.BufferViews.Count;
        foreach (var bufferView in inputModel.BufferViews)
            outputModel.BufferViews.Add(bufferView);
        for (var i = startBufferViewIndex; i < outputModel.BufferViews.Count; ++i)
            outputModel.BufferViews[i].Buffer += startBufferIndex;

        // Consume and patch accessors
        var startAccessorIndex = outputModel.Accessors.Count;
        foreach (var accessor in inputModel.Accessors)
            outputModel.Accessors.Add(accessor);
        for (var i = startAccessorIndex; i < outputModel.Accessors.Count; ++i)
            outputModel.Accessors[i].BufferView += startBufferViewIndex;

        // Consume and patch animations
        var startAnimationIndex = outputModel.Animations.Count;
        foreach (var animation in inputModel.Animations)
            outputModel.Animations.Add(animation);

        var ignoredNodes = new HashSet<string>();
        for (var i = startAnimationIndex; i < outputModel.Animations.Count; ++i)
        {
            var animation = outputModel.Animations[i];
            for (var j = animation.Channels.Count - 1; j >= 0; --j)
            {
                var channel = animation.Channels[j];
                if (sourceToDestinationNodeIndex.TryGetValue(channel.TargetNode, out var destinationIndex))
                    channel.TargetNode = destinationIndex;
                else
                {
                    ignoredNodes.Add(channel.TargetNode >= 0 && channel.TargetNode < sourceNodeNames.Count
                        ? sourceNodeNames[channel.TargetNode]
                        : "[Invalid node]");
                    animation.Channels.RemoveAt(j);
                }
            }

            foreach (var sampler in animation.Samplers)
            {
                sampler.Input += startAccessorIndex;
                sampler.Output += startAccessorIndex;
            }
        }

        if (ignoredNodes.Count != 0)
            Log.GetLogger().Warning("Ignored nodes in animation: {}", string.Join(", ", ignoredNodes));
    }

    public static void RenameNodes(Tg.Model model, Dictionary<string, string> mapping)
    {
        foreach (var node in model.Nodes)
        {
            if (mapping.TryGetValue(node.Name.ToString(), out var name))
                node.Name = name;
        }
    }
}

/// <summary>Inline transformation applied to the scene at the lowest level.</summary>
internal sealed class InlineTransform
{
    public bool MirrorX;
    public float Scale = 1.0f;

    public Vector3f ApplyToPosition(Vector3f vector) =>
        Scale * GLTFImporterFunctions.ConditionalMirrorX(vector, MirrorX);

    public Quaternion ApplyToRotation(Quaternion quaternion) =>
        GLTFImporterFunctions.ConditionalMirrorX(quaternion, MirrorX);

    public Matrix3x4 ApplyToInverseBindMatrix(Matrix3x4 matrix) =>
        Matrix3x4.FromScale(Scale) * GLTFImporterFunctions.ConditionalMirrorX(matrix, MirrorX) *
        Matrix3x4.FromScale(1.0f / Scale);
}

/// <summary>Raw imported input, parameters and generic output layout.</summary>
internal sealed class GLTFImporterBase : IDisposable
{
    public GLTFImporterBase(GLTFImporterSettings settings, Tg.Model model, string outputPath,
        string resourceNamePrefix, GLTFImporterCallback callback)
    {
        _settings = settings.Clone();
        _model = model;
        _outputPath = outputPath;
        _resourceNamePrefix = resourceNamePrefix;
        _callback = callback;
    }

    public void Dispose()
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        foreach (var (type, name) in _manualResources)
            cache.ReleaseResource(type, name, true);
    }

    public string CreateLocalResourceName(string nameHint, string prefix, string defaultName, string suffix)
    {
        var body = nameHint.Length != 0 ? GLTFImporterFunctions.GetSanitizedName(nameHint) : defaultName;
        for (var i = 0; i < GLTFImporterFunctions.MaxNameAssignTries; ++i)
        {
            var localResourceName = i != 0
                ? $"{prefix}{body}_{i}{suffix}"
                : $"{prefix}{body}{suffix}";
            if (!_localResourceNames.Add(localResourceName))
                continue;
            return localResourceName;
        }
        // Should never happen
        throw new InvalidOperationException("Cannot assign resource name");
    }

    public string CreateResourceName(string localResourceName)
    {
        var resourceName = _resourceNamePrefix + localResourceName;
        var absoluteFileName = _outputPath + localResourceName;
        _resourceNameToAbsoluteFileName[resourceName] = absoluteFileName;
        return resourceName;
    }

    public string GetResourceName(string nameHint, string prefix, string defaultName, string suffix) =>
        CreateResourceName(CreateLocalResourceName(nameHint, prefix, defaultName, suffix));

    public string GetAbsoluteFileName(string resourceName) =>
        _resourceNameToAbsoluteFileName.GetValueOrDefault(resourceName, string.Empty);

    public void AddToResourceCache(Resource resource)
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        cache.AddManualResource(resource);
        _manualResources.Add((resource.GetType().GetTypeId(), resource.GetName()));
        resource.SetAbsoluteFileName(GetAbsoluteFileName(resource.GetName()));
    }

    public void SaveResource(Resource resource)
    {
        var fileName = GetAbsoluteFileName(resource.GetName());
        if (fileName.Length == 0)
            throw new InvalidOperationException("Cannot save imported resource");
        resource.SetAbsoluteFileName(fileName);
        resource.SaveFile(new FileIdentifier(fileName));
    }

    public Tg.Model GetModel() => _model;
    public Context GetContext() => Context.Instance;
    public GLTFImporterSettings GetSettings() => _settings;
    public IReadOnlyDictionary<string, string> GetResourceNames() => _resourceNameToAbsoluteFileName;
    public GLTFImporterCallback GetCallback() => _callback;

    public void CheckAnimation(int index) => CheckT(index, _model.Animations.Count, "Invalid animation #{0} referenced");
    public void CheckAccessor(int index) => CheckT(index, _model.Accessors.Count, "Invalid accessor #{0} referenced");
    public void CheckBufferView(int index) => CheckT(index, _model.BufferViews.Count, "Invalid buffer view #{0} referenced");
    public void CheckImage(int index) => CheckT(index, _model.Images.Count, "Invalid image #{0} referenced");
    public void CheckMaterial(int index) => CheckT(index, _model.Materials.Count, "Invalid material #{0} referenced");
    public void CheckMesh(int index) => CheckT(index, _model.Meshes.Count, "Invalid mesh #{0} referenced");
    public void CheckNode(int index) => CheckT(index, _model.Nodes.Count, "Invalid node #{0} referenced");
    public void CheckSampler(int index) => CheckT(index, _model.Samplers.Count, "Invalid sampler #{0} referenced");
    public void CheckSkin(int index) => CheckT(index, _model.Skins.Count, "Invalid skin #{0} referenced");
    public void CheckTexture(int index) => CheckT(index, _model.Textures.Count, "Invalid texture #{0} referenced");

    private static void CheckT(int index, int count, string message)
    {
        if (index < 0 || index >= count)
            throw new InvalidOperationException(string.Format(message, index));
    }

    private readonly GLTFImporterSettings _settings;
    private readonly Tg.Model _model;
    private readonly string _outputPath;
    private readonly string _resourceNamePrefix;
    private readonly GLTFImporterCallback _callback;
    private readonly HashSet<string> _localResourceNames = [];
    private readonly Dictionary<string, string> _resourceNameToAbsoluteFileName = [];
    private readonly List<(StringHash Type, string Name)> _manualResources = [];
}

/// <summary>Utility to parse GLTF buffers.</summary>
internal sealed class GLTFBufferReader
{
    public GLTFBufferReader(GLTFImporterBase importerBase)
    {
        _base = importerBase;
        _model = _base.GetModel();
    }

    public List<T> ReadBufferView<T>(int bufferViewIndex, int byteOffset, int componentType,
        int type, int count, bool normalized) where T : unmanaged
    {
        _base.CheckBufferView(bufferViewIndex);
        var numComponents = checked((int)TgNative.GetNumComponentsInType((uint)type));
        if (numComponents <= 0)
            throw new InvalidOperationException($"Unexpected type {type} of buffer view elements");

        var bufferView = _model.BufferViews[bufferViewIndex];
        var result = new List<T>(new T[count * numComponents]);
        switch (componentType)
        {
        case GLTFConstants.ComponentTypeByte:
            ReadBufferViewImpl<sbyte, T>(result, bufferView, byteOffset, componentType, type, count);
            NormalizeFloats(result, normalized, 127);
            break;
        case GLTFConstants.ComponentTypeUnsignedByte:
            ReadBufferViewImpl<byte, T>(result, bufferView, byteOffset, componentType, type, count);
            NormalizeFloats(result, normalized, 255);
            break;
        case GLTFConstants.ComponentTypeShort:
            ReadBufferViewImpl<short, T>(result, bufferView, byteOffset, componentType, type, count);
            NormalizeFloats(result, normalized, 32767);
            break;
        case GLTFConstants.ComponentTypeUnsignedShort:
            ReadBufferViewImpl<ushort, T>(result, bufferView, byteOffset, componentType, type, count);
            NormalizeFloats(result, normalized, 65535);
            break;
        case GLTFConstants.ComponentTypeInt:
            ReadBufferViewImpl<int, T>(result, bufferView, byteOffset, componentType, type, count);
            break;
        case GLTFConstants.ComponentTypeUnsignedInt:
            ReadBufferViewImpl<uint, T>(result, bufferView, byteOffset, componentType, type, count);
            break;
        case GLTFConstants.ComponentTypeFloat:
            ReadBufferViewImpl<float, T>(result, bufferView, byteOffset, componentType, type, count);
            break;
        case GLTFConstants.ComponentTypeDouble:
            ReadBufferViewImpl<double, T>(result, bufferView, byteOffset, componentType, type, count);
            break;
        default:
            throw new InvalidOperationException($"Unsupported component type {componentType} of buffer view elements");
        }
        return result;
    }

    public List<T> ReadAccessorChecked<T>(Tg.Accessor accessor) where T : unmanaged
    {
        var result = ReadAccessor<T>(accessor);
        if (result.Count != checked((int)accessor.Count))
            throw new InvalidOperationException("Unexpected number of objects in accessor");
        return result;
    }

    public List<T> ReadAccessor<T>(Tg.Accessor accessor) where T : unmanaged
    {
        var numComponents = checked((int)TgNative.GetNumComponentsInType((uint)accessor.Type));
        if (numComponents <= 0)
            throw new InvalidOperationException($"Unexpected type {accessor.Type} of buffer view elements");

        // Read dense buffer data
        List<T> result;
        if (accessor.BufferView >= 0)
            result = ReadBufferView<T>(accessor.BufferView, checked((int)accessor.ByteOffset), accessor.ComponentType,
                accessor.Type, checked((int)accessor.Count), accessor.Normalized);
        else
            result = new List<T>(new T[checked((int)accessor.Count) * numComponents]);

        // Read sparse buffer data
        var numSparseElements = checked((int)accessor.SparseCount);
        if (accessor.SparseIsSparse && numSparseElements > 0)
        {
            var indices = ReadBufferView<uint>(accessor.SparseIndicesBufferView, checked((int)accessor.SparseIndicesByteOffset),
                accessor.SparseIndicesComponentType, GLTFConstants.TypeScalar, numSparseElements, false);
            var values = ReadBufferView<T>(accessor.SparseValuesBufferView, checked((int)accessor.SparseValuesByteOffset),
                accessor.ComponentType, accessor.Type, numSparseElements, accessor.Normalized);
            for (var i = 0; i < indices.Count; ++i)
            {
                for (var j = 0; j < numComponents; ++j)
                    result[(int)indices[i] * numComponents + j] = values[i * numComponents + j];
            }
        }
        return result;
    }

    public List<Vector2f> ReadVector2Accessor(Tg.Accessor accessor) => RepackFloats<Vector2f>(ReadAccessor<float>(accessor));
    public List<Vector3f> ReadVector3Accessor(Tg.Accessor accessor) => RepackFloats<Vector3f>(ReadAccessor<float>(accessor));
    public List<Vector4f> ReadVector4Accessor(Tg.Accessor accessor) => RepackFloats<Vector4f>(ReadAccessor<float>(accessor));
    public List<Matrix4> ReadMatrix4Accessor(Tg.Accessor accessor) => RepackFloats<Matrix4>(ReadAccessor<float>(accessor));

    public List<Quaternion> ReadQuaternionAccessor(Tg.Accessor accessor)
    {
        var values = RepackFloats<Vector4f>(ReadAccessor<float>(accessor));
        return values.Select(GLTFImporterFunctions.RotationFromVector).ToList();
    }

    private static int GetByteStride(Tg.BufferView bufferView, int componentType, int type)
    {
        var componentSizeInBytes = checked((int)TgNative.GetComponentSizeInBytes((uint)componentType));
        var numComponents = checked((int)TgNative.GetNumComponentsInType((uint)type));
        if (componentSizeInBytes <= 0 || numComponents <= 0)
            return -1;
        return bufferView.ByteStride == 0 ? componentSizeInBytes * numComponents : checked((int)bufferView.ByteStride);
    }

    private void ReadBufferViewImpl<T, U>(List<U> result, Tg.BufferView bufferView,
        int byteOffset, int componentType, int type, int count)
        where T : unmanaged where U : unmanaged
    {
        var buffer = _model.Buffers[bufferView.Buffer];
        var bufferData = buffer.Data;
        var offset = checked((int)bufferView.ByteOffset) + byteOffset;
        var stride = GetByteStride(bufferView, componentType, type);
        var numComponents = checked((int)TgNative.GetNumComponentsInType((uint)type));
        Span<byte> source = stackalloc byte[Unsafe.SizeOf<T>()];
        for (var i = 0; i < count; ++i)
        {
            for (var j = 0; j < numComponents; ++j)
            {
                var componentOffset = offset + Unsafe.SizeOf<T>() * j;
                for (var byteIndex = 0; byteIndex < source.Length; ++byteIndex)
                    source[byteIndex] = bufferData[componentOffset + byteIndex];
                var elementValue = MemoryMarshal.Read<T>(source);
                result[i * numComponents + j] = (U)Convert.ChangeType(elementValue, typeof(U));
            }
            offset += stride;
        }
    }

    private static List<T> RepackFloats<T>(List<float> source) where T : unmanaged
    {
        var numComponents = Unsafe.SizeOf<T>() / sizeof(float);
        if (source.Count % numComponents != 0)
            throw new InvalidOperationException("Unexpected number of components in array");
        return MemoryMarshal.Cast<float, T>(CollectionsMarshal.AsSpan(source)).ToArray().ToList();
    }

    private static void NormalizeFloats<T>(List<T> result, bool normalize, float maxValue) where T : unmanaged
    {
        if (!normalize || typeof(T) != typeof(float))
            return;
        for (var i = 0; i < result.Count; ++i)
        {
            var value = Math.Max(-1.0f, (float)(object)result[i] / maxValue);
            result[i] = (T)(object)value;
        }
    }

    private readonly GLTFImporterBase _base;
    private readonly Tg.Model _model;
}

/// <summary>GLTF node reference used for hierarchy view.</summary>
internal sealed class GLTFNode
{
    /// <summary>Hierarchy metadata.</summary>
    public GLTFNode? Root;
    public GLTFNode? Parent;
    public List<GLTFNode> Children = [];

    /// <summary>Data directly imported from GLTF.</summary>
    public int Index;
    public string Name = string.Empty;
    public Vector3f Position;
    public Quaternion Rotation;
    public Vector3f Scale = Vector3f.One;
    public int? Mesh;
    public int? Skin;
    public List<float> MorphWeights = [];
    public List<int> ContainedInSkins = [];

    /// <summary>Data generated by importer.</summary>
    public int? SkeletonIndex;
    public string? UniqueBoneName;
    public List<int> SkinnedMeshNodes = [];

    public string GetEffectiveName() => UniqueBoneName ?? Name;

    /// <summary>Check if this node subtree contains nothing but empty nodes.</summary>
    public bool IsRecursivelyEmpty() => Mesh is null && Skin is null && ContainedInSkins.Count == 0 &&
        Children.All(child => child.IsRecursivelyEmpty());
}

/// <summary>Represents Urho skeleton which may be composed from one or more GLTF skins.</summary>
/// <remarks>Does <em>not</em> contain mesh-related specifics like bone indices and bind matrices.</remarks>
internal sealed class GLTFSkeleton
{
    public int Index;
    public List<int> Skins = [];
    public GLTFNode RootNode = null!;
    public Dictionary<string, GLTFNode> BoneNameToNode = [];
}

/// <summary>Represents GLTF skin as Urho skeleton with bone indices and bind matrices.</summary>
internal sealed class GLTFSkin
{
    public int Index;
    public GLTFSkeleton Skeleton = null!;
    /// <summary>Bone nodes, contain exactly one root node.</summary>
    public List<GLTFNode> BoneNodes = [];
    public List<Matrix3x4> InverseBindMatrices = [];
    /// <summary>Does <em>not</em> contain bounding volumes.</summary>
    public List<BoneView> CookedBones = [];
}

/// <summary>Represents unique Urho model with optional skin.</summary>
internal sealed class GLTFMeshSkinPair
{
    public int Mesh;
    public int? Skin;
}

/// <summary>Represents Urho AnimationTrack with possibly separate keys.</summary>
internal sealed class GLTFBoneTrack
{
    public AnimationChannel ChannelMask;
    public GLTFKeyFrameSet<Vector3f> Positions = new();
    public GLTFKeyFrameSet<Quaternion> Rotations = new();
    public GLTFKeyFrameSet<Vector3f> Scales = new();
}

internal sealed class GLTFKeyFrameSet<T>() : KeyFrameSet<(float, T)>(keyFrame => keyFrame.Item1);

/// <summary>Represents attribute track.</summary>
internal sealed class GLTFAttributeTrack
{
    public KeyFrameInterpolation Interpolation = KeyFrameInterpolation.Linear;
    public List<float> Keys = [];
    public List<Variant> Values = [];
    public List<Variant> InTangents = [];
    public List<Variant> OutTangents = [];
}

/// <summary>Represents subset of animation tracks of single GLTF animation that corresponds to single Urho animation.</summary>
internal sealed class GLTFAnimationTrackGroup
{
    public Dictionary<string, GLTFBoneTrack> BoneTracksByBoneName = [];
    public Dictionary<string, GLTFAttributeTrack> AttributeTracksByPath = [];
}

/// <summary>Represents preprocessed GLTF animation which may correspond to one or more Urho animations.</summary>
internal sealed class GLTFAnimation
{
    public int Index;
    public string Name = string.Empty;
    /// <summary>Animations grouped by the nearest parent skeleton.</summary>
    public Dictionary<GLTFAnimationGroupKey, GLTFAnimationTrackGroup> AnimationGroups = [];
}

internal readonly record struct GLTFAnimationGroupKey(int? Value);
