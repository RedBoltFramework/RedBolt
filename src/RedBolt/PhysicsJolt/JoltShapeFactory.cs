namespace RedBolt.PhysicsJolt;

using RedBolt.Bindings.Jolt;
using RedBolt.Math;

internal static class JoltShapeFactory
{
    internal static Shape CreatePlane()
    {
        using var box = new BoxShape(new Vector3f(1000.0f, 1000.0f, 1000.0f));
        return new RotatedTranslatedShape(new Vector3f(0.0f, -1000.0f, 0.0f), Quaternion.IDENTITY, box);
    }

    internal static Shape? CreateCone(float halfHeight, float radius, float convexRadius)
    {
        using var settings = new TaperedCylinderShapeSettings(
            MathF.Max(halfHeight, 0.001f), 0.0f, MathF.Max(radius, 0.001f), MathF.Max(convexRadius, 0.0f));
        return Create(settings);
    }

    internal static Shape? CreateHeightField(
        float[] samples, uint sampleCount, Vector3f offset, Vector3f scale)
    {
        using var heightSamples = ToArray(samples);
        using var settings = new HeightFieldShapeSettings
        {
            MHeightSamples = heightSamples,
            MSampleCount = sampleCount,
            MOffset = offset,
            MScale = scale
        };
        return Create(settings);
    }

    internal static Shape? CreateConvexHull(Vector3f[] vertices, float convexRadius)
    {
        using var points = ToArray(vertices);
        using var settings = new ConvexHullShapeSettings(points, MathF.Max(convexRadius, 0.0f));
        return Create(settings);
    }

    internal static Shape? CreateMesh(Vector3f[] vertices, uint[] indices)
    {
        using var nativeVertices = new global::JPH.Array<Float3>();
        for (var i = 0; i < vertices.Length; ++i)
        {
            using var vertex = new Float3(vertices[i].X, vertices[i].Y, vertices[i].Z);
            nativeVertices.Add(vertex);
        }

        using var triangles = new global::JPH.Array<IndexedTriangle>();
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            if (indices[i] >= (uint)vertices.Length || indices[i + 1] >= (uint)vertices.Length ||
                indices[i + 2] >= (uint)vertices.Length)
                continue;
            using var triangle = new IndexedTriangle(indices[i], indices[i + 1], indices[i + 2], 0);
            triangles.Add(triangle);
        }

        if (triangles.Count == 0)
            return null;
        using var settings = new MeshShapeSettings(nativeVertices, triangles);
        return Create(settings);
    }

    internal static Shape? CreateTriangleCompound(Vector3f[] vertices, uint[] indices, float convexRadius)
    {
        using var settings = CreateStaticCompoundSettings();
        var added = false;
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            var i0 = indices[i];
            var i1 = indices[i + 1];
            var i2 = indices[i + 2];
            if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                continue;
            using var triangle = new TriangleShape(
                vertices[i0], vertices[i1], vertices[i2], MathF.Max(convexRadius, 0.0f));
            settings.AddShape(Vector3f.Zero, Quaternion.IDENTITY, triangle);
            added = true;
        }
        return added ? Create(settings) : null;
    }

    internal static Shape? CreateCompound(IReadOnlyList<Shape> shapes)
    {
        using var settings = CreateStaticCompoundSettings();
        for (var i = 0; i < shapes.Count; ++i)
            settings.AddShape(Vector3f.Zero, Quaternion.IDENTITY, shapes[i]);
        return shapes.Count != 0 ? Create(settings) : null;
    }

    private static Shape? Create(ShapeSettings settings)
    {
        using var result = settings.Create();
        return result.IsValid() ? result.Get() : null;
    }

    private static StaticCompoundShapeSettings CreateStaticCompoundSettings() =>
        new();

    private static global::JPH.Array<T> ToArray<T>(IEnumerable<T> source)
    {
        var result = new global::JPH.Array<T>();
        foreach (var item in source)
            result.Add(item);
        return result;
    }
}
