using System.Collections.Generic;
using Microsoft.Extensions.Logging;

using RedBolt.Logging;
namespace RedBolt.Math;
using Math = global::System.Math;


/// <summary>Tetrahedral mesh.</summary>
public partial class TetrahedralMesh : ICloneable
{
    public TetrahedralMesh()
    {
        Vertices = [];
        Tetrahedrons = [];
        HullNormals = [];
        IgnoredVertices = [];
        NumInnerTetrahedrons = 0;
        DebugHighlightEdges = [];
    }

    public TetrahedralMesh(TetrahedralMesh mesh)
    {
        Vertices = new List<Vector3f>(mesh.Vertices);
        Tetrahedrons = new List<Tetrahedron>(mesh.Tetrahedrons);
        HullNormals = new List<Vector3f>(mesh.HullNormals);
        IgnoredVertices = new List<uint>(mesh.IgnoredVertices);
        NumInnerTetrahedrons = mesh.NumInnerTetrahedrons;
        DebugHighlightEdges = new List<(uint First, uint Second)>(mesh.DebugHighlightEdges);
    }

    public object Clone() => new TetrahedralMesh(this);

    /// <summary>Define mesh from vertices.</summary>
    public void Define(IReadOnlyList<Vector3f> positions)
    {
        var boundingBox = new BoundingBox(positions);
        var size = boundingBox.Size;
        var maxSize = MathF.Max(1.0f, MathF.Max(size.X, MathF.Max(size.Y, size.Z)));
        boundingBox.Min -= Vector3f.One;
        boundingBox.Max += Vector3f.One;
        InitializeSuperMesh(boundingBox);
        BuildTetrahedrons(positions);
    }

    /// <summary>Collect all edges in the mesh, e.g. for debug rendering.</summary>
    public void CollectEdges(List<(uint First, uint Second)> edges)
    {
        edges.Clear();

        // Collect everything
        for (uint tetIndex = 0; tetIndex < NumInnerTetrahedrons; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[(int)tetIndex];
            for (uint i = 0; i < 4; ++i)
            {
                for (uint j = 0; j < 4; ++j)
                {
                    var startIndex = tetrahedron.Indices[(int)i];
                    var endIndex = tetrahedron.Indices[(int)j];
                    edges.Add((startIndex, endIndex));
                }
            }
        }

        // Sort edges and remove duplicates
        for (var i = 0; i < edges.Count; ++i)
        {
            if (edges[i].First > edges[i].Second)
                edges[i] = (edges[i].Second, edges[i].First);
        }

        edges.Sort((lhs, rhs) => lhs.First != rhs.First
            ? lhs.First.CompareTo(rhs.First)
            : lhs.Second.CompareTo(rhs.Second));
        for (var i = edges.Count - 1; i > 0; --i)
        {
            if (edges[i] == edges[i - 1])
                edges.RemoveAt(i);
        }
    }

    /// <summary>Calculate circumsphere of given tetrahedron.</summary>
    public HighPrecisionSphere GetTetrahedronCircumsphere(uint tetIndex)
    {
        var tetrahedron = Tetrahedrons[(int)tetIndex];
        var p0Value = Vertices[(int)tetrahedron.Indices[0]];
        var p1Value = Vertices[(int)tetrahedron.Indices[1]];
        var p2Value = Vertices[(int)tetrahedron.Indices[2]];
        var p3Value = Vertices[(int)tetrahedron.Indices[3]];
        var p0 = new Vector3d(p0Value.X, p0Value.Y, p0Value.Z);
        var p1 = new Vector3d(p1Value.X, p1Value.Y, p1Value.Z);
        var p2 = new Vector3d(p2Value.X, p2Value.Y, p2Value.Z);
        var p3 = new Vector3d(p3Value.X, p3Value.Y, p3Value.Z);
        var u1 = p1 - p0;
        var u2 = p2 - p0;
        var u3 = p3 - p0;

        var d01 = u1.LengthSquared;
        var d02 = u2.LengthSquared;
        var d03 = u3.LengthSquared;

        var u2u3 = u2.CrossProduct(u3);
        var u3u1 = u3.CrossProduct(u1);
        var u1u2 = u1.CrossProduct(u2);

        var radiusNum = u2u3 * d01 + u3u1 * d02 + u1u2 * d03;
        var radiusDen = 2.0 * u1.DotProduct(u2u3);

        if (Math.Abs(radiusDen) < MathDefs.Epsilon * MathDefs.Epsilon)
        {
            Log.GetLogger().LogWarning("Degenerate tetrahedron in tetrahedral mesh due to error in tetrahedral mesh generation");
            return new HighPrecisionSphere { Center = default, Radius = MathDefs.LargeValue * MathDefs.LargeValue };
        }

        var center = p0 + radiusNum * (1.0 / radiusDen);

        // Radius is the minimum distance
        Span<double> squaredDistances = stackalloc double[4]
        {
            (p0 - center).LengthSquared,
            (p1 - center).LengthSquared,
            (p2 - center).LengthSquared,
            (p3 - center).LengthSquared
        };
        var radiusSquared = squaredDistances[0];
        for (var i = 1; i < squaredDistances.Length; ++i)
            radiusSquared = Math.Min(radiusSquared, squaredDistances[i]);
        return new HighPrecisionSphere { Center = center, Radius = Math.Sqrt(radiusSquared) };
    }

    /// <summary>Calculate barycentric coordinates for inner tetrahedron.</summary>
    public Vector4f GetInnerBarycentricCoords(uint tetIndex, Vector3f position)
    {
        var tetrahedron = Tetrahedrons[(int)tetIndex];
        var basePosition = Vertices[(int)tetrahedron.Indices[0]];
        var coords = tetrahedron.Matrix * (position - basePosition);
        return new Vector4f(1.0f - coords.X - coords.Y - coords.Z, coords.X, coords.Y, coords.Z);
    }

    /// <summary>Calculate barycentric coordinates for outer tetrahedron.</summary>
    public Vector4f GetOuterBarycentricCoords(uint tetIndex, Vector3f position)
    {
        var tetrahedron = Tetrahedrons[(int)tetIndex];
        var p1 = Vertices[(int)tetrahedron.Indices[0]];
        var p2 = Vertices[(int)tetrahedron.Indices[1]];
        var p3 = Vertices[(int)tetrahedron.Indices[2]];
        var normal = (p2 - p1).CrossProduct(p3 - p1);

        // Position is in the inner cell, return fake barycentric
        if (normal.DotProduct(position - p1) < 0.0f)
            return new Vector4f(0.0f, 0.0f, 0.0f, -1.0f);

        var poly = tetrahedron.Matrix * position;
        var t = tetrahedron.Indices[3] == Tetrahedron.Infinity3 ? SolveCubic(poly) : SolveQuadratic(poly);

        var t1 = p1 + t * HullNormals[(int)tetrahedron.Indices[0]];
        var t2 = p2 + t * HullNormals[(int)tetrahedron.Indices[1]];
        var t3 = p3 + t * HullNormals[(int)tetrahedron.Indices[2]];
        var coords = GetTriangleBarycentricCoords(position, t1, t2, t3);
        return new Vector4f(coords, 0.0f);
    }

    /// <summary>Calculate barycentric coordinates for tetrahedron.</summary>
    public Vector4f GetBarycentricCoords(uint tetIndex, Vector3f position)
    {
        return tetIndex < NumInnerTetrahedrons
            ? GetInnerBarycentricCoords(tetIndex, position)
            : GetOuterBarycentricCoords(tetIndex, position);
    }

    /// <summary>Find tetrahedron containing given position and calculate barycentric coordinates within this tetrahedron.</summary>
    public Vector4f GetInterpolationFactors(Vector3f position, ref uint tetIndexHint)
    {
        if (Tetrahedrons.Count == 0)
            return Vector4f.Zero;

        var maxIters = (uint)Tetrahedrons.Count;
        if (tetIndexHint >= maxIters)
            tetIndexHint = 0;

        for (uint i = 0; i < maxIters; ++i)
        {
            var weights = GetBarycentricCoords(tetIndexHint, position);
            if (weights.X >= 0.0f && weights.Y >= 0.0f && weights.Z >= 0.0f && weights.W >= 0.0f)
                return weights;

            if (weights.X < weights.Y && weights.X < weights.Z && weights.X < weights.W)
                tetIndexHint = Tetrahedrons[(int)tetIndexHint].Neighbors[0];
            else if (weights.Y < weights.Z && weights.Y < weights.W)
                tetIndexHint = Tetrahedrons[(int)tetIndexHint].Neighbors[1];
            else if (weights.Z < weights.W)
                tetIndexHint = Tetrahedrons[(int)tetIndexHint].Neighbors[2];
            else
                tetIndexHint = Tetrahedrons[(int)tetIndexHint].Neighbors[3];
        }

        return GetBarycentricCoords(tetIndexHint, position);
    }

    /// <summary>Sample value at given position from the arbitrary container of per-vertex data.</summary>
    public Vector3f Sample(IReadOnlyList<Vector3f> container, Vector3f position, ref uint tetIndexHint)
    {
        var result = Vector3f.Zero;
        var weights = GetInterpolationFactors(position, ref tetIndexHint);
        if (tetIndexHint < Tetrahedrons.Count)
        {
            var tetrahedron = Tetrahedrons[(int)tetIndexHint];
            for (var i = 0; i < 3; ++i)
                result += container[(int)tetrahedron.Indices[i]] * weights[i];
            if (tetIndexHint < NumInnerTetrahedrons)
                result += container[(int)tetrahedron.Indices[3]] * weights[3];
        }

        return result;
    }

    /// <summary>Sample value at given position from the arbitrary container of per-vertex data.</summary>
    public SphericalHarmonicsDot9 Sample(
        IReadOnlyList<SphericalHarmonicsDot9> container, Vector3f position, ref uint tetIndexHint)
    {
        var result = SphericalHarmonicsDot9.Zero;
        var weights = GetInterpolationFactors(position, ref tetIndexHint);
        if (tetIndexHint < Tetrahedrons.Count)
        {
            var tetrahedron = Tetrahedrons[(int)tetIndexHint];
            for (var i = 0; i < 3; ++i)
                result += container[(int)tetrahedron.Indices[i]] * weights[i];
            if (tetIndexHint < NumInnerTetrahedrons)
                result += container[(int)tetrahedron.Indices[3]] * weights[3];
        }

        return result;
    }

    /// <summary>Vertices.</summary>
    public List<Vector3f> Vertices;

    /// <summary>Tetrahedrons.</summary>
    public List<Tetrahedron> Tetrahedrons;

    /// <summary>Hull normals.</summary>
    public List<Vector3f> HullNormals;

    /// <summary>Array of ignored vertices.</summary>
    public List<uint> IgnoredVertices;

    /// <summary>Number of inner tetrahedrons.</summary>
    public uint NumInnerTetrahedrons;

    /// <summary>Debug array of edges related to errors in generation.</summary>
    public List<(uint First, uint Second)> DebugHighlightEdges;

}
