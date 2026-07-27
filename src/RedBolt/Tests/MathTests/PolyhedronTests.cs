using RedBolt;

namespace RedBolt.Tests;

public sealed class PolyhedronTests
{
    [Fact]
    public void Class_shape_and_nested_face_construction_match_native()
    {
        Assert.False(typeof(Polyhedron).IsValueType);
        Assert.False(typeof(Polyhedron).IsSealed);
        Assert.True(typeof(Polyhedron).IsAssignableTo(typeof(ICloneable)));

        var inputFace = new List<Vector3f>
        {
            Vector3f.Zero,
            Vector3f.Right,
            Vector3f.Up
        };
        IReadOnlyList<IReadOnlyList<Vector3f>> inputFaces = new IReadOnlyList<Vector3f>[] { inputFace };
        var polyhedron = new Polyhedron(inputFaces);

        inputFace[0] = Vector3f.One;

        Assert.Equal(Vector3f.Zero, polyhedron.Faces[0][0]);
        Assert.NotSame(inputFace, polyhedron.Faces[0]);
    }

    [Fact]
    public void Bounding_box_definition_and_copy_preserve_faces()
    {
        var polyhedron = new Polyhedron(new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f)));
        var clone = (Polyhedron)polyhedron.Clone();

        Assert.False(polyhedron.Empty());
        Assert.Equal(6, polyhedron.Faces.Count);
        Assert.All(polyhedron.Faces, face => Assert.Equal(4, face.Count));
        Assert.Equal(polyhedron.Faces, clone.Faces);
        Assert.NotSame(polyhedron.Faces, clone.Faces);
        Assert.NotSame(polyhedron.Faces[0], clone.Faces[0]);
    }

    [Fact]
    public void Copy_construction_and_assignment_copy_only_faces()
    {
        var source = new Polyhedron(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), Vector3f.One));
        source.Clip(new Plane(Vector3f.Right, Vector3f.Zero));

        var sourceOutFace = GetScratchFace(source, "_outFace");
        Assert.NotEmpty(sourceOutFace);

        var copy = new Polyhedron(source);
        var clone = (Polyhedron)source.Clone();
        Assert.Empty(GetScratchFace(copy, "_outFace"));
        Assert.Empty(GetScratchFace(clone, "_outFace"));

        var target = new Polyhedron(new BoundingBox(new Vector3f(-2.0f, -2.0f, -2.0f), new Vector3f(2.0f, 2.0f, 2.0f)));
        target.Clip(new Plane(Vector3f.Up, Vector3f.Zero));
        var targetOutFace = GetScratchFace(target, "_outFace");
        var targetOutFaceCopy = targetOutFace.ToArray();

        Assert.Same(target, target.CopyAssign(source));
        Assert.Same(targetOutFace, GetScratchFace(target, "_outFace"));
        Assert.Equal(targetOutFaceCopy, targetOutFace);
        Assert.NotSame(source.Faces, target.Faces);
        Assert.NotSame(source.Faces[0], target.Faces[0]);

        source.Faces[0][0] = new Vector3f(10.0f, 20.0f, 30.0f);
        Assert.NotEqual(source.Faces[0][0], copy.Faces[0][0]);
        Assert.NotEqual(source.Faces[0][0], clone.Faces[0][0]);
        Assert.NotEqual(source.Faces[0][0], target.Faces[0][0]);

        var assignedFaces = target.Faces.Select(face => face.ToArray()).ToArray();
        Assert.Same(target, target.CopyAssign(target));
        Assert.Equal(assignedFaces.Length, target.Faces.Count);
        for (var i = 0; i < assignedFaces.Length; ++i)
            Assert.Equal(assignedFaces[i], target.Faces[i]);
    }

    [Fact]
    public void Define_resizes_faces_and_preserves_existing_face_storage()
    {
        var polyhedron = new Polyhedron(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), Vector3f.One));
        var originalFaces = polyhedron.Faces.ToArray();
        polyhedron.AddFace(Vector3f.Zero, Vector3f.Right, Vector3f.Up);

        polyhedron.Define(new BoundingBox(new Vector3f(-2.0f, -3.0f, -4.0f), new Vector3f(2.0f, 3.0f, 4.0f)));

        Assert.Equal(6, polyhedron.Faces.Count);
        for (var i = 0; i < originalFaces.Length; ++i)
            Assert.Same(originalFaces[i], polyhedron.Faces[i]);

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 2.0f));
        polyhedron.Define(frustum);

        Assert.Equal(6, polyhedron.Faces.Count);
        for (var i = 0; i < originalFaces.Length; ++i)
            Assert.Same(originalFaces[i], polyhedron.Faces[i]);
    }

    [Fact]
    public void Faces_can_be_added_cleared_and_transformed()
    {
        var polyhedron = new Polyhedron();
        polyhedron.AddFace(Vector3f.Zero, new Vector3f(1.0f, 0.0f, 0.0f), new Vector3f(0.0f, 1.0f, 0.0f));
        polyhedron.AddFace(new Vector3f(0.0f, 0.0f, 1.0f), new Vector3f(1.0f, 0.0f, 1.0f), new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(0.0f, 1.0f, 1.0f));

        var translated = polyhedron.Transformed(Matrix3x4.FromTranslation(new Vector3f(5.0f, 0.0f, 0.0f)));

        Assert.Equal(2, translated.Faces.Count);
        Assert.Equal(6.0f, translated.Faces[0][1].X);
        polyhedron.Transform(Matrix3x4.FromTranslation(new Vector3f(5.0f, 0.0f, 0.0f)));
        Assert.Equal(6.0f, polyhedron.Faces[0][1].X);
        polyhedron.Clear();
        Assert.True(polyhedron.Empty());
    }

    [Fact]
    public void Arbitrary_faces_and_both_transform_overloads_copy_values()
    {
        var inputFace = new List<Vector3f> { Vector3f.Zero, Vector3f.Right, Vector3f.Up };
        var polyhedron = new Polyhedron();
        polyhedron.AddFace(inputFace);
        inputFace[0] = Vector3f.One;
        Assert.Equal(Vector3f.Zero, polyhedron.Faces[0][0]);

        var scale = new Matrix3(
            2.0f, 0.0f, 0.0f,
            0.0f, 3.0f, 0.0f,
            0.0f, 0.0f, 4.0f);
        var scaled = polyhedron.Transformed(scale);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), scaled.Faces[0][1]);
        Assert.Equal(Vector3f.Right, polyhedron.Faces[0][1]);

        polyhedron.Transform(scale);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), polyhedron.Faces[0][1]);

        var translated = polyhedron.Transformed(Matrix3x4.FromTranslation(new Vector3f(5.0f, 6.0f, 7.0f)));
        Assert.Equal(new Vector3f(7.0f, 6.0f, 7.0f), translated.Faces[0][1]);
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), polyhedron.Faces[0][1]);
    }

    [Fact]
    public void Plane_clipping_keeps_visible_faces()
    {
        var polyhedron = new Polyhedron(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));

        polyhedron.Clip(new Plane(new Vector3f(1.0f, 0.0f, 0.0f), Vector3f.Zero));

        Assert.NotEmpty(polyhedron.Faces);
        Assert.All(polyhedron.Faces, face =>
        {
            Assert.True(face.Count >= 3);
            Assert.All(face, vertex => Assert.True(vertex.X >= -MathDefs.Epsilon));
        });
    }

    [Fact]
    public void Bounding_box_and_frustum_clipping_keep_vertices_inside()
    {
        var polyhedron = new Polyhedron(new BoundingBox(new Vector3f(-2.0f, -2.0f, -2.0f), new Vector3f(2.0f, 2.0f, 2.0f)));
        var clipBox = new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), Vector3f.One);

        polyhedron.Clip(clipBox);

        Assert.All(polyhedron.Faces, face => Assert.All(face, vertex =>
        {
            Assert.True(vertex.X >= -1.0f - MathDefs.Epsilon && vertex.X <= 1.0f + MathDefs.Epsilon);
            Assert.True(vertex.Y >= -1.0f - MathDefs.Epsilon && vertex.Y <= 1.0f + MathDefs.Epsilon);
            Assert.True(vertex.Z >= -1.0f - MathDefs.Epsilon && vertex.Z <= 1.0f + MathDefs.Epsilon);
        }));

        var frustum = new Frustum();
        frustum.Define(new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(2.0f, 2.0f, 2.0f));
        polyhedron.Clip(frustum);

        Assert.All(polyhedron.Faces, face => Assert.All(face, vertex =>
            Assert.All(frustum.Planes, plane => Assert.True(plane.Distance(vertex) >= -MathDefs.Epsilon))));
    }

    private static List<Vector3f> GetScratchFace(Polyhedron polyhedron, string name)
    {
        return (List<Vector3f>)typeof(Polyhedron)
            .GetField(name, global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic)!
            .GetValue(polyhedron)!;
    }
}
