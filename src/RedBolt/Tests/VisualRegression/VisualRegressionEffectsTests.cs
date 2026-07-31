namespace RedBolt.Tests;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionEffectsTests
{
    [VisualFact]
    public void Billboard_face_modes_and_per_object_data_render_stably()
    {
        using var fixture = new VisualRegressionFixture();
        var rotateXyz = CaptureBillboardView(fixture, FaceCameraMode.RotateXyz);
        var rotateY = CaptureBillboardView(fixture, FaceCameraMode.RotateY);
        var lookAtY = CaptureBillboardView(fixture, FaceCameraMode.LookAtY);
        var direction = CaptureBillboardView(fixture, FaceCameraMode.Direction);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(rotateXyz, "effects/billboard-face-rotate-xyz.png"),
            new VisualRegressionImageCapture(rotateY, "effects/billboard-face-rotate-y.png"),
            new VisualRegressionImageCapture(lookAtY, "effects/billboard-face-look-at-y.png"),
            new VisualRegressionImageCapture(direction, "effects/billboard-face-direction.png"));
    }

    [VisualFact]
    public void Billboard_sorting_and_crossing_alpha_depths_are_visible()
    {
        using var fixture = new VisualRegressionFixture();
        var unsorted = CaptureBillboardOrdering(fixture, sorted: false);
        var sorted = CaptureBillboardOrdering(fixture, sorted: true);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(unsorted, "effects/billboard-unsorted-alpha.png"),
            new VisualRegressionImageCapture(sorted, "effects/billboard-sorted-alpha.png"));
    }

    [VisualFact]
    public void Particle_emitter_shapes_render_with_a_fixed_random_seed()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var sphere = CaptureParticleShape(fixture, EmitterType.Sphere);
        var box = CaptureParticleShape(fixture, EmitterType.Box);
        var volume = CaptureParticleShape(fixture, EmitterType.SphereVolume);
        var cylinder = CaptureParticleShape(fixture, EmitterType.Cylinder);
        var ring = CaptureParticleShape(fixture, EmitterType.Ring);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(sphere, "effects/particle-shape-sphere.png"),
            new VisualRegressionImageCapture(box, "effects/particle-shape-box.png"),
            new VisualRegressionImageCapture(volume, "effects/particle-shape-volume.png"),
            new VisualRegressionImageCapture(cylinder, "effects/particle-shape-cylinder.png"),
            new VisualRegressionImageCapture(ring, "effects/particle-shape-ring.png"));
    }

    [VisualFact]
    public void Particle_fixed_time_color_texture_and_size_frames_render_independently()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var initial = CaptureParticleFrame(fixture, 0.0f);
        var middle = CaptureParticleFrame(fixture, 0.5f);
        var final = CaptureParticleFrame(fixture, 1.0f);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(initial, "effects/particle-frame-start.png"),
            new VisualRegressionImageCapture(middle, "effects/particle-frame-middle.png"),
            new VisualRegressionImageCapture(final, "effects/particle-frame-end.png"));
    }

    [VisualFact]
    public void Particle_relative_scaled_sorted_and_fixed_screen_modes_change_the_capture()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var world = CaptureParticleMode(fixture, relative: false, scaled: false, sorted: false,
            fixedScreenSize: false);
        var relative = CaptureParticleMode(fixture, relative: true, scaled: true, sorted: true,
            fixedScreenSize: false);
        var fixedScreen = CaptureParticleMode(fixture, relative: false, scaled: false, sorted: true,
            fixedScreenSize: true);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(world, "effects/particle-mode-world.png"),
            new VisualRegressionImageCapture(relative, "effects/particle-mode-relative.png"),
            new VisualRegressionImageCapture(fixedScreen, "effects/particle-mode-fixed-screen.png"));
    }

    [VisualFact]
    public void Particle_forces_damping_size_and_lifetime_update_at_fixed_steps()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var before = CaptureParticleForce(fixture, 0.0f);
        var after = CaptureParticleForce(fixture, 0.25f);
        var expired = CaptureParticleForce(fixture, 2.0f);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(before, "effects/particle-force-before.png"),
            new VisualRegressionImageCapture(after, "effects/particle-force-after.png"),
            new VisualRegressionImageCapture(expired, "effects/particle-force-expired.png"));
    }

    [VisualFact]
    public void Soft_particle_material_fades_against_a_solid_occluder()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml",
            new Vector3f(0.0f, 0.0f, 0.55f), 1.7f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.2f);

        var node = scene.CreateChild("SoftParticle");
        var emitter = new VisualParticleEmitter();
        node.AddComponent(emitter);
        using var effect = CreateParticleEffect(fixture, EmitterType.Sphere, relative: false,
            scaled: true, sorted: true, fixedScreenSize: false);
        effect.SetMaterial(fixture.Resources.Get<Material>("Materials/Showcase/LitSmokeSoft.xml"));
        emitter.SetEffect(effect);
        emitter.SetEmitting(false);
        SetFixedParticle(emitter, 0.0f, new Vector3f(0.0f, 0.0f, 0.15f),
            new Vector2f(1.7f, 1.7f), new Color(0.85f, 0.9f, 1.0f, 0.8f));
        StepParticle(scene, emitter, 0.0f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 3.4f);
        var soft = fixture.Capture(scene, camera);

        effect.SetMaterial(fixture.Resources.Get<Material>("Materials/Smoke.xml"));
        var regular = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(soft, "effects/soft-particle-faded.png"),
            new VisualRegressionImageCapture(regular, "effects/soft-particle-regular.png"));
    }

    [VisualFact]
    public void Serialized_particle_billboards_render_fixed_fire_and_smoke_frames()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.03f, 0.03f, 0.03f));
        AddStaticParticles(fixture, scene, "Particle/Fire.xml", new Vector3f(-1.3f, 0.0f, 0.0f),
            new Color(1.0f, 0.45f, 0.05f));
        AddStaticParticles(fixture, scene, "Particle/Smoke.xml", new Vector3f(1.3f, 0.0f, 0.0f),
            new Color(0.65f, 0.7f, 0.75f));

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.0f, -5.5f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 40.0f);

        fixture.AssertMatches(scene, camera, "effects/static-particle-billboards.png");
    }

    [VisualFact]
    public void Ribbon_trail_renders_fixed_path_with_gradient_and_tail_columns()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("Ribbon");
        node.SetPosition(new Vector3f(-2.0f, -0.5f, 0.0f));
        var trail = node.CreateComponent<RibbonTrail>();
        trail.SetMaterial(fixture.Resources.Get<Material>("Materials/RibbonTrail.xml"));
        trail.SetUpdateInvisible(true);
        trail.SetVertexDistance(0.15f);
        trail.SetWidth(0.65f);
        trail.SetLifetime(5.0f);
        trail.SetStartColor(Color.Yellow);
        trail.SetEndColor(new Color(1.0f, 0.1f, 0.0f, 0.1f));
        trail.SetStartScale(1.0f);
        trail.SetEndScale(0.25f);
        trail.SetTailColumn(4);
        trail.SetSorted(true);
        trail.OnSetEnabled();

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -7.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 4.2f);

        var positions = new[]
        {
            new Vector3f(-2.0f, -0.5f, 0.0f),
            new Vector3f(-1.2f, 0.5f, 0.0f),
            new Vector3f(-0.2f, 1.0f, 0.0f),
            new Vector3f(0.8f, 0.3f, 0.0f),
            new Vector3f(1.8f, 0.9f, 0.0f)
        };
        for (var i = 0; i < positions.Length; ++i)
        {
            node.SetWorldPosition(positions[i]);
            var postUpdate = new SceneEvents.ScenePostUpdateEvent
            {
                Scene = scene,
                TimeStep = 1.0f / 30.0f
            };
            scene.SendEvent(ref postUpdate);
            trail.Update(new FrameInfo
            {
                Scene = scene,
                Camera = camera,
                FrameNumber = (uint)(i + 1),
                TimeStep = 1.0f / 30.0f
            });
        }

        var trailFrame = new FrameInfo
        {
            Scene = scene,
            Camera = camera,
            FrameNumber = 10,
            TimeStep = 1.0f / 30.0f
        };
        trail.UpdateBatches(trailFrame);
        trail.UpdateBatchesDelayed(trailFrame);
        trail.UpdateGeometry(trailFrame);
        Assert.True(trail.GetBatches()[0].Geometry!.GetIndexCount() > 0);

        fixture.AssertMatches(scene, camera, "effects/ribbon-trail.png");
    }

    [VisualFact]
    public void Static_decal_projects_onto_a_box()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));
        var target = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.6f);
        var decalNode = scene.CreateChild("Decals");
        var decals = decalNode.CreateComponent<DecalSet>();
        decals.SetMaterial(fixture.Resources.Get<Material>("Materials/UrhoDecal.xml"));
        Assert.True(decals.AddDecal(target, new Vector3f(0.0f, 0.0f, -1.4f), Quaternion.IDENTITY,
            1.5f, 1.0f, 1.0f, Vector2f.Zero, Vector2f.One));

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.5f, -6.0f), Vector3f.Zero, 256.0f / 192.0f,
            fov: 40.0f);

        fixture.AssertMatches(scene, camera, "effects/static-decal.png");
    }

    [VisualFact]
    public void Skinned_decal_projects_onto_a_fixed_animated_bone()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var node = scene.CreateChild("Ninja");
        var animated = node.CreateComponent<AnimatedModel>();
        animated.SetModel(fixture.Resources.Get<Model>("Models/Ninja.mdl"), true);
        animated.SetMaterial(fixture.Resources.Get<Material>("Materials/Ninja.xml"));
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var controller = node.CreateComponent<AnimationController>();
        controller.PlayNewExclusive(new AnimationParameters(animation).Time(animation.Length * 0.45f));
        controller.UpdatePose();

        var bone = animated.GetSkeleton().GetBones()
            .FirstOrDefault(static candidate => candidate.Node is not null && candidate.CollisionMask != BoneCollisionShape.None);
        Assert.NotNull(bone?.Node);
        var decals = scene.CreateChild("SkinnedDecals").CreateComponent<DecalSet>();
        decals.SetMaterial(fixture.Resources.Get<Material>("Materials/UrhoDecalAlphaMaskTwoSided.xml"));
        Assert.True(decals.AddDecal(animated, bone!.Node!.GetWorldPosition(), Quaternion.IDENTITY,
            0.8f, 1.0f, 0.8f, Vector2f.Zero, Vector2f.One));

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.2f, -4.2f), new Vector3f(0.0f, 1.0f, 0.0f),
            256.0f / 192.0f, fov: 40.0f);

        fixture.AssertMatches(scene, camera, "effects/skinned-decal.png");
    }

    [VisualFact]
    public void Decal_lifetime_capacity_and_buffer_optimization_update_visually()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        var before = CaptureDecalState(fixture, 0.0f, optimize: false);
        var after = CaptureDecalState(fixture, 1.0f, optimize: true);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(before, "effects/decal-lifetime-before.png"),
            new VisualRegressionImageCapture(after, "effects/decal-lifetime-expired.png"));
    }

    [VisualFact]
    public void Ribbon_bone_trail_follows_a_fixed_animated_pose()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("Ninja");
        node.SetScale(0.9f);
        var animated = node.CreateComponent<AnimatedModel>();
        animated.SetModel(fixture.Resources.Get<Model>("Models/Ninja.mdl"), true);
        animated.SetMaterial(fixture.Resources.Get<Material>("Materials/Ninja.xml"));
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var controller = node.CreateComponent<AnimationController>();
        controller.PlayNewExclusive(new AnimationParameters(animation).Time(animation.Length * 0.4f));
        controller.UpdatePose();

        var bone = animated.GetSkeleton().GetBones()
            .FirstOrDefault(static candidate => candidate.Node is not null && candidate.ParentIndex != 0);
        Assert.NotNull(bone?.Node);
        var trailNode = bone!.Node!.CreateChild("BoneTrail");
        var trail = trailNode.CreateComponent<RibbonTrail>();
        trail.SetTrailType(TrailType.Bone);
        trail.SetMaterial(fixture.Resources.Get<Material>("Materials/RibbonTrail.xml"));
        trail.SetWidth(0.35f);
        trail.SetLifetime(4.0f);
        trail.SetStartColor(Color.Cyan);
        trail.SetEndColor(new Color(0.1f, 0.2f, 1.0f, 0.1f));
        trail.SetUpdateInvisible(true);
        trail.OnSetEnabled();

        var points = new[] { new Vector3f(-0.2f, 0.0f, 0.0f), new Vector3f(0.0f, 0.4f, 0.0f), new Vector3f(0.25f, 0.75f, 0.0f) };
        for (var i = 0; i < points.Length; ++i)
        {
            trailNode.SetPosition(points[i]);
            var postUpdate = new SceneEvents.ScenePostUpdateEvent
            {
                Scene = scene,
                TimeStep = 1.0f / 30.0f
            };
            scene.SendEvent(ref postUpdate);
            trail.Update(new FrameInfo
            {
                Scene = scene,
                Camera = null,
                FrameNumber = (uint)(i + 1),
                TimeStep = 1.0f / 30.0f
            });
        }
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.25f, -4.2f), new Vector3f(0.0f, 1.0f, 0.0f),
            256.0f / 192.0f, fov: 40.0f);
        var frame = new FrameInfo { Scene = scene, Camera = camera, FrameNumber = 10, TimeStep = 1.0f / 30.0f };
        trail.UpdateBatches(frame);
        trail.UpdateBatchesDelayed(frame);
        trail.UpdateGeometry(frame);
        Assert.True(trail.GetBatches()[0].Geometry!.GetIndexCount() > 0);

        fixture.AssertMatches(scene, camera, "effects/ribbon-bone-trail.png");
    }

    private static VisualRegressionImage CaptureBillboardView(
        VisualRegressionFixture fixture, FaceCameraMode faceCameraMode)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("Billboards");
        var set = node.CreateComponent<BillboardSet>();
        set.SetMaterial(fixture.Resources.Get<Material>("Materials/Particle.xml"));
        set.SetFaceCameraMode(faceCameraMode);
        set.SetNumBillboards(3);
        for (var i = 0; i < 3; ++i)
        {
            ref var billboard = ref set.GetBillboard((uint)i);
            billboard.Position = new Vector3f((i - 1) * 0.9f, (i & 1) == 0 ? 0.35f : -0.35f, 0.0f);
            billboard.Size = new Vector2f(0.75f, 0.75f);
            billboard.Uv = Rect.Positive;
            billboard.Color = i switch
            {
                0 => Color.Red,
                1 => Color.Green,
                _ => Color.Blue
            };
            billboard.Rotation = i * 18.0f;
            billboard.Direction = new Vector3f(0.3f, 0.0f, 1.0f).Normalized;
            billboard.Enabled = true;
        }
        set.Commit();

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureBillboardOrdering(
        VisualRegressionFixture fixture, bool sorted)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("SortedBillboards");
        var set = node.CreateComponent<BillboardSet>();
        set.SetMaterial(fixture.Resources.Get<Material>("Materials/Smoke.xml"));
        set.SetSorted(sorted);
        set.SetNumBillboards(2);
        ref var first = ref set.GetBillboard(0);
        first.Position = new Vector3f(0.0f, 0.0f, 0.35f);
        first.Size = new Vector2f(1.5f, 1.5f);
        first.Color = new Color(1.0f, 0.1f, 0.1f, 0.55f);
        first.Enabled = true;
        ref var second = ref set.GetBillboard(1);
        second.Position = new Vector3f(0.0f, 0.0f, -0.35f);
        second.Size = new Vector2f(1.5f, 1.5f);
        second.Color = new Color(0.1f, 0.25f, 1.0f, 0.55f);
        second.Enabled = true;
        set.Commit();
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureParticleShape(
        VisualRegressionFixture fixture, EmitterType emitterType)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("ParticleShape");
        var emitter = new VisualParticleEmitter();
        node.AddComponent(emitter);
        using var effect = CreateParticleEffect(fixture, emitterType, relative: false, scaled: true,
            sorted: false, fixedScreenSize: false);
        effect.SetNumParticles(12);
        emitter.SetEffect(effect);
        emitter.SetEmitting(false);
        var previousSeed = MathDefs.GetRandomSeed();
        MathDefs.SetRandomSeed(7);
        try
        {
            for (var i = 0; i < 12; ++i)
                Assert.True(emitter.EmitForVisual());
        }
        finally
        {
            MathDefs.SetRandomSeed(previousSeed);
        }
        emitter.Commit();
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureParticleFrame(
        VisualRegressionFixture fixture, float timer)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("ParticleFrames");
        var emitter = new VisualParticleEmitter();
        node.AddComponent(emitter);
        using var effect = CreateParticleEffect(fixture, EmitterType.Sphere, relative: true, scaled: true,
            sorted: true, fixedScreenSize: false);
        effect.SetColorFrames([
            new ColorFrame(new Color(0.1f, 0.4f, 1.0f, 1.0f), 0.0f),
            new ColorFrame(new Color(1.0f, 0.75f, 0.1f, 1.0f), 0.5f),
            new ColorFrame(new Color(1.0f, 0.05f, 0.0f, 0.0f), 1.0f)
        ]);
        effect.SetTextureFrames([
            new TextureFrame { Uv = new Rect(0.0f, 0.0f, 0.5f, 1.0f), Time = 0.0f },
            new TextureFrame { Uv = new Rect(0.5f, 0.0f, 1.0f, 1.0f), Time = 0.5f }
        ]);
        effect.SetSizeAdd(0.15f);
        effect.SetSizeMul(1.25f);
        emitter.SetEffect(effect);
        emitter.SetEmitting(false);
        SetFixedParticle(emitter, timer, new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector2f(1.25f, 1.25f), Color.White);
        StepParticle(scene, emitter, 0.0f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureParticleMode(
        VisualRegressionFixture fixture, bool relative, bool scaled, bool sorted, bool fixedScreenSize)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("ParticleMode");
        node.SetPosition(new Vector3f(0.4f, 0.2f, 0.0f));
        node.SetScale(1.7f);
        var emitter = new VisualParticleEmitter();
        node.AddComponent(emitter);
        using var effect = CreateParticleEffect(fixture, EmitterType.Box, relative, scaled, sorted, fixedScreenSize);
        emitter.SetEffect(effect);
        emitter.SetEmitting(false);
        SetFixedParticle(emitter, 0.0f, new Vector3f(-0.45f, 0.0f, 0.0f),
            new Vector2f(0.6f, 0.6f), new Color(0.2f, 0.8f, 1.0f, 0.85f));
        SetFixedParticle(emitter, 0.0f, new Vector3f(0.45f, 0.0f, 0.1f),
            new Vector2f(0.6f, 0.6f), new Color(1.0f, 0.25f, 0.15f, 0.85f), append: true);
        emitter.Commit();
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.5f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureParticleForce(
        VisualRegressionFixture fixture, float timeStep)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("ParticleForce");
        var emitter = new VisualParticleEmitter();
        node.AddComponent(emitter);
        using var effect = CreateParticleEffect(fixture, EmitterType.Box, relative: true, scaled: true,
            sorted: true, fixedScreenSize: false);
        effect.SetConstantForce(new Vector3f(0.0f, 1.8f, 0.0f));
        effect.SetDampingForce(0.5f);
        effect.SetSizeAdd(0.25f);
        effect.SetSizeMul(1.1f);
        effect.SetMaxTimeToLive(1.0f);
        emitter.SetEffect(effect);
        emitter.SetEmitting(false);
        SetFixedParticle(emitter, timeStep, Vector3f.Zero, new Vector2f(0.75f, 0.75f), Color.White);
        StepParticle(scene, emitter, timeStep);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 3.0f);
        return fixture.Capture(scene, camera);
    }

    private static ParticleEffect CreateParticleEffect(
        VisualRegressionFixture fixture, EmitterType emitterType,
        bool relative, bool scaled, bool sorted, bool fixedScreenSize)
    {
        var effect = new ParticleEffect { Name = "VisualParticleEffect" };
        effect.SetMaterial(fixture.Resources.Get<Material>("Materials/Particle.xml"));
        effect.SetNumParticles(2);
        effect.SetEmitterType(emitterType);
        effect.SetEmitterSize(new Vector3f(1.5f, 1.2f, 1.0f));
        effect.SetMinDirection(Vector3f.Up);
        effect.SetMaxDirection(Vector3f.Up);
        effect.SetMinParticleSize(new Vector2f(0.65f, 0.65f));
        effect.SetMaxParticleSize(new Vector2f(0.65f, 0.65f));
        effect.SetMinVelocity(0.0f);
        effect.SetMaxVelocity(0.0f);
        effect.SetMinTimeToLive(3.0f);
        effect.SetMaxTimeToLive(3.0f);
        effect.SetMinRotation(0.0f);
        effect.SetMaxRotation(0.0f);
        effect.SetRelative(relative);
        effect.SetScaled(scaled);
        effect.SetSorted(sorted);
        effect.SetFixedScreenSize(fixedScreenSize);
        effect.SetFaceCameraMode(FaceCameraMode.RotateY);
        effect.SetColorFrames([new ColorFrame(Color.White, 0.0f)]);
        return effect;
    }

    private static void SetFixedParticle(
        VisualParticleEmitter emitter, float timer, Vector3f position, Vector2f size,
        Color color, bool append = false)
    {
        if (!append)
        {
            var particles = new VariantVector
            {
                new Variant(1), new Variant(Vector3f.Zero), new Variant(size), new Variant(timer),
                new Variant(3.0f), new Variant(1.0f), new Variant(0.0f), new Variant(0), new Variant(0)
            };
            emitter.SetParticlesAttr(particles);
            var billboards = new VariantVector
            {
                new Variant(1), new Variant(position), new Variant(size),
                new Variant(new Vector4f(0.0f, 0.0f, 1.0f, 1.0f)), new Variant(color),
                new Variant(0.0f), new Variant(Vector3f.Forward), new Variant(true)
            };
            emitter.SetBillboardsAttr(billboards);
        }
        else
        {
            var billboards = emitter.GetBillboards();
            if (billboards.Count < 2)
                emitter.SetNumBillboards(2);
            ref var billboard = ref emitter.GetBillboard(1);
            billboard.Position = position;
            billboard.Size = size;
            billboard.Uv = Rect.Positive;
            billboard.Color = color;
            billboard.Enabled = true;
        }
    }

    private static void StepParticle(Scene scene, VisualParticleEmitter emitter, float timeStep)
    {
        var postUpdate = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = timeStep };
        scene.SendEvent(ref postUpdate);
        emitter.Step(new FrameInfo { Scene = scene, Camera = null, FrameNumber = 1, TimeStep = timeStep });
        emitter.Commit();
    }

    private static VisualRegressionImage CaptureDecalState(
        VisualRegressionFixture fixture, float timeStep, bool optimize)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));
        var target = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.6f);
        var decals = scene.CreateChild("Decals").CreateComponent<DecalSet>();
        decals.SetMaterial(fixture.Resources.Get<Material>("Materials/UrhoDecal.xml"));
        decals.SetMaxVertices(12);
        decals.SetMaxIndices(18);
        decals.SetOptimizeBufferSize(optimize);
        Assert.True(decals.AddDecal(target, new Vector3f(0.0f, 0.0f, -1.4f), Quaternion.IDENTITY,
            1.2f, 1.0f, 1.0f, Vector2f.Zero, Vector2f.One, timeToLive: 0.5f));
        var postUpdate = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = timeStep };
        scene.SendEvent(ref postUpdate);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.2f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.5f, -6.0f), Vector3f.Zero, 128.0f / 96.0f,
            fov: 40.0f);
        return fixture.Capture(scene, camera);
    }

    private sealed class VisualParticleEmitter : ParticleEmitter
    {
        public bool EmitForVisual() => EmitNewParticle();

        public void Step(FrameInfo frame) => Update(frame);
    }

    private static void AddStaticParticles(
        VisualRegressionFixture fixture,
        Scene scene,
        string effectName,
        Vector3f position,
        Color color)
    {
        var node = scene.CreateChild(Path.GetFileNameWithoutExtension(effectName));
        node.SetPosition(position);
        var emitter = node.CreateComponent<ParticleEmitter>();
        emitter.SetEffect(fixture.Resources.Get<ParticleEffect>(effectName));
        emitter.SetSerializeParticles(true);

        var particles = new VariantVector { new Variant(4) };
        for (var i = 0; i < 4; ++i)
        {
            particles.Add(new Variant(Vector3f.Zero));
            particles.Add(new Variant(new Vector2f(0.7f, 0.7f)));
            particles.Add(new Variant(0.0f));
            particles.Add(new Variant(100.0f));
            particles.Add(new Variant(1.0f));
            particles.Add(new Variant(0.0f));
            particles.Add(new Variant(0));
            particles.Add(new Variant(0));
        }
        emitter.SetParticlesAttr(particles);

        var billboards = new VariantVector { new Variant(4) };
        for (var i = 0; i < 4; ++i)
        {
            billboards.Add(new Variant(new Vector3f((i - 1.5f) * 0.35f, (i & 1) * 0.45f, 0.0f)));
            billboards.Add(new Variant(new Vector2f(0.8f, 0.8f)));
            billboards.Add(new Variant(new Vector4f(0.0f, 0.0f, 1.0f, 1.0f)));
            billboards.Add(new Variant(color));
            billboards.Add(new Variant(i * 12.0f));
            billboards.Add(new Variant(Vector3f.Forward));
            billboards.Add(new Variant(true));
        }
        emitter.SetBillboardsAttr(billboards);
        emitter.SetEmitting(false);
    }
}
