namespace RedBolt.Tests;

public sealed class AnimatedModelTests
{
    [Fact]
    public void Animated_model_preserves_cpp_class_kind()
    {
        Assert.True(typeof(AnimatedModel).IsClass);
        Assert.False(typeof(AnimatedModel).IsSealed);
        Assert.DoesNotContain(typeof(ICloneable), typeof(AnimatedModel).GetInterfaces());
    }

    [Fact]
    public void Animated_model_registers_geometry_factory()
    {
        using var context = new Context();

        AnimatedModel.RegisterObject(context);

        using var animatedModel = Assert.IsType<AnimatedModel>(
            context.CreateObject(ObjectReflection<AnimatedModel>.TypeId));
        Assert.Equal(ObjectCategory.Category_Geometry, context.GetReflection<AnimatedModel>().Category);
    }

    [Fact]
    public void Animated_model_copies_skeleton_and_morphs_from_model()
    {
        var model = new Model { Name = "AnimatedModel" };
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

        Assert.Equal(1, animatedModel.GetSkeleton().GetNumBones());
        Assert.Equal("Root", animatedModel.GetSkeleton().GetBone(0)!.Name);
        Assert.Equal(0.5f, animatedModel.GetMorphWeight("Smile"));
        Assert.Equal(1, animatedModel.GetNumMorphs());
        Assert.True(animatedModel.IsMaster());
        Assert.NotNull(animatedModel.GetSkeleton().GetBone(0)!.Node);
    }

    [Fact]
    public void Animated_model_update_uses_integer_bone_indices()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var animatedModel = scene.CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(CreateSkeletonModel("AnimatedModel", BoneCollisionShape.None), true);

        animatedModel.Update(new FrameInfo { FrameNumber = 1 });

        Assert.NotNull(animatedModel.GetSkeleton().GetBone(0)!.Node);
    }

    [Fact]
    public void Nonzero_morph_weight_clones_model_vertex_buffers_on_demand()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(1, VertexMask.Position));
        vertexBuffer.Update(new byte[vertexBuffer.GetVertexSize()]);
        var model = new Model { Name = "Morph" };
        Assert.True(model.SetVertexBuffers([vertexBuffer], [0u], [1u]));
        var morph = new ModelMorph { Name = "Smile", NameHash = new StringHash("Smile") };
        morph.Buffers[0] = new VertexBufferMorph
        {
            ElementMask = VertexMask.Position,
            VertexCount = 1,
            DataSize = sizeof(uint) + 3u * sizeof(float),
            MorphData = new byte[sizeof(uint) + 3 * sizeof(float)]
        };
        model.SetMorphs([morph]);
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);

        Assert.Empty(animatedModel.GetMorphVertexBuffers());
        animatedModel.SetMorphWeight(0, 1.0f);

        Assert.NotSame(vertexBuffer, Assert.IsType<VertexBuffer>(Assert.Single(animatedModel.GetMorphVertexBuffers())));
        Assert.Equal(UpdateGeometryType.MainThread, animatedModel.GetUpdateGeometryType());
    }

    [Fact]
    public void Animated_model_attribute_helpers_match_native_values()
    {
        var model = new Model { Name = "AnimatedModel" };
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

        Assert.Equal(1.0f, animatedModel.GetMorphWeight(0));
        Assert.Single(animatedModel.GetBonesEnabledAttr());
        Assert.True(animatedModel.GetBonesEnabledAttr()[0].GetBool());

        animatedModel.SetBonesEnabledAttr(new VariantVector([new Variant(false)]));
        Assert.False(animatedModel.GetBonesEnabledAttr()[0].GetBool());
    }

    [Fact]
    public void Animated_model_morph_weights_match_native_lookup_defaults()
    {
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();

        animatedModel.SetMorphWeight(5, 1.0f);
        animatedModel.SetMorphWeight("Missing", 1.0f);
        animatedModel.SetMorphWeight(new StringHash("Missing"), 1.0f);

        Assert.Equal(0.0f, animatedModel.GetMorphWeight(5));
        Assert.Equal(0.0f, animatedModel.GetMorphWeight("Missing"));
        Assert.Equal(0.0f, animatedModel.GetMorphWeight(new StringHash("Missing")));
    }

    [Fact]
    public void Animation_state_changes_mark_connected_master_model_dirty_and_clamp_values()
    {
        var animatedModel = CreateAnimatedModel(BoneCollisionShape.Box);
        var animation = new Animation();
        animation.SetLength(2.0f);
        var state = new AnimationState();
        state.ConnectToAnimatedModel(animatedModel);
        state.Initialize(animation, string.Empty, AnimationBlendMode.Lerp);

        SetPrivateField(animatedModel, "_animationDirty", false);
        state.OnTracksReady();
        Assert.True(GetPrivateField<bool>(animatedModel, "_animationDirty"));

        SetPrivateField(animatedModel, "_animationDirty", false);
        state.SetWeight(2.0f);
        Assert.Equal(1.0f, state.GetWeight());
        Assert.True(GetPrivateField<bool>(animatedModel, "_animationDirty"));

        SetPrivateField(animatedModel, "_animationDirty", false);
        state.SetTime(3.0f);
        Assert.Equal(2.0f, state.GetTime());
        Assert.True(GetPrivateField<bool>(animatedModel, "_animationDirty"));
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

    [Fact]
    public void Apply_attributes_assigns_existing_bone_nodes()
    {
        var model = CreateSkeletonModel("DeferredBones", BoneCollisionShape.Box);
        var node = new Scene().CreateChild("AnimatedModel");
        var rootNode = node.CreateChild("Root");
        var animatedModel = node.CreateComponent<AnimatedModel>();

        animatedModel.SetModel(model, false);
        Assert.Null(animatedModel.GetSkeleton().GetBone(0)!.Node);

        animatedModel.ApplyAttributes();

        Assert.Same(rootNode, animatedModel.GetSkeleton().GetBone(0)!.Node);
        Assert.Contains(animatedModel, rootNode.GetListeners());
    }

    [Fact]
    public void Compatible_skeleton_reload_preserves_bone_node_and_animated_state()
    {
        var node = new Scene().CreateChild("AnimatedModel");
        var animatedModel = node.CreateComponent<AnimatedModel>();
        animatedModel.SetModel(CreateSkeletonModel("First", BoneCollisionShape.Box), true);
        var originalNode = animatedModel.GetSkeleton().GetBone(0)!.Node;
        animatedModel.GetSkeleton().GetModifiableBones()[0].Animated = false;

        animatedModel.SetModel(CreateSkeletonModel("Second", BoneCollisionShape.Sphere), true);

        var bone = animatedModel.GetSkeleton().GetBone(0)!;
        Assert.Same(originalNode, bone.Node);
        Assert.False(bone.Animated);
        Assert.Equal(BoneCollisionShape.Sphere, bone.CollisionMask);
    }

    [Fact]
    public void Model_reload_event_reapplies_only_the_current_model()
    {
        var firstModel = CreateSkeletonModel("First", BoneCollisionShape.Box);
        var secondModel = CreateSkeletonModel("Second", BoneCollisionShape.Sphere);
        secondModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-2.0f, -2.0f, -2.0f), new Vector3f(2.0f, 2.0f, 2.0f)));
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(firstModel, true);
        animatedModel.SetModel(secondModel, true);

        firstModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-3.0f, -3.0f, -3.0f), new Vector3f(3.0f, 3.0f, 3.0f)));
        firstModel.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(secondModel.GetBoundingBox(), animatedModel.GetBoundingBox());

        secondModel.SetBoundingBox(new BoundingBox(
            new Vector3f(-4.0f, -4.0f, -4.0f), new Vector3f(4.0f, 4.0f, 4.0f)));
        secondModel.SendEvent<ResourceEvents.ReloadFinished>();
        Assert.Equal(secondModel.GetBoundingBox(), animatedModel.GetBoundingBox());
    }

    [Fact]
    public void Update_bone_bounding_box_uses_local_bone_transform()
    {
        var model = CreateSkeletonModel("Bounds", BoneCollisionShape.Box);
        model.GetSkeleton().GetModifiableBones()[0].InitialPosition = new Vector3f(3.0f, 0.0f, 0.0f);
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);

        animatedModel.UpdateBoneBoundingBox();

        var bounds = animatedModel.GetWorldBoundingBox();
        Assert.Equal(Intersection.Inside, bounds.IsInside(new Vector3f(3.0f, 0.0f, 0.0f)));
        Assert.Equal(Intersection.Outside, bounds.IsInside(Vector3f.Zero));
    }

    [Fact]
    public void Non_master_model_uses_master_bone_nodes_and_world_bounds()
    {
        var node = new Scene().CreateChild("AnimatedModels");
        var master = node.CreateComponent<AnimatedModel>();
        master.SetModel(CreateSkeletonModel("Master", BoneCollisionShape.Box), true);
        master.UpdateBoneBoundingBox();
        var attachment = node.CreateComponent<AnimatedModel>();
        attachment.SetModel(CreateSkeletonModel("Attachment", BoneCollisionShape.Box), false);

        attachment.ApplyAttributes();

        Assert.True(master.IsMaster());
        Assert.False(attachment.IsMaster());
        Assert.Same(master.GetSkeleton().GetBone(0)!.Node, attachment.GetSkeleton().GetBone(0)!.Node);
        Assert.Equal(master.GetWorldBoundingBox(), attachment.GetWorldBoundingBox());
    }

    [Fact]
    public void Animated_model_destruction_removes_bones_only_after_last_model()
    {
        var node = new Scene().CreateChild("AnimatedModels");
        var model = CreateSkeletonModel("Shared", BoneCollisionShape.Box);
        model.GetSkeleton().SetRootBoneIndex(0);
        var master = node.CreateComponent<AnimatedModel>();
        master.SetModel(model, true);
        var rootBoneNode = master.GetSkeleton().GetRootBone()!.Node!;
        var attachment = node.CreateComponent<AnimatedModel>();
        attachment.SetModel(model, true);

        node.RemoveComponent(attachment);
        attachment.Dispose();

        Assert.Same(rootBoneNode, node.GetChild("Root"));

        node.RemoveComponent(master);
        master.Dispose();

        Assert.Null(node.GetChild("Root"));
    }

    [Fact]
    public void Animated_model_keeps_animation_state_source_weak()
    {
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        var source = new AnimationController();

        animatedModel.ConnectToAnimationStateSource(source);

        var weakSource = GetPrivateField<WeakReference<AnimationStateSource>>(animatedModel,
            "_animationStateSource");
        Assert.True(weakSource.TryGetTarget(out var target));
        Assert.Same(source, target);
    }

    [Fact]
    public void Update_batches_matches_model_bounds_and_minimum_animation_lod_distance()
    {
        var model = CreateSkeletonModel("Lod", BoneCollisionShape.Box);
        model.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));
        model.SetNumGeometries(2);
        model.SetGeometryCenter(0, new Vector3f(-2.0f, 0.0f, 0.0f));
        model.SetGeometryCenter(1, new Vector3f(2.0f, 0.0f, 0.0f));
        var scene = new Scene();
        var modelNode = scene.CreateChild("AnimatedModel");
        modelNode.SetScale(new Vector3f(2.0f, 3.0f, 4.0f));
        var animatedModel = modelNode.CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        var frame = new FrameInfo { Camera = camera, FrameNumber = 7 };

        animatedModel.UpdateBatches(frame);

        var expectedLodDistance = camera.GetLodDistance(animatedModel.GetDistance(), 6.0f, animatedModel.LodBias);
        Assert.Equal(expectedLodDistance, animatedModel.GetLodDistance());
        Assert.Equal(camera.GetDistance(modelNode.GetWorldTransform() * new Vector3f(-2.0f, 0.0f, 0.0f)),
            animatedModel.GetBatches()[0].Distance);
        Assert.Equal(camera.GetDistance(modelNode.GetWorldTransform() * new Vector3f(2.0f, 0.0f, 0.0f)),
            animatedModel.GetBatches()[1].Distance);

        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -5.0f));
        animatedModel.UpdateBatches(frame);

        Assert.Equal(animatedModel.GetLodDistance(), GetPrivateField<float>(animatedModel, "_animationLodDistance"));
        Assert.Equal(7u, GetPrivateField<uint>(animatedModel, "_animationLodFrameNumber"));
    }

    [Theory]
    [InlineData(false, false, false, false, UpdateGeometryType.None)]
    [InlineData(false, false, true, false, UpdateGeometryType.WorkerThread)]
    [InlineData(false, false, true, true, UpdateGeometryType.MainThread)]
    [InlineData(true, false, false, false, UpdateGeometryType.MainThread)]
    [InlineData(false, true, false, false, UpdateGeometryType.MainThread)]
    [InlineData(true, true, true, false, UpdateGeometryType.MainThread)]
    public void Update_geometry_type_matches_cpp_dirty_flag_precedence(bool morphsDirty,
        bool forceAnimationUpdate, bool skinningDirty, bool softwareSkinning, UpdateGeometryType expected)
    {
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        SetPrivateField(animatedModel, "_morphsDirty", morphsDirty);
        SetPrivateField(animatedModel, "_forceAnimationUpdate", forceAnimationUpdate);
        SetPrivateField(animatedModel, "_skinningDirty", skinningDirty);
        SetPrivateField(animatedModel, "_softwareSkinning", softwareSkinning);

        Assert.Equal(expected, animatedModel.GetUpdateGeometryType());
    }

    private static AnimatedModel CreateAnimatedModel(BoneCollisionShape collisionShape)
    {
        var model = CreateSkeletonModel("AnimatedModelRayQuery", collisionShape);
        var animatedModel = new Scene().CreateChild("AnimatedModel").CreateComponent<AnimatedModel>();
        animatedModel.SetModel(model, true);
        return animatedModel;
    }

    private static Model CreateSkeletonModel(string name, BoneCollisionShape collisionShape)
    {
        var model = new Model { Name = name };
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
        return model;
    }

    private static T GetPrivateField<T>(AnimatedModel animatedModel, string name)
    {
        var field = typeof(AnimatedModel).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<T>(field!.GetValue(animatedModel));
    }

    private static void SetPrivateField<T>(AnimatedModel animatedModel, string name, T value)
    {
        var field = typeof(AnimatedModel).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(animatedModel, value);
    }
}
