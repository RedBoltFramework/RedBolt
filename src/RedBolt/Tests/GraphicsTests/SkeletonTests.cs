namespace RedBolt.Tests;

using Xunit;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Scene;

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

        Assert.Equal(3, skeleton.GetNumBones());
        Assert.Same(bones[0], skeleton.GetRootBone());
        Assert.Same(bones[1], skeleton.GetBoneParent(bones[2]));
        Assert.Equal(new[] { 0, 1, 2 }, skeleton.GetBonesOrder());
        Assert.Equal(1, skeleton.GetBoneIndex("Child"));
        Assert.Same(bones[2], skeleton.GetBone(2));
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
        sourceBone.Node = new Scene().CreateChild("Root");

        var destination = new Skeleton();
        destination.Define(source);

        var destinationBone = destination.GetBone(0);
        Assert.NotNull(destinationBone);
        Assert.NotSame(sourceBone, destinationBone);
        Assert.Equal("Root", destinationBone!.Name);
        Assert.Null(destinationBone.Node);
    }

    [Fact]
    public void Bone_and_skeleton_copy_semantics_match_cpp_types()
    {
        Assert.False(typeof(Bone).IsValueType);
        Assert.False(typeof(Skeleton).IsValueType);

        var source = new Skeleton();
        source.SetNumBones(1);
        var sourceBone = source.GetModifiableBones()[0];
        sourceBone.Name = "Root";
        sourceBone.NameHash = new StringHash("Root");
        sourceBone.ParentIndex = 0;
        sourceBone.Node = new Scene().CreateChild("CopyRoot");

        var boneCopy = (Bone)sourceBone.Clone();
        boneCopy.Name = "Copy";
        Assert.Equal("Root", sourceBone.Name);

        var skeletonCopy = source.Clone();
        skeletonCopy.GetModifiableBones()[0].Name = "SkeletonCopy";
        Assert.Equal("Root", source.GetBone(0)!.Name);
        Assert.Equal("SkeletonCopy", skeletonCopy.GetBone(0)!.Name);
        Assert.Same(sourceBone.Node, skeletonCopy.GetBone(0)!.Node);
    }
}
