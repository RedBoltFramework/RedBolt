using RedBolt;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class TetrahedralMeshTests
{
    [Fact]
    public void Implementation_helpers_match_native_value_and_reference_semantics()
    {
        Assert.True(typeof(TetrahedralMesh).IsClass);

        var edgeType = typeof(TetrahedralMesh).GetNestedType("TetrahedralMeshEdge", BindingFlags.NonPublic)!;
        var indicesField = edgeType.GetField("Indices", BindingFlags.Instance | BindingFlags.Public)!;
        var elementField = indicesField.FieldType.GetField("_element0", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.True(edgeType.IsValueType);
        var containsReferencesMethod = typeof(RuntimeHelpers)
            .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences), BindingFlags.Public | BindingFlags.Static)!
            .GetGenericMethodDefinition();
        Assert.False((bool)containsReferencesMethod.MakeGenericMethod(edgeType).Invoke(null, null)!);
        Assert.Equal(20, Marshal.SizeOf(edgeType));

        var source = Activator.CreateInstance(edgeType)!;
        var sourceIndices = indicesField.GetValue(source)!;
        elementField.SetValue(sourceIndices, 7u);
        indicesField.SetValue(source, sourceIndices);

        var copy = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(source, null)!;
        var copyIndices = indicesField.GetValue(copy)!;
        elementField.SetValue(copyIndices, 9u);
        indicesField.SetValue(copy, copyIndices);

        Assert.Equal(7u, elementField.GetValue(indicesField.GetValue(source)!));
        Assert.Equal(9u, elementField.GetValue(indicesField.GetValue(copy)!));

        var contextType = typeof(TetrahedralMesh).GetNestedType("DelaunayContext", BindingFlags.NonPublic)!;
        Assert.True(contextType.IsValueType);
        Assert.True((bool)containsReferencesMethod.MakeGenericMethod(contextType).Invoke(null, null)!);
        var findMethod = typeof(TetrahedralMesh).GetMethod("FindAndRemoveIntersected", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var fillMethod = typeof(TetrahedralMesh).GetMethod("FillStarShapedHole", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(contextType.MakeByRefType(), findMethod.GetParameters()[0].ParameterType);
        Assert.Equal(contextType.MakeByRefType(), fillMethod.GetParameters()[0].ParameterType);
    }

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
