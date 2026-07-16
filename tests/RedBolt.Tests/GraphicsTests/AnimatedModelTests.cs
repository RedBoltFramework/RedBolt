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

    [Fact]
    public void Animated_model_ray_query_hits_bone_box()
    {
        var animatedModel = CreateAnimatedModel(BoneCollisionShape.Box);
        using var query = new RayOctreeQuery(new Ray(new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Forward),
            RayQueryLevel.RayTriangle);

        animatedModel.ProcessRayQuery(query, query.Result);

        var result = Assert.Single(query.Result);
        Assert.Equal(animatedModel, result.Drawable);
        Assert.Equal(animatedModel.Node, result.Node);
        Assert.Equal(0u, result.SubObject);
        Assert.InRange(result.Distance, 3.9999f, 4.0001f);
    }

    [Fact]
    public void Animated_model_custom_ray_query_uses_supplied_bone_transform()
    {
        var animatedModel = CreateAnimatedModel(BoneCollisionShape.Sphere);
        using var query = new RayOctreeQuery(new Ray(new Vector3f(2.0f, 0.0f, -5.0f), Vector3f.Forward),
            RayQueryLevel.RayTriangle);
        var transforms = new[] { Matrix3x4.FromTranslation(new Vector3f(2.0f, 0.0f, 0.0f)) };

        animatedModel.ProcessCustomRayQuery(query,
            new BoundingBox(new Vector3f(-4.0f, -4.0f, -4.0f), new Vector3f(4.0f, 4.0f, 4.0f)),
            Matrix3x4.Identity, transforms, query.Result);

        var result = Assert.Single(query.Result);
        Assert.InRange(result.Distance, 3.9999f, 4.0001f);
        Assert.Equal(0u, result.SubObject);
    }

    [Fact]
    public void Animated_model_ray_query_rejects_world_bounds_and_missing_collision_shape()
    {
        var animatedModel = CreateAnimatedModel(BoneCollisionShape.None);
        using var query = new RayOctreeQuery(new Ray(new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Forward),
            RayQueryLevel.RayTriangle, 3.0f);

        animatedModel.ProcessCustomRayQuery(query,
            new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)),
            Matrix3x4.Identity, [], query.Result);

        Assert.Empty(query.Result);
    }

    private static AnimatedModel CreateAnimatedModel(BoneCollisionShape collisionShape)
    {
        var model = new Model("AnimatedModelRayQuery");
        var skeleton = new Skeleton();
        skeleton.SetNumBones(1);
        var bone = skeleton.GetModifiableBones()[0];
        bone.Name = "Root";
        bone.NameHash = new StringHash("Root");
        bone.ParentIndex = 0;
        bone.CollisionMask = collisionShape;
        bone.BoundingBox = new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f));
        bone.Radius = 1.0f;
        model.SetSkeleton(skeleton);

        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);
        return animatedModel;
    }
}
