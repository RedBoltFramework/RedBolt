using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ModelFileTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ModelFileTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Repo_backed_rbfx_models_load_through_resource_cache()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var plane = Resources.GetResource<Model>("Models/Plane.mdl");
            var mushroom = Resources.GetResource<Model>("Models/Mushroom.mdl");

            Assert.NotNull(plane.GetGeometry(0));
            Assert.Equal(1u, plane.GetNumGeometries());
            Assert.Equal(1u, plane.GetNumGeometryLodLevels(0));
            Assert.NotNull(plane.GetGeometry(0)!.GetVertexBuffer(0));
            Assert.True((((VertexMask)plane.GetGeometry(0)!.GetVertexBuffer(0)!.GetElementMask()) & VertexMask.Position) != 0);
            Assert.Equal(Intersection.Inside, plane.GetBoundingBox().IsInside(Vector3f.Zero));

            Assert.NotNull(mushroom.GetGeometry(0));
            Assert.True(mushroom.GetNumGeometryLodLevels(0) >= 2u);
            Assert.NotNull(mushroom.GetGeometry(0)!.GetVertexBuffer(0));
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    [Fact]
    public void Model_preserves_a_defined_skeleton()
    {
        var model = new Model("SkeletonModel");
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        var sourceBone = skeleton.GetModifiableBones()[0];
        sourceBone.Name = "Root";
        sourceBone.NameHash = new StringHash("Root");
        sourceBone.ParentIndex = 0;

        model.SetSkeleton(skeleton);

        var modelBone = model.GetSkeleton().GetBone(0u);
        Assert.NotNull(modelBone);
        Assert.NotSame(sourceBone, modelBone);
        Assert.Equal("Root", modelBone!.Name);
        Assert.Equal(1u, model.GetSkeleton().GetNumBones());
    }

    [Fact]
    public void Model_preserves_geometry_bone_mappings()
    {
        var model = new Model("BoneMappingsModel");
        IReadOnlyList<IReadOnlyList<uint>> mappings =
        [
            new uint[] { 3u, 7u },
            new uint[] { 1u }
        ];

        model.SetGeometryBoneMappings(mappings);

        var result = model.GetGeometryBoneMappings();
        Assert.Equal(2, result.Count);
        Assert.Equal(new uint[] { 3u, 7u }, result[0]);
        Assert.Equal(new uint[] { 1u }, result[1]);
    }

    [Fact]
    public void Model_preserves_morph_metadata_and_packed_data()
    {
        var model = new Model("MorphModel");
        var morphBuffer = new VertexBufferMorph
        {
            ElementMask = VertexMask.Position | VertexMask.Normal,
            VertexCount = 2,
            DataSize = 56,
            MorphData = new byte[56]
        };
        var morph = new ModelMorph
        {
            Name = "Smile",
            NameHash = new StringHash("Smile"),
            Weight = 0.25f,
            Buffers = new Dictionary<uint, VertexBufferMorph> { [1u] = morphBuffer }
        };

        model.SetMorphs([morph]);

        Assert.Equal(1u, model.GetNumMorphs());
        Assert.Same(morph, model.GetMorph(0u));
        Assert.Same(morph, model.GetMorph("Smile"));
        Assert.Same(morphBuffer, model.GetMorph(new StringHash("Smile"))!.Buffers[1u]);
        Assert.Null(model.GetMorph(1u));
        Assert.Equal(0u, model.GetMorphRangeStart(0u));
        Assert.Equal(0u, model.GetMorphRangeCount(0u));
    }

    [Fact]
    public void Model_accepts_shadowed_vertex_and_index_buffers()
    {
        var model = new Model("BufferModel");
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize(3u, VertexMask.Position);
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize(3u);

        Assert.True(model.SetVertexBuffers([vertexBuffer], [4u], [8u]));
        Assert.True(model.SetIndexBuffers([indexBuffer]));
        Assert.Same(vertexBuffer, model.GetVertexBuffers()[0]);
        Assert.Same(indexBuffer, model.GetIndexBuffers()[0]);
        Assert.Equal(4u, model.GetMorphRangeStart(0u));
        Assert.Equal(8u, model.GetMorphRangeCount(0u));
    }
}
