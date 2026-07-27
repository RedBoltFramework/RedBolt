using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class TetrahedralMeshTypesTests
{
    [Fact]
    public void Pod_aggregates_have_native_layout_and_independent_inline_storage()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<HighPrecisionSphere>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TetrahedralMeshSurfaceTriangle>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TetrahedralMeshSurfaceEdge>());
        Assert.Equal(32, Marshal.SizeOf<HighPrecisionSphere>());
        Assert.Equal(36, Marshal.SizeOf<TetrahedralMeshSurfaceTriangle>());
        Assert.Equal(16, Marshal.SizeOf<TetrahedralMeshSurfaceEdge>());

        var triangle = new TetrahedralMeshSurfaceTriangle();
        Assert.Equal(uint.MaxValue, triangle.UnusedIndex);
        Assert.Equal(uint.MaxValue, triangle.Neighbors[0]);
        Assert.Equal(uint.MaxValue, triangle.Neighbors[1]);
        Assert.Equal(uint.MaxValue, triangle.Neighbors[2]);
        Assert.Equal(uint.MaxValue, triangle.TetIndex);
        var triangleCopy = triangle;
        triangleCopy.Indices[0] = 1;
        triangleCopy.Neighbors[0] = 2;
        Assert.Equal(0u, triangle.Indices[0]);
        Assert.Equal(uint.MaxValue, triangle.Neighbors[0]);

        var edge = new TetrahedralMeshSurfaceEdge(4, 3, 2, 1);
        var edgeCopy = edge;
        edgeCopy.Indices[0] = 7;
        Assert.Equal(3u, edge.Indices[0]);
        Assert.Equal(4u, edge.Indices[1]);
        Assert.Equal(7u, edgeCopy.Indices[0]);
    }

    [Fact]
    public void Surface_copy_constructor_and_clone_copy_owned_vectors_independently()
    {
        var source = new TetrahedralMeshSurface();
        var triangle = new TetrahedralMeshSurfaceTriangle();
        triangle.Indices[0] = 3;
        source.Faces.Add(triangle);
        source.Edges.Add(new TetrahedralMeshSurfaceEdge(1, 2, 3, 4));

        var copy = new TetrahedralMeshSurface(source);
        var clone = (TetrahedralMeshSurface)source.Clone();

        Assert.IsAssignableFrom<ICloneable>(source);
        Assert.NotSame(source.Faces, copy.Faces);
        Assert.NotSame(source.Edges, copy.Edges);
        var copiedTriangle = copy.Faces[0];
        copiedTriangle.Indices[0] = 9;
        copy.Faces[0] = copiedTriangle;
        clone.Edges.Clear();
        Assert.Equal(3u, source.Faces[0].Indices[0]);
        Assert.Single(source.Edges);
    }

    [Fact]
    public void Tetrahedron_is_pod_and_copies_inline_arrays_independently()
    {
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Tetrahedron>());
        Assert.Equal(80, System.Runtime.InteropServices.Marshal.SizeOf<Tetrahedron>());

        var source = new Tetrahedron();
        Assert.Equal(Matrix3x4.Identity, source.Matrix);
        source.Indices[0] = 1;
        source.Neighbors[0] = 2;

        var copy = source;
        copy.Indices[0] = 3;
        copy.Neighbors[0] = 4;

        Assert.Equal(1u, source.Indices[0]);
        Assert.Equal(2u, source.Neighbors[0]);
        Assert.Equal(3u, copy.Indices[0]);
        Assert.Equal(4u, copy.Neighbors[0]);
    }

    [Fact]
    public void Surface_adjacency_links_two_triangles_per_edge()
    {
        var surface = new TetrahedralMeshSurface();
        var first = new TetrahedralMeshSurfaceTriangle();
        first.Indices[0] = 0;
        first.Indices[1] = 1;
        first.Indices[2] = 2;
        var second = new TetrahedralMeshSurfaceTriangle();
        second.Indices[0] = 0;
        second.Indices[1] = 2;
        second.Indices[2] = 3;
        surface.Faces.Add(first);
        surface.Faces.Add(second);

        Assert.False(surface.CalculateAdjacency());
        Assert.False(surface.IsClosedSurface());
        Assert.Equal(6, surface.Edges.Count);
    }

    [Fact]
    public void Tetrahedron_calculates_inner_barycentric_matrix_and_faces()
    {
        var vertices = new List<Vector3f>
        {
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        };
        var tetrahedron = new Tetrahedron();
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 1;
        tetrahedron.Indices[2] = 2;
        tetrahedron.Indices[3] = 3;

        tetrahedron.CalculateInnerMatrix(vertices);
        var face = tetrahedron.GetTriangleFace(0, 7, 2);

        Assert.Equal(1u, face.Indices[0]);
        Assert.Equal(2u, face.Indices[1]);
        Assert.Equal(3u, face.Indices[2]);
        Assert.Equal(0u, face.UnusedIndex);
        Assert.Equal(7u, face.TetIndex);
        Assert.Equal(2u, face.TetFace);
        Assert.False(tetrahedron.HasNeighbor(7));
    }
}
