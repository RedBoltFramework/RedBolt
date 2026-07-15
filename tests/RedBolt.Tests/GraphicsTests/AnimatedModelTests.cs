namespace RedBolt.Tests;

public sealed class AnimatedModelTests
{
    [Fact]
    public void Animated_model_copies_skeleton_and_morphs_from_model()
    {
        var model = new Model("AnimatedModel");
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        skeleton.GetModifiableBones()[0].Name = "Root";
        skeleton.GetModifiableBones()[0].NameHash = new StringHash("Root");
        skeleton.GetModifiableBones()[0].ParentIndex = 0;
        model.SetSkeleton(skeleton);
        var morph = new ModelMorph
        {
            Name = "Smile",
            NameHash = new StringHash("Smile"),
            Weight = 0.5f
        };
        model.SetMorphs([morph]);

        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);

        Assert.Equal(1u, animatedModel.GetSkeleton().GetNumBones());
        Assert.Equal("Root", animatedModel.GetSkeleton().GetBone(0u)!.Name);
        Assert.Equal(0.5f, animatedModel.GetMorphWeight("Smile"));
        Assert.Equal(1u, animatedModel.GetNumMorphs());
        Assert.True(animatedModel.IsMaster());
        Assert.NotNull(animatedModel.GetSkeleton().GetBone(0u)!.Node);
    }

    [Fact]
    public void Animated_model_attribute_helpers_match_native_values()
    {
        var model = new Model("AnimatedModel");
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        skeleton.GetModifiableBones()[0].Name = "Root";
        skeleton.GetModifiableBones()[0].NameHash = new StringHash("Root");
        skeleton.GetModifiableBones()[0].ParentIndex = 0;
        model.SetSkeleton(skeleton);
        model.SetMorphs([new ModelMorph { Name = "Smile", NameHash = new StringHash("Smile") }]);

        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, false);
        animatedModel.SetMorphsAttr(new byte[] { 255 });

        Assert.Equal(1.0f, animatedModel.GetMorphWeight(0u));
        Assert.Single(animatedModel.GetBonesEnabledAttr());
        Assert.True(animatedModel.GetBonesEnabledAttr()[0].GetBool());

        animatedModel.SetBonesEnabledAttr(new VariantVector([new Variant(false)]));
        Assert.False(animatedModel.GetBonesEnabledAttr()[0].GetBool());
    }

    [Fact]
    public void Animated_model_morph_weights_match_native_lookup_defaults()
    {
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();

        animatedModel.SetMorphWeight(5u, 1.0f);
        animatedModel.SetMorphWeight("Missing", 1.0f);
        animatedModel.SetMorphWeight(new StringHash("Missing"), 1.0f);

        Assert.Equal(0.0f, animatedModel.GetMorphWeight(5u));
        Assert.Equal(0.0f, animatedModel.GetMorphWeight("Missing"));
        Assert.Equal(0.0f, animatedModel.GetMorphWeight(new StringHash("Missing")));
    }
}
