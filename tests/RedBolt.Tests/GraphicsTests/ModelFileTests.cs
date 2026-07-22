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
        var model = new Model { Name = "SkeletonModel" };
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        var sourceBone = skeleton.GetModifiableBones()[0];
        sourceBone.Name = "Root";
        sourceBone.NameHash = new StringHash("Root");
        sourceBone.ParentIndex = 0;
        sourceBone.Node = new Scene().CreateChild("Root");

        model.SetSkeleton(skeleton);

        var modelBone = model.GetSkeleton().GetBone(0u);
        Assert.NotNull(modelBone);
        Assert.NotSame(sourceBone, modelBone);
        Assert.Equal("Root", modelBone!.Name);
        Assert.Same(sourceBone.Node, modelBone.Node);
        Assert.Equal(1u, model.GetSkeleton().GetNumBones());

        var clone = model.Clone("SkeletonClone");
        var cloneBone = clone.GetSkeleton().GetBone(0u);
        Assert.NotSame(modelBone, cloneBone);
        Assert.Same(modelBone.Node, cloneBone!.Node);
    }

    [Fact]
    public void Model_preserves_geometry_bone_mappings()
    {
        var model = new Model { Name = "BoneMappingsModel" };
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
        var model = new Model { Name = "MorphModel" };
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
        Assert.NotSame(morph, model.GetMorph(0u));
        Assert.Same(model.GetMorph(0u), model.GetMorph("Smile"));
        Assert.NotSame(morphBuffer, model.GetMorph(new StringHash("Smile"))!.Buffers[1u]);
        Assert.Same(morphBuffer.MorphData, model.GetMorph(0u)!.Buffers[1u].MorphData);
        Assert.Null(model.GetMorph(1u));
        Assert.Equal(0u, model.GetMorphRangeStart(0u));
        Assert.Equal(0u, model.GetMorphRangeCount(0u));
    }

    [Fact]
    public void Model_accepts_shadowed_vertex_and_index_buffers()
    {
        var model = new Model { Name = "BufferModel" };
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

    [Fact]
    public void Model_saves_and_loads_umd3_data()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize(3u, VertexMask.Position);
        vertexBuffer.Update(new byte[36]);
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        indexBuffer.SetSize(3u);
        indexBuffer.SetUnpackedData(new uint[] { 0u, 1u, 2u });
        var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 3);

        var source = new Model { Name = "Source" };
        source.SetVertexBuffers([vertexBuffer]);
        source.SetIndexBuffers([indexBuffer]);
        source.SetNumGeometries(1);
        source.SetGeometry(0, 0, geometry);

        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream));
        stream.Seek(0);

        var loaded = new Model { Name = "Loaded" };
        Assert.True(loaded.Load(stream));
        Assert.Equal(1u, loaded.GetNumGeometries());
        Assert.NotNull(loaded.GetGeometry(0));
        Assert.Equal(3u, loaded.GetGeometry(0)!.GetIndexCount());
        Assert.Equal(3u, loaded.GetVertexBuffers()[0].GetVertexCount());
        Assert.Equal(3u, loaded.GetIndexBuffers()[0].GetIndexCount());

        stream.Seek(0);
        var staged = new Model { Name = "Staged" };
        staged.SetAsyncLoadState(AsyncLoadState.Loading);
        Assert.True(staged.BeginLoad(stream));
        Assert.Equal(0u, staged.GetVertexBuffers()[0].GetVertexCount());
        Assert.Equal(0u, staged.GetIndexBuffers()[0].GetIndexCount());
        Assert.True(staged.EndLoad());
        Assert.Equal(3u, staged.GetVertexBuffers()[0].GetVertexCount());
        Assert.Equal(3u, staged.GetIndexBuffers()[0].GetIndexCount());
    }
}
