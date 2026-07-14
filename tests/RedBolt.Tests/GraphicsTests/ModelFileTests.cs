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
}
