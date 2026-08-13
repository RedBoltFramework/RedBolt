// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using System;

using RedBolt.Graphics;
using RedBolt.Math;

public static class CsgCommon
{
    public const float DefaultEpsilon = 0.0001f;

    /// <summary>Pick a stable perpendicular axis for the provided normal.</summary>
    public static Vector3f MakePerpendicular(Vector3f normal)
    {
        // Pick an axis least aligned with normal to avoid degeneracy.
        var axis = MathF.Abs(normal.X) > MathF.Abs(normal.Z) ? Vector3f.Forward : Vector3f.Right;
        var perp = normal.CrossProduct(axis);
        if (perp.LengthSquared > MathDefs.Epsilon)
            return perp.Normalized;

        // Fallback for pathological cases.
        return normal.CrossProduct(Vector3f.Up).NormalizedOrDefault(Vector3f.Right);
    }

    /// <summary>Lerp vertex attributes for CSG operations.</summary>
    public static ModelVertex LerpModelVertex(ModelVertex a, ModelVertex b, float t)
    {
        var result = new ModelVertex
        {
            Position = a.Position.Lerp(b.Position, t)
        };

        if (a.HasNormal() || b.HasNormal())
        {
            var normalA = a.GetNormal();
            var normalB = b.GetNormal();
            result.SetNormal(normalA.Lerp(normalB, t).Normalized);
        }

        if (a.HasTangent() || b.HasTangent())
        {
            var tangentA = a.GetTangent();
            var tangentB = b.GetTangent();
            var tangent3 = tangentA.Lerp(tangentB, t);

            var tangentLenSq = tangent3.LengthSquared;
            if (tangentLenSq > MathDefs.Epsilon)
            {
                tangent3 /= MathF.Sqrt(tangentLenSq);
                if (result.HasNormal())
                {
                    var normal = result.GetNormal();
                    var dotNT = normal.DotProduct(tangent3);
                    tangent3 -= normal * dotNT;
                    var orthoLenSq = tangent3.LengthSquared;
                    tangent3 = orthoLenSq > MathDefs.Epsilon
                        ? tangent3 / MathF.Sqrt(orthoLenSq)
                        : MakePerpendicular(normal);
                }
            }
            else if (result.HasNormal())
            {
                tangent3 = MakePerpendicular(result.GetNormal());
            }

            var handedness = t < 0.5f ? a.Tangent.W : b.Tangent.W;
            result.Tangent = new Vector4f(tangent3, handedness);
        }

        if (a.HasBinormal() || b.HasBinormal())
        {
            var binormalA = a.Binormal.ToVector3();
            var binormalB = b.Binormal.ToVector3();
            result.Binormal = binormalA.Lerp(binormalB, t).Normalized.ToVector4();
        }

        for (var i = 0; i < ModelVertex.MaxUVs; ++i)
            result.Uv[i] = a.Uv[i].Lerp(b.Uv[i], t);

        for (var i = 0u; i < ModelVertex.MaxColors; ++i)
            result.SetColor(i, a.GetColor(i).Lerp(b.GetColor(i), t));

        return result;
    }

    /// <summary>Make stable orthonormal basis vectors for the provided normal.</summary>
    public static void MakeAxesFromNormal(Vector3f normal, out Vector3f axisU, out Vector3f axisV)
    {
        var unitNormal = normal.NormalizedOrDefault(Vector3f.Up);
        axisU = MakePerpendicular(unitNormal);
        axisV = unitNormal.CrossProduct(axisU).NormalizedOrDefault(Vector3f.Up);
    }

    /// <summary>Quantize position into integer key using 1/epsilon scale.</summary>
    public static Vector3i QuantizePosition(Vector3f position, float invEps)
    {
        return new Vector3i(
            (int)MathF.Floor(position.X * invEps + 0.5f),
            (int)MathF.Floor(position.Y * invEps + 0.5f),
            (int)MathF.Floor(position.Z * invEps + 0.5f));
    }
}
