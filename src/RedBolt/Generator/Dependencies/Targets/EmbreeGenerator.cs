namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class EmbreeGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "embree",
        "RedBolt.Bindings.Embree",
        "RedBolt.Bindings.Embree",
        "EmbreeNativeBindings",
        nativeLibraryName,
        "redbolt_embree",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "rtcAttachGeometry",
        "rtcCommitGeometry",
        "rtcCommitScene",
        "rtcInterpolate",
        "rtcIntersect1",
        "rtcNewDevice",
        "rtcNewGeometry",
        "rtcNewScene",
        "rtcReleaseDevice",
        "rtcReleaseGeometry",
        "rtcReleaseScene",
        "rtcSetGeometryMask",
        "rtcSetGeometryVertexAttributeCount",
        "rtcSetNewGeometryBuffer",
        "rtcSetSceneFlags",
        "RedBoltEmbreeGetFilterHit",
        "RedBoltEmbreeGetFilterRay"
    };

    private static readonly HashSet<string> RetainedEnums = new(StringComparer.Ordinal)
    {
        "RTCBufferType",
        "RTCFeatureFlags",
        "RTCFormat",
        "RTCGeometryType",
        "RTCRayQueryFlags",
        "RTCSceneFlags"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedRecordFields =
        new(StringComparer.Ordinal)
        {
            ["RTCFilterFunctionNArguments"] = new(StringComparer.Ordinal)
            {
                "valid", "geometryUserPtr", "context", "ray", "hit", "N"
            },
            ["RTCHit"] = new(StringComparer.Ordinal)
            {
                "Ng_x", "Ng_y", "Ng_z", "u", "v", "primID", "geomID", "instID"
            },
            ["RTCIntersectArguments"] = new(StringComparer.Ordinal)
            {
                "flags", "feature_mask", "context", "filter", "intersect"
            },
            ["RTCInterpolateArguments"] = new(StringComparer.Ordinal)
            {
                "geometry", "primID", "u", "v", "bufferType", "bufferSlot", "P", "dPdu", "dPdv",
                "ddPdudu", "ddPdvdv", "ddPdudv", "valueCount"
            },
            ["RTCRay"] = new(StringComparer.Ordinal)
            {
                "org_x", "org_y", "org_z", "tnear", "dir_x", "dir_y", "dir_z", "time", "tfar", "mask",
                "id", "flags"
            },
            ["RTCRayHit"] = new(StringComparer.Ordinal) { "ray", "hit" },
            ["RTCRayQueryContext"] = new(StringComparer.Ordinal) { "instID" }
        };

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            var member = compilation.Members[index];
            if (member is FunctionBinding function && RetainedFunctions.Contains(function.NativeName) ||
                member is EnumBinding @enum && RetainedEnums.Contains(@enum.NativeName) ||
                member is RecordBinding nativeArray && nativeArray.HasMetadata(MetadataKeys.NativeArrayRecord))
            {
                continue;
            }

            if (member is RecordBinding record &&
                RetainedRecordFields.TryGetValue(record.NativeName, out var retainedFields))
            {
                for (var memberIndex = record.Members.Count - 1; memberIndex >= 0; --memberIndex)
                {
                    if (record.Members[memberIndex] is FieldBinding field && retainedFields.Contains(field.NativeName) ||
                        record.Members[memberIndex] is FunctionBinding { IsConstructor: true })
                    {
                        continue;
                    }

                    record.Members.RemoveAt(memberIndex);
                    prunedCount++;
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
