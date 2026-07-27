using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Math;
using Math = global::System.Math;


/// <summary>Sphere with double precision components.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct HighPrecisionSphere
{
    /// <summary>Center.</summary>
    public Vector3d Center;

    /// <summary>Radius.</summary>
    public double Radius;

    /// <summary>Return signed distance from position to the sphere.</summary>
    public readonly double Distance(Vector3f position)
    {
        var doublePosition = new Vector3d(position.X, position.Y, position.Z);
        var distSquared = (doublePosition - Center).LengthSquared;
        return Math.Sqrt(distSquared) - Radius;
    }
}

/// <summary>Surface triangle of tetrahedral mesh with adjacency information.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TetrahedralMeshSurfaceTriangle
{
    [InlineArray(3)]
    [StructLayout(LayoutKind.Sequential)]
    public struct UIntArray3
    {
        private uint _element0;
    }

    /// <summary>Indices of triangle vertices.</summary>
    public UIntArray3 Indices;

    /// <summary>Index of the 4th vertex of underlying tetrahedron. Unspecified if there's no underlying tetrahedron.</summary>
    public uint UnusedIndex;

    /// <summary>Indices of neighbor triangles.</summary>
    public UIntArray3 Neighbors;

    /// <summary>Index of underlying tetrahedron. uint.MaxValue if empty.</summary>
    public uint TetIndex;

    /// <summary>Face of underlying tetrahedron, from 0 to 3.</summary>
    public uint TetFace;

    public TetrahedralMeshSurfaceTriangle()
    {
        Indices = default;
        UnusedIndex = uint.MaxValue;
        Neighbors = default;
        Neighbors[0] = uint.MaxValue;
        Neighbors[1] = uint.MaxValue;
        Neighbors[2] = uint.MaxValue;
        TetIndex = uint.MaxValue;
        TetFace = 0;
    }

    /// <summary>Return whether the triangle has given neighbour.</summary>
    public readonly bool HasNeighbor(uint neighborIndex)
    {
        for (var i = 0; i < 3; ++i)
        {
            if (Neighbors[i] == neighborIndex)
                return true;
        }

        return false;
    }

    /// <summary>Normalize triangle indices so (p2 - p1) x (p3 - p1) is the normal.</summary>
    public void Normalize(IReadOnlyList<Vector3f> vertices)
    {
        var p0 = vertices[(int)UnusedIndex];
        var p1 = vertices[(int)Indices[0]];
        var p2 = vertices[(int)Indices[1]];
        var p3 = vertices[(int)Indices[2]];
        var outsideDirection = p1 - p0;
        var actualNormal = (p2 - p1).CrossProduct(p3 - p1);
        if (outsideDirection.DotProduct(actualNormal) < 0.0f)
        {
            (Indices[0], Indices[1]) = (Indices[1], Indices[0]);
            (Neighbors[0], Neighbors[1]) = (Neighbors[1], Neighbors[0]);
        }
    }

    /// <summary>Calculate the ratio between longest and shortest side of the triangle.</summary>
    public readonly float CalculateScore(IReadOnlyList<Vector3f> vertices)
    {
        var p1 = vertices[(int)Indices[0]];
        var p2 = vertices[(int)Indices[1]];
        var p3 = vertices[(int)Indices[2]];

        var side1 = (p1 - p2).Length;
        var side2 = (p2 - p3).Length;
        var side3 = (p3 - p1).Length;
        var minSide = MathF.Min(side1, MathF.Min(side2, side3));
        var maxSide = MathF.Max(side1, MathF.Max(side2, side3));
        return MathF.Min(MathDefs.LargeValue, maxSide / minSide);
    }
}

/// <summary>Edge of the surface of tetrahedral mesh.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TetrahedralMeshSurfaceEdge
{
    [InlineArray(2)]
    [StructLayout(LayoutKind.Sequential)]
    public struct UIntArray2
    {
        private uint _element0;
    }

    /// <summary>Indices.</summary>
    public UIntArray2 Indices;

    /// <summary>Face that owns this edge.</summary>
    public uint FaceIndex;

    /// <summary>Index of the edge in triangle.</summary>
    public uint EdgeIndex;

    /// <summary>Construct default.</summary>
    public TetrahedralMeshSurfaceEdge()
    {
        Indices = default;
        FaceIndex = uint.MaxValue;
        EdgeIndex = 0;
    }

    /// <summary>Construct valid.</summary>
    public TetrahedralMeshSurfaceEdge(uint i0, uint i1, uint faceIndex, uint edgeIndex)
    {
        Indices = default;
        Indices[0] = i0;
        Indices[1] = i1;
        FaceIndex = faceIndex;
        EdgeIndex = edgeIndex;
        if (Indices[0] > Indices[1])
            (Indices[0], Indices[1]) = (Indices[1], Indices[0]);
    }

    /// <summary>Compare for sorting. Only edges themselves are compared.</summary>
    public static bool operator <(TetrahedralMeshSurfaceEdge lhs, TetrahedralMeshSurfaceEdge rhs)
    {
        if (lhs.Indices[0] != rhs.Indices[0])
            return lhs.Indices[0] < rhs.Indices[0];
        return lhs.Indices[1] < rhs.Indices[1];
    }

    public static bool operator >(TetrahedralMeshSurfaceEdge lhs, TetrahedralMeshSurfaceEdge rhs) => rhs < lhs;
}

/// <summary>Surface of tetrahedral mesh. Vertices are shared with tetrahedral mesh and are not stored.</summary>
public struct TetrahedralMeshSurface : ICloneable
{
    /// <summary>Faces.</summary>
    public List<TetrahedralMeshSurfaceTriangle> Faces;

    /// <summary>Temporary buffer for calculating adjacency.</summary>
    public List<TetrahedralMeshSurfaceEdge> Edges;

    public TetrahedralMeshSurface()
    {
        Faces = [];
        Edges = [];
    }

    public TetrahedralMeshSurface(TetrahedralMeshSurface surface)
    {
        Faces = new List<TetrahedralMeshSurfaceTriangle>(surface.Faces);
        Edges = new List<TetrahedralMeshSurfaceEdge>(surface.Edges);
    }

    public readonly object Clone() => new TetrahedralMeshSurface(this);

    /// <summary>Clear.</summary>
    public void Clear() => Faces.Clear();

    /// <summary>Return size.</summary>
    public readonly uint Size => (uint)Faces.Count;

    /// <summary>Calculate adjacency information. Surface must be closed.</summary>
    public bool CalculateAdjacency()
    {
        // Collect all the edges
        Edges.Clear();
        for (uint faceIndex = 0; faceIndex < Faces.Count; ++faceIndex)
        {
            var face = Faces[(int)faceIndex];
            for (uint edgeIndex = 0; edgeIndex < 3; ++edgeIndex)
            {
                var i0 = face.Indices[(int)((edgeIndex + 1) % 3)];
                var i1 = face.Indices[(int)((edgeIndex + 2) % 3)];
                Edges.Add(new TetrahedralMeshSurfaceEdge(i0, i1, faceIndex, edgeIndex));
            }
        }

        // Match all the edges
        Edges.Sort((lhs, rhs) => lhs < rhs ? -1 : rhs < lhs ? 1 : 0);
        if (Edges.Count % 2 != 0)
            return false;

        for (var pairIndex = 0; pairIndex < Edges.Count / 2; ++pairIndex)
        {
            var firstEdge = Edges[pairIndex * 2];
            var secondEdge = Edges[pairIndex * 2 + 1];

            // Edges should be equivalent, i.e. same edge
            if (firstEdge < secondEdge || secondEdge < firstEdge)
                return false;

            var firstFace = Faces[(int)firstEdge.FaceIndex];
            var secondFace = Faces[(int)secondEdge.FaceIndex];

            // Link should not be initialized before
            if (firstFace.Neighbors[(int)firstEdge.EdgeIndex] != uint.MaxValue ||
                secondFace.Neighbors[(int)secondEdge.EdgeIndex] != uint.MaxValue)
                return false;

            firstFace.Neighbors[(int)firstEdge.EdgeIndex] = secondEdge.FaceIndex;
            secondFace.Neighbors[(int)secondEdge.EdgeIndex] = firstEdge.FaceIndex;
            Faces[(int)firstEdge.FaceIndex] = firstFace;
            Faces[(int)secondEdge.FaceIndex] = secondFace;
        }

        return true;
    }

    /// <summary>Return whether the mesh is a closed surface.</summary>
    public readonly bool IsClosedSurface()
    {
        for (var faceIndex = 0; faceIndex < Faces.Count; ++faceIndex)
        {
            var face = Faces[faceIndex];
            for (var i = 0; i < 3; ++i)
            {
                var neighborFaceIndex = face.Neighbors[i];
                if (neighborFaceIndex == uint.MaxValue)
                    return false;

                Debug.Assert(Faces[(int)neighborFaceIndex].HasNeighbor((uint)faceIndex));
            }
        }

        return true;
    }
}

/// <summary>Tetrahedron with adjacency information.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Tetrahedron
{
    [InlineArray(4)]
    [StructLayout(LayoutKind.Sequential)]
    public struct UIntArray4
    {
        private uint _element0;
    }

    /// <summary>Special index for infinite vertex of outer tetrahedron, cubic equation.</summary>
    public const uint Infinity3 = uint.MaxValue;

    /// <summary>Special index for infinite vertex of outer tetrahedron, square equation.</summary>
    public const uint Infinity2 = uint.MaxValue - 1;

    /// <summary>Indices of tetrahedron vertices.</summary>
    public UIntArray4 Indices;

    /// <summary>Indices of neighbor tetrahedrons. uint.MaxValue if empty.</summary>
    public UIntArray4 Neighbors;

    /// <summary>Pre-computed matrix for calculating barycentric coordinates.</summary>
    public Matrix3x4 Matrix;

    public Tetrahedron()
    {
        Indices = default;
        Neighbors = default;
        Neighbors[0] = uint.MaxValue;
        Neighbors[1] = uint.MaxValue;
        Neighbors[2] = uint.MaxValue;
        Neighbors[3] = uint.MaxValue;
        Matrix = Matrix3x4.Identity;
    }

    /// <summary>Calculate matrix for valid inner tetrahedron.</summary>
    public void CalculateInnerMatrix(IReadOnlyList<Vector3f> vertices)
    {
        var p0 = vertices[(int)Indices[0]];
        var p1 = vertices[(int)Indices[1]];
        var p2 = vertices[(int)Indices[2]];
        var p3 = vertices[(int)Indices[3]];
        var u1 = p1 - p0;
        var u2 = p2 - p0;
        var u3 = p3 - p0;
        var matrix = new Matrix3(u1.X, u2.X, u3.X, u1.Y, u2.Y, u3.Y, u1.Z, u2.Z, u3.Z).Inverse;
        Matrix = new Matrix3x4(matrix);
    }

    /// <summary>Return indices of specified triangle face of the tetrahedron.</summary>
    public readonly void GetTriangleFaceIndices(uint faceIndex, ref TetrahedralMeshSurfaceTriangle.UIntArray3 indices)
    {
        var j = 0;
        for (var i = 0; i < 4; ++i)
        {
            if ((uint)i != faceIndex)
                indices[j++] = Indices[i];
        }
    }

    /// <summary>Return triangle face of the tetrahedron. Adjacency information is left uninitialized.</summary>
    public readonly TetrahedralMeshSurfaceTriangle GetTriangleFace(uint faceIndex, uint tetIndex, uint tetFace)
    {
        var face = new TetrahedralMeshSurfaceTriangle();
        GetTriangleFaceIndices(faceIndex, ref face.Indices);
        face.UnusedIndex = Indices[(int)faceIndex];
        face.TetIndex = tetIndex;
        face.TetFace = tetFace;
        return face;
    }

    /// <summary>Return face index corresponding to given neighbor. Return 4 if not found.</summary>
    public readonly uint GetNeighborFaceIndex(uint neighborTetIndex)
    {
        for (uint i = 0; i < 4; ++i)
        {
            if (Neighbors[(int)i] == neighborTetIndex)
                return i;
        }

        return 4;
    }

    /// <summary>Return whether the tetrahedron has given neighbour.</summary>
    public readonly bool HasNeighbor(uint neighborTetIndex) => GetNeighborFaceIndex(neighborTetIndex) < 4;
}
