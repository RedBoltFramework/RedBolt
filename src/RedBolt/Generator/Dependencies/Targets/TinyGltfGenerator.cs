namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class TinyGltfGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "tinygltf",
        "RedBolt.Bindings.TinyGltf",
        "RedBolt.Bindings.TinyGltf",
        "TinyGltfNativeBindings",
        nativeLibraryName,
        "redbolt_tinygltf",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> RetainedRecords = new(StringComparer.Ordinal)
    {
        "Accessor",
        "AccessorSparse",
        "AccessorSparseIndices",
        "AccessorSparseValues",
        "Animation",
        "AnimationChannel",
        "AnimationChannelTarget",
        "AnimationSampler",
        "Buffer",
        "BufferView",
        "Camera",
        "Image",
        "Material",
        "Mesh",
        "Model",
        "Node",
        "NormalTextureInfo",
        "OcclusionTextureInfo",
        "Parameter",
        "PbrMetallicRoughness",
        "Primitive",
        "Sampler",
        "Scene",
        "Skin",
        "Texture",
        "TextureInfo",
        "TinyGLTF",
        "Value",
        "Light"
    };

    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "GetComponentSizeInBytes",
        "GetNumComponentsInType"
    };

    private static readonly HashSet<string> TinyGltfFunctions = new(StringComparer.Ordinal)
    {
        "LoadASCIIFromFile",
        "LoadBinaryFromFile",
        "LoadBinaryFromMemory",
        "SetImageLoader"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedFields = new(StringComparer.Ordinal)
    {
        ["Accessor"] = ["bufferView", "byteOffset", "componentType", "count", "normalized", "sparse", "type"],
        ["AccessorSparse"] = ["count", "indices", "values"],
        ["AccessorSparseIndices"] = ["bufferView", "byteOffset", "componentType"],
        ["AccessorSparseValues"] = ["bufferView", "byteOffset"],
        ["Animation"] = ["channels", "name", "samplers"],
        ["AnimationChannel"] = ["sampler", "target_node", "target_path"],
        ["AnimationSampler"] = ["input", "interpolation", "output"],
        ["Buffer"] = ["data"],
        ["BufferView"] = ["buffer", "byteLength", "byteOffset", "byteStride", "target"],
        ["Image"] = ["bits", "component", "height", "image", "mimeType", "name", "pixel_type", "uri", "width"],
        ["Material"] = ["alphaCutoff", "alphaMode", "doubleSided", "emissiveFactor", "emissiveTexture", "extensions", "name", "normalTexture", "occlusionTexture", "pbrMetallicRoughness"],
        ["Mesh"] = ["name", "primitives", "weights"],
        ["Model"] = ["accessors", "animations", "bufferViews", "buffers", "defaultScene", "extensionsUsed", "images", "materials", "meshes", "nodes", "samplers", "scenes", "skins", "textures"],
        ["Node"] = ["children", "matrix", "mesh", "name", "rotation", "scale", "skin", "translation", "weights"],
        ["NormalTextureInfo"] = ["index", "scale", "texCoord"],
        ["OcclusionTextureInfo"] = ["index", "strength", "texCoord"],
        ["PbrMetallicRoughness"] = ["baseColorFactor", "baseColorTexture", "metallicFactor", "metallicRoughnessTexture", "roughnessFactor"],
        ["Primitive"] = ["attributes", "indices", "material", "mode", "targets"],
        ["Sampler"] = ["magFilter", "minFilter", "wrapS", "wrapT"],
        ["Scene"] = ["name", "nodes"],
        ["Skin"] = ["inverseBindMatrices", "joints", "name", "skeleton"],
        ["Texture"] = ["sampler", "source"],
        ["TextureInfo"] = ["index", "texCoord"]
    };

    protected override bool RetainStdlibWrapper(RecordBinding record) =>
        record.ManagedNamespace == "Std" && record.ManagedName is "Map" or "String" or "Vector";

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tinygltf"] = "RedBolt.Bindings.TinyGltf"
        };

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            var member = compilation.Members[index];
            if (member is FunctionBinding function && RetainedFunctions.Contains(function.NativeName) ||
                member is RecordBinding record && RetainedRecords.Contains(record.NativeName) ||
                member is RecordBinding stdlibRecord && RetainStdlibWrapper(stdlibRecord))
            {
                if (member is RecordBinding targetRecord)
                {
                    if (RetainStdlibWrapper(targetRecord))
                        continue;

                    RetainedFields.TryGetValue(targetRecord.NativeName, out var retainedFields);
                    for (var memberIndex = targetRecord.Members.Count - 1; memberIndex >= 0; --memberIndex)
                    {
                        if ((targetRecord.Members[memberIndex] is FieldBinding field &&
                                retainedFields?.Contains(field.NativeName) == true) ||
                            (targetRecord.Members[memberIndex] is FunctionBinding { IsConstructor: true } &&
                                targetRecord.NativeName is not "Value") ||
                            (targetRecord.Members[memberIndex] is FunctionBinding accessorMethod &&
                                targetRecord.NativeName == "Accessor" && accessorMethod.NativeName == "ByteStride") ||
                            (targetRecord.Members[memberIndex] is FunctionBinding tinyGltfMethod &&
                                targetRecord.NativeName == "TinyGLTF" && TinyGltfFunctions.Contains(tinyGltfMethod.NativeName)))
                        {
                            continue;
                        }

                        targetRecord.Members.RemoveAt(memberIndex);
                        prunedCount++;
                    }
                }
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }

        compilation.InvalidateCaches();
        return prunedCount;
    }
}
