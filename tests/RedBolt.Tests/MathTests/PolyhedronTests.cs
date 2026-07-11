using RedBolt;

namespace RedBolt.Tests;

public sealed class PolyhedronTests
{
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
    public void Faces_can_be_added_cleared_and_transformed()
    {
        var polyhedron = new Polyhedron();
        polyhedron.AddFace(Vector3f.Zero, new Vector3f(1.0f, 0.0f, 0.0f), new Vector3f(0.0f, 1.0f, 0.0f));
        polyhedron.AddFace(new Vector3f(0.0f, 0.0f, 1.0f), new Vector3f(1.0f, 0.0f, 1.0f), new Vector3f(1.0f, 1.0f, 1.0f), new Vector3f(0.0f, 1.0f, 1.0f));

        var translated = polyhedron.Transformed(new Matrix3x4(1.0f, 0.0f, 0.0f, 5.0f));

        Assert.Equal(2, translated.Faces.Count);
        Assert.Equal(6.0f, translated.Faces[0][1].X);
        polyhedron.Transform(new Matrix3x4(1.0f, 0.0f, 0.0f, 5.0f));
        Assert.Equal(6.0f, polyhedron.Faces[0][1].X);
        polyhedron.Clear();
        Assert.True(polyhedron.Empty());
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
}
