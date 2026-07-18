using RedBolt;

namespace RedBolt.Tests;

public sealed class ModelViewTests
{
    [Fact]
    public void Model_view_value_clones_are_independent()
    {
        var vertex = new ModelVertex();
        vertex.Color[0] = Vector4f.One;
        vertex.Uv[0] = Vector4f.One;
        var vertexClone = vertex.Clone();
        vertex.Color[0] = Vector4f.Zero;
        vertex.Uv[0] = Vector4f.Zero;

        Assert.Equal(Vector4f.One, vertexClone.Color[0]);
        Assert.Equal(Vector4f.One, vertexClone.Uv[0]);

        var lod = new GeometryLODView();
        lod.Vertices.Add(vertexClone);
        lod.Indices.Add(0);
        lod.VertexFormat.Position = VertexElementType.Vector3;
        lod.Morphs[0] = [new ModelVertexMorph { Index = 0, PositionDelta = Vector3f.One }];
        var geometry = new GeometryView();
        geometry.Lods.Add(lod);
        var geometryClone = geometry.Clone();

        geometry.Lods[0].Vertices[0].Color[0] = Vector4f.Zero;
        geometry.Lods[0].Indices[0] = 1;
        geometry.Lods[0].VertexFormat.Color[0] = VertexElementType.Vector4;
        geometry.Lods[0].Morphs[0][0] = new ModelVertexMorph();

        Assert.Equal(Vector4f.One, geometryClone.Lods[0].Vertices[0].Color[0]);
        Assert.Equal(0u, geometryClone.Lods[0].Indices[0]);
        Assert.Equal(ModelVertexFormat.Undefined, geometryClone.Lods[0].VertexFormat.Color[0]);
        Assert.Equal(Vector3f.One, geometryClone.Lods[0].Morphs[0][0].PositionDelta);
    }

    [Fact]
    public void Set_contents_copies_cpp_values()
    {
        using var view = new ModelView();
        var lod = new GeometryLODView();
        lod.Vertices.Add(new ModelVertex());
        var geometry = new GeometryView();
        geometry.Lods.Add(lod);
        var geometries = new List<GeometryView> { geometry };
        var bones = new List<BoneView> { new() { Name = "Bone" } };
        var morphs = new List<ModelMorphView> { new() { Name = "Morph" } };

        view.SetGeometries(geometries);
        view.SetBones(bones);
        view.SetMorphs(morphs);
        geometries[0].Lods[0].Vertices[0].Color[0] = Vector4f.One;
        geometries.Clear();
        bones[0] = new BoneView();
        morphs[0] = new ModelMorphView();

        Assert.Single(view.GetGeometries());
        Assert.Equal(Vector4f.Zero, view.GetGeometries()[0].Lods[0].Vertices[0].Color[0]);
        Assert.Equal("Bone", view.GetBones()[0].Name);
        Assert.Equal("Morph", view.GetMorphs()[0].Name);
    }

    [Fact]
    public void Clear_preserves_name_and_morphs_but_releases_other_contents()
    {
        using var view = new ModelView();
        view.SetName("Model");
        view.SetGeometries([new GeometryView()]);
        view.SetBones([new BoneView()]);
        view.SetMorphs([new ModelMorphView { Name = "Morph" }]);
        view.AddMetadata("Value", new Variant(42));

        view.Clear();

        Assert.Equal("Model", view.GetName());
        Assert.Equal("Morph", view.GetMorphs()[0].Name);
        Assert.Empty(view.GetGeometries());
        Assert.Empty(view.GetBones());
        Assert.True(view.GetMetadata("Value").IsEmpty);
    }

    [Fact]
    public void Metadata_is_copied_on_assignment()
    {
        using var view = new ModelView();
        var values = new VariantVector { new Variant("Value") };
        var value = new Variant(values);
        values.Dispose();

        view.AddMetadata("Value", value);
        value.Dispose();

        Assert.Equal("Value", view.GetMetadata("Value").GetVariantVector()[0].GetString());
    }

    [Fact]
    public void Flat_normals_duplicate_morphs_for_split_vertices()
    {
        var lod = new GeometryLODView();
        foreach (var position in new[]
                 {
                     Vector3f.Zero, Vector3f.Right, Vector3f.Up,
                     Vector3f.Zero, Vector3f.Up, Vector3f.Forward
                 })
        {
            var vertex = new ModelVertex();
            vertex.SetPosition(position);
            lod.Vertices.Add(vertex);
        }
        lod.Indices.AddRange([0, 1, 2, 0, 2, 5]);
        lod.Morphs[0] = [new ModelVertexMorph { Index = 0, PositionDelta = Vector3f.One }];

        lod.RecalculateFlatNormals();

        Assert.Equal([0u, 3u], lod.Morphs[0].Select(static morph => morph.Index));
    }

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

    [Fact]
    public void Model_view_exports_and_imports_geometry_morphs_skeleton_and_metadata()
    {
        var format = new ModelVertexFormat
        {
            Position = VertexElementType.Vector3,
            Normal = VertexElementType.Vector3
        };
        var lod = new GeometryLODView
        {
            PrimitiveType = PrimitiveType.TriangleList,
            LodDistance = 4.0f,
            VertexFormat = format
        };
        lod.Vertices.Add(CreateTangentVertex(0, 0, 0, 0, 0));
        lod.Vertices.Add(CreateTangentVertex(1, 0, 0, 1, 0));
        lod.Vertices.Add(CreateTangentVertex(0, 1, 0, 0, 1));
        lod.Indices.AddRange([0, 1, 2]);
        lod.Morphs[0] = [new ModelVertexMorph { Index = 1, PositionDelta = new Vector3f(0, 0, 2) }];

        var view = new ModelView();
        view.SetName("Models/RoundTrip.mdl");
        view.SetGeometries([new GeometryView { Lods = [lod] }]);
        view.SetMorphs([new ModelMorphView { Name = "Raise", InitialWeight = 0.25f }]);
        view.SetBones([new BoneView
        {
            Name = "Root",
            ParentIndex = uint.MaxValue,
            Position = new Vector3f(1, 2, 3),
            Rotation = Quaternion.IDENTITY,
            Scale = Vector3f.ONE,
            OffsetMatrix = Matrix3x4.IDENTITY,
            ShapeFlags = BoneCollisionShape.Sphere,
            BoundingSphereRadius = 2.0f
        }]);
        view.AddMetadata("Tag", new Variant("round-trip"));

        var model = view.ExportModel(ModelViewExportFlag.Headless);
        var imported = new ModelView();

        Assert.True(imported.ImportModel(model));
        Assert.Equal("Models/RoundTrip.mdl", imported.GetName());
        Assert.Equal("round-trip", imported.GetMetadata("Tag").GetString());
        Assert.Equal(4.0f, imported.GetGeometries()[0].Lods[0].LodDistance);
        Assert.Equal([0u, 1u, 2u], imported.GetGeometries()[0].Lods[0].Indices);
        Assert.Equal(new Vector3f(0, 0, 2), imported.GetGeometries()[0].Lods[0].Morphs[0][0].PositionDelta);
        Assert.Equal("Raise", imported.GetMorphs()[0].Name);
        Assert.Equal(0.25f, imported.GetMorphs()[0].InitialWeight);
        Assert.Equal("Root", imported.GetBones()[0].Name);
        Assert.Equal(uint.MaxValue, imported.GetBones()[0].ParentIndex);
        Assert.Equal(2.0f, imported.GetBones()[0].BoundingSphereRadius);
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
