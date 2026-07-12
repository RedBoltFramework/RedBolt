using Xunit;

namespace RedBolt.Tests;

public sealed class SkeletonTests
{
    [Fact]
    public void Skeleton_tracks_root_parent_and_hierarchy_order()
    {
        var skeleton = new Skeleton();
        skeleton.SetNumBones(3);
        var bones = skeleton.GetModifiableBones();
        bones[0].Name = "Root";
        bones[0].NameHash = new StringHash("Root");
        bones[0].ParentIndex = 0;
        bones[1].Name = "Child";
        bones[1].NameHash = new StringHash("Child");
        bones[1].ParentIndex = 0;
        bones[2].Name = "Grandchild";
        bones[2].NameHash = new StringHash("Grandchild");
        bones[2].ParentIndex = 1;

        skeleton.SetRootBoneIndex(0);
        skeleton.UpdateBoneOrder();

        Assert.Equal(3u, skeleton.GetNumBones());
        Assert.Same(bones[0], skeleton.GetRootBone());
        Assert.Same(bones[1], skeleton.GetBoneParent(bones[2]));
        Assert.Equal(new uint[] { 0, 1, 2 }, skeleton.GetBonesOrder());
        Assert.Equal(1u, skeleton.GetBoneIndex("Child"));
        Assert.Same(bones[2], skeleton.GetBone(2u));
    }

    [Fact]
    public void Skeleton_define_clones_bones_without_node_references()
    {
        var source = new Skeleton();
        source.SetNumBones(1);
        var sourceBone = source.GetModifiableBones()[0];
        sourceBone.Name = "Root";
        sourceBone.NameHash = new StringHash("Root");
        sourceBone.ParentIndex = 0;

        var destination = new Skeleton();
        destination.Define(source);

        var destinationBone = destination.GetBone(0u);
        Assert.NotNull(destinationBone);
        Assert.NotSame(sourceBone, destinationBone);
        Assert.Equal("Root", destinationBone!.Name);
        Assert.Null(destinationBone.Node);
    }
}
