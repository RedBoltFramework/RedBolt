using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CustomGeometryTests
{
    private readonly HeadlessEngineFixture _fixture;

    public CustomGeometryTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Defaults_are_correct()
    {
        var custom = new Scene().CreateChild("Custom").CreateComponent<CustomGeometry>();
        Assert.Equal(DrawableFlags.Geometry, custom.GetDrawableFlags());
        Assert.Equal(1u, custom.GetNumGeometries());
        Assert.False(custom.IsDynamic());
    }

    [Fact]
    public void Dynamic_vertex_definition_and_commit_work()
    {
        var custom = new Scene().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);

        Assert.True(custom.BeginGeometry(0u, PrimitiveType.TriangleList));
        custom.DefineVertex(new Vector3(-1.0f, -1.0f, 0.0f));
        custom.DefineNormal(Vector3.Forward);
        custom.DefineColor(new Color(1.0f, 0.0f, 0.0f));
        custom.DefineTexCoord(Vector2.Zero);
        custom.DefineTangent(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        custom.DefineVertex(new Vector3(1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3(0.0f, 1.0f, 0.0f));

        Assert.True(custom.DefineGeometry(1u, PrimitiveType.LineList, 2u,
            true, true, true, true));

        var editable = custom.GetVertex(1u, 1u);
        Assert.NotNull(editable);
        editable.Position = new Vector3(2.0f, 0.0f, 0.0f);
        editable.Normal = Vector3.Up;
        editable.Color = new Color(1.0f, 1.0f, 0.0f).ToUInt();
        editable.TexCoord = Vector2.One;
        editable.Tangent = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);

        var material = Resources.Get<Material>("Materials/FirstCube");
        custom.SetMaterial(material);
        custom.Commit();

        Assert.True(custom.IsDynamic());
        Assert.Equal(3u, custom.GetNumVertices(0u));
        Assert.Equal(2u, custom.GetNumVertices(1u));
    }

    [Fact]
    public void Vertex_buffer_metadata_is_correct()
    {
        var custom = new Scene().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(Vector3.Zero);
        custom.DefineVertex(Vector3.Zero);
        custom.DefineVertex(Vector3.Zero);
        custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, false, false, false, false);
        custom.Commit();

        Assert.Equal(5u, custom.GetVertexBuffer().GetVertexCount());
        Assert.Equal(12u, custom.GetVertexBuffer().GetVertexSize());
        Assert.Equal(60, custom.GetVertexBuffer().GetData().Count);
    }

    [Fact]
    public void Bounding_box_and_material_work()
    {
        var custom = new Scene().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3(0.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3(2.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3(0.0f, 2.0f, 0.0f));
        var material = Resources.Get<Material>("Materials/FirstCube");
        custom.SetMaterial(material);
        custom.Commit();

        Assert.Same(material, custom.GetMaterial());
        Assert.Equal(Intersection.Inside,
            custom.GetBoundingBox().IsInside(new Vector3(1.0f, 0.5f, 0.0f)));
    }

    [Fact]
    public void Geometry_data_attribute_roundtrips()
    {
        var custom = new Scene().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3(-1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3(1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3(0.0f, 1.0f, 0.0f));
        custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, false, false, false, false);
        custom.GetVertex(1u, 1u)!.Position = new Vector3(2.0f, 0.0f, 0.0f);
        custom.Commit();

        var data = custom.GetGeometryDataAttr();
        var clone = new CustomGeometry();
        clone.SetGeometryDataAttr(data);

        Assert.Equal(2u, clone.GetNumGeometries());
        Assert.Equal(3u, clone.GetNumVertices(0u));
        Assert.Equal(new Vector3(2.0f, 0.0f, 0.0f), clone.GetVertex(1u, 1u)?.Position);
    }
}
