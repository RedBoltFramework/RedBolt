using System.Runtime.CompilerServices;

using RedBolt;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

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
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        Assert.Equal(DrawableFlags.Geometry, custom.GetDrawableFlags());
        Assert.Equal(1u, custom.GetNumGeometries());
        Assert.False(custom.IsDynamic());
    }

    [Fact]
    public void Vertex_copy_is_independent()
    {
        var source = new CustomGeometryVertex
        {
            Position = new Vector3f(1.0f, 2.0f, 3.0f),
            Normal = Vector3f.Up,
            Color = 0x12345678,
            TexCoord = Vector2f.One,
            Tangent = new Vector4f(1.0f, 0.0f, 0.0f, 1.0f)
        };

        var copy = source;
        copy.Position = Vector3f.Zero;

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), source.Position);
        Assert.Equal(Vector3f.Zero, copy.Position);
        Assert.Equal(source.Normal, copy.Normal);
        Assert.Equal(source.Color, copy.Color);
        Assert.Equal(source.TexCoord, copy.TexCoord);
        Assert.Equal(source.Tangent, copy.Tangent);
    }

    [Fact]
    public void Renderer_is_not_copyable()
    {
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(CustomGeometry)));
    }

    [Fact]
    public void Dynamic_vertex_definition_and_commit_work()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);

        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(-1.0f, -1.0f, 0.0f));
        custom.DefineNormal(Vector3f.Forward);
        custom.DefineColor(new Color(1.0f, 0.0f, 0.0f));
        custom.DefineTexCoord(Vector2f.Zero);
        custom.DefineTangent(new Vector4f(1.0f, 0.0f, 0.0f, 1.0f));
        custom.DefineVertex(new Vector3f(1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));

        custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, true, true, true, true);

        ref var editable = ref custom.GetVertex(1u, 1u);
        Assert.False(Unsafe.IsNullRef(ref editable));
        editable.Position = new Vector3f(2.0f, 0.0f, 0.0f);
        editable.Normal = Vector3f.Up;
        editable.Color = new Color(1.0f, 1.0f, 0.0f).ToUInt();
        editable.TexCoord = Vector2f.One;
        editable.Tangent = new Vector4f(0.0f, 1.0f, 0.0f, 1.0f);

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
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(Vector3f.Zero);
        custom.DefineVertex(Vector3f.Zero);
        custom.DefineVertex(Vector3f.Zero);
        custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, false, false, false, false);
        custom.Commit();

        var vertexBuffer = custom.GetLodGeometry(0u, 0u)!.GetVertexBuffer(0u)!;
        Assert.Equal(5u, vertexBuffer.GetVertexCount());
        Assert.Equal(12u, vertexBuffer.GetVertexSize());
        Assert.Equal(60, vertexBuffer.GetShadowData().Length);
    }

    [Fact]
    public void Bounding_box_and_material_work()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(2.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(0.0f, 2.0f, 0.0f));
        var material = Resources.Get<Material>("Materials/FirstCube");
        custom.SetMaterial(material);
        custom.Commit();

        Assert.Same(material, custom.GetMaterial());
        Assert.Equal(Intersection.Inside,
            custom.GetBoundingBox().IsInside(new Vector3f(1.0f, 0.5f, 0.0f)));
    }

    [Fact]
    public void Occluder_triangle_count_respects_geometry_and_material()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(2.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(0.0f, 2.0f, 0.0f));
        custom.Commit();

        Assert.Equal(1u, custom.GetNumOccluderTriangles());

        var material = Resources.Get<Material>("Materials/FirstCube").Clone("CustomOccluder");
        material.SetOcclusion(false);
        custom.SetMaterial(material);

        Assert.Equal(0u, custom.GetNumOccluderTriangles());
    }

    [Fact]
    public void Material_attribute_resolves_resource_names()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        var material = Resources.Get<Material>("Materials/FirstCube");

        custom.SetMaterialsAttr(ResourceType.GetResourceRefList(new Material?[] { material }));

        Assert.Same(material, custom.GetMaterial());
        var first = custom.GetMaterialsAttr();
        var second = custom.GetMaterialsAttr();
        Assert.Same(first.Names, second.Names);
        Assert.Equal(material.Name, second.Names[0]);
    }

    [Fact]
    public void Vertex_pointer_returns_null_ref_when_out_of_range()
    {
        var custom = new CustomGeometry();

        ref var vertex = ref custom.GetVertex(1u, 0u);

        Assert.True(Unsafe.IsNullRef(ref vertex));
    }

    [Fact]
    public void Triangle_ray_query_uses_committed_geometry()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(0.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(1.0f, 0.0f, 0.0f));
        custom.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
        custom.Commit();
        var results = new List<RayQueryResult>();
        var query = new RayOctreeQuery(results,
            new Ray(new Vector3f(0.25f, 0.25f, 1.0f), new Vector3f(0.0f, 0.0f, -1.0f)),
            RayQueryLevel.RayTriangle);

        custom.ProcessRayQuery(query, results);

        var result = Assert.Single(results);
        Assert.Same(custom, result.Drawable);
        Assert.Equal(1.0f, result.Distance);
        Assert.Equal(new Vector3f(0.0f, 0.0f, 1.0f), result.Normal);
        Assert.Equal(MathDefs.MaxUnsigned, result.SubObject);
    }

    [Fact]
    public void Geometry_data_attribute_roundtrips()
    {
        var custom = new SceneType().CreateChild("Custom").CreateComponent<CustomGeometry>();
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);
        custom.BeginGeometry(0u, PrimitiveType.TriangleList);
        custom.DefineVertex(new Vector3f(-1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3f(1.0f, -1.0f, 0.0f));
        custom.DefineVertex(new Vector3f(0.0f, 1.0f, 0.0f));
        custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, false, false, false, false);
        custom.GetVertex(1u, 1u).Position = new Vector3f(2.0f, 0.0f, 0.0f);
        custom.Commit();

        var data = custom.GetGeometryDataAttr();
        var clone = new CustomGeometry();
        clone.SetGeometryDataAttr(data);

        Assert.Equal(2u, clone.GetNumGeometries());
        Assert.Equal(3u, clone.GetNumVertices(0u));
        Assert.Equal(new Vector3f(2.0f, 0.0f, 0.0f), clone.GetVertex(1u, 1u).Position);
    }
}
