using RedBolt;

namespace RedBolt.Tests;

public sealed class ModelViewTests
{
    [Fact]
    public void Model_view_cpu_data_and_geometry_transforms_work()
    {
        var format = new ModelVertexFormat
        {
            Position = VertexElementType.Vector3,
            Normal = VertexElementType.Vector3,
            Uv = [VertexElementType.Vector2, ModelVertexFormat.Undefined, ModelVertexFormat.Undefined, ModelVertexFormat.Undefined]
        };
        var elements = format.ToVertexElements();
        Assert.Equal(3, elements.Count);
        Assert.Equal(VertexElementSemantic.Position, elements[0].Semantic);
        Assert.Equal(VertexElementSemantic.Normal, elements[1].Semantic);
        Assert.Equal(VertexElementSemantic.TexCoord, elements[2].Semantic);

        var lod = new GeometryLODView { VertexFormat = format };
        lod.Vertices.Add(CreateVertex(-1, 0, 0));
        lod.Vertices.Add(CreateVertex(1, 0, 0));
        lod.Vertices.Add(CreateVertex(0, 1, 0));
        lod.Indices.AddRange([0, 1, 2]);
        Assert.Equal(1u, lod.GetNumPrimitives());
        Assert.True(lod.IsTriangleGeometry());
        Assert.Equal(new Vector3f(0, 1f / 3, 0), lod.CalculateCenter());

        var triangles = new List<(uint, uint, uint)>();
        lod.ForEachTriangle((a, b, c) => triangles.Add((a, b, c)));
        Assert.Equal([(0u, 1u, 2u)], triangles);

        var view = new ModelView();
        view.SetGeometries([new GeometryView { Lods = [lod], Material = "Materials/Test.xml" }]);
        Assert.Equal(new BoundingBox(new Vector3f(-1, 0, 0), new Vector3f(1, 1, 0)), view.CalculateBoundingBox());

        view.ScaleGeometries(2);
        Assert.Equal(-2, view.GetGeometries()[0].Lods[0].Vertices[0].GetPosition().X);
        view.MirrorGeometriesX();
        Assert.Equal(2, view.GetGeometries()[0].Lods[0].Vertices[0].GetPosition().X);
    }

    [Fact]
    public void Model_view_repairs_morphs_bones_and_normals()
    {
        var morphs = new List<ModelVertexMorph>
        {
            new() { Index = 1, PositionDelta = new Vector3f(1, 0, 0) },
            new() { Index = 0 },
            new() { Index = 1, NormalDelta = new Vector3f(0, 1, 0) }
        };
        ModelViewFunctions.NormalizeModelVertexMorphVector(morphs);
        Assert.Single(morphs);
        Assert.Equal(1u, morphs[0].Index);

        var lod = new GeometryLODView();
        lod.Vertices.Add(CreateVertex(0, 0, 0));
        lod.Vertices.Add(CreateVertex(1, 0, 0));
        lod.Vertices.Add(CreateVertex(0, 1, 0));
        lod.Indices.AddRange([0, 1, 2]);
        var geometry = new GeometryView { Lods = [lod] };

        var view = new ModelView();
        view.SetGeometries([geometry]);
        view.SetBones([new BoneView { Name = "Root", ParentIndex = uint.MaxValue }]);
        view.CalculateMissingNormals();
        Assert.True(view.GetGeometries()[0].Lods[0].Vertices[0].HasNormal());

        var bone = view.GetBones()[0];
        bone.SetLocalBoundingSphere(2);
        Assert.Equal(BoneCollisionShape.Sphere, bone.ShapeFlags);
        bone.RecalculateOffsetMatrix();
        Assert.Equal(Matrix3x4.IDENTITY, bone.OffsetMatrix);
    }

    [Fact]
    public void Model_view_recalculates_tangents_from_triangle_data()
    {
        var lod = new GeometryLODView
        {
            VertexFormat = new ModelVertexFormat
            {
                Position = VertexElementType.Vector3,
                Normal = VertexElementType.Vector3,
                Tangent = VertexElementType.Vector4,
                Uv = [VertexElementType.Vector2, ModelVertexFormat.Undefined, ModelVertexFormat.Undefined,
                    ModelVertexFormat.Undefined]
            }
        };
        lod.Vertices.Add(CreateTangentVertex(0, 0, 0, 0, 0));
        lod.Vertices.Add(CreateTangentVertex(1, 0, 0, 1, 0));
        lod.Vertices.Add(CreateTangentVertex(0, 1, 0, 0, 1));
        lod.Indices.AddRange([0, 1, 2]);

        lod.RecalculateTangents();

        Assert.Equal(new Vector3f(1, 0, 0), lod.Vertices[0].GetTangent());
    }

    private static ModelVertex CreateVertex(float x, float y, float z)
    {
        var vertex = new ModelVertex();
        vertex.SetPosition(new Vector3f(x, y, z));
        return vertex;
    }

    private static ModelVertex CreateTangentVertex(float x, float y, float z, float u, float v)
    {
        var vertex = CreateVertex(x, y, z);
        vertex.SetNormal(new Vector3f(0, 0, 1));
        vertex.Uv[0] = new Vector4f(u, v, 0, 0);
        return vertex;
    }
}
