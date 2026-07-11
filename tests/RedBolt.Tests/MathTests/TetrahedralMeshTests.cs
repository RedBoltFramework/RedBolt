using RedBolt;

namespace RedBolt.Tests;

public sealed class TetrahedralMeshTests
{
    [Fact]
    public void Define_builds_inner_and_outer_tetrahedrons_for_simple_input()
    {
        var mesh = new TetrahedralMesh();
        mesh.Define(
        [
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(1.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 1.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 1.0f)
        ]);

        Assert.Equal(12, mesh.Vertices.Count);
        Assert.True(mesh.NumInnerTetrahedrons > 0);
        Assert.True(mesh.Tetrahedrons.Count > mesh.NumInnerTetrahedrons);
        Assert.Empty(mesh.IgnoredVertices);
    }

    [Fact]
    public void Inner_barycentric_coordinates_interpolate_a_tetrahedron()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        ]);
        var tetrahedron = new Tetrahedron();
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 1;
        tetrahedron.Indices[2] = 2;
        tetrahedron.Indices[3] = 3;
        tetrahedron.CalculateInnerMatrix(mesh.Vertices);
        mesh.Tetrahedrons.Add(tetrahedron);
        mesh.NumInnerTetrahedrons = 1;

        var result = mesh.GetInnerBarycentricCoords(0, new Vector3f(0.25f, 0.25f, 0.25f));

        Assert.True(result.ApproximatelyEquivalent(new Vector4f(0.25f, 0.25f, 0.25f, 0.25f), 1e-5f));
    }

    [Fact]
    public void Sample_interpolates_per_vertex_vectors()
    {
        var mesh = new TetrahedralMesh();
        mesh.Vertices.AddRange(
        [
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        ]);
        var tetrahedron = new Tetrahedron();
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 1;
        tetrahedron.Indices[2] = 2;
        tetrahedron.Indices[3] = 3;
        tetrahedron.CalculateInnerMatrix(mesh.Vertices);
        mesh.Tetrahedrons.Add(tetrahedron);
        mesh.NumInnerTetrahedrons = 1;
        var values = new List<Vector3f>
        {
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up,
            Vector3f.Forward
        };
        var hint = 0u;

        var result = mesh.Sample(values, new Vector3f(0.25f, 0.25f, 0.25f), ref hint);

        Assert.True(result.ApproximatelyEquivalent(new Vector3f(0.25f, 0.25f, 0.25f), 1e-5f));
    }
}
