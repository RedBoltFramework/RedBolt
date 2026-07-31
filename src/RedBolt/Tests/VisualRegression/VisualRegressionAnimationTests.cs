namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionAnimationTests
{
    [VisualFact]
    public void Animated_model_renders_fixed_animation_times()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var first = CapturePose(fixture, SkinningMode.Auto, animation, 0.0f);
        var middle = CapturePose(fixture, SkinningMode.Auto, animation, animation.Length * 0.5f);
        var last = CapturePose(fixture, SkinningMode.Auto, animation, animation.Length * 0.95f);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(first, "animation/ninja-attack-time-0.png"),
            new VisualRegressionImageCapture(middle, "animation/ninja-attack-time-mid.png"),
            new VisualRegressionImageCapture(last, "animation/ninja-attack-time-end.png"));
    }

    [VisualFact]
    public void Animated_model_skinning_modes_render_a_fixed_pose()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var auto = CapturePose(fixture, SkinningMode.Auto, animation, animation.Length * 0.4f);
        var hardware = CapturePose(fixture, SkinningMode.Hardware, animation, animation.Length * 0.4f);
        var software = CapturePose(fixture, SkinningMode.Software, animation, animation.Length * 0.4f);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(auto, "animation/skinning-auto.png"),
            new VisualRegressionImageCapture(hardware, "animation/skinning-hardware.png"),
            new VisualRegressionImageCapture(software, "animation/skinning-software.png"));
    }

    [VisualFact]
    public void Animation_loop_speed_and_wrap_are_deterministic()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");

        var loopStart = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation).Looped().Time(0.0f));
            controller.Update(0.0f);
        });
        var loopWrapped = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation).Looped().Time(0.0f));
            controller.Update(animation.Length);
        });
        var fast = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation).Looped().Speed(2.0f));
            controller.Update(animation.Length * 0.25f);
        });
        var slow = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation).Looped().Speed(0.5f));
            controller.Update(animation.Length * 0.5f);
        });

        fixture.AssertMatches(
            new VisualRegressionImageCapture(loopStart, "animation/loop-start.png"),
            new VisualRegressionImageCapture(loopWrapped, "animation/loop-wrapped.png"),
            new VisualRegressionImageCapture(fast, "animation/speed-fast.png"),
            new VisualRegressionImageCapture(slow, "animation/speed-slow.png"));
    }

    [VisualFact]
    public void Animation_instances_blend_at_fixed_weights_and_layers()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");

        var blended = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.AddAnimation(new AnimationParameters(animation)
                .Time(0.0f).Weight(0.5f));
            controller.AddAnimation(new AnimationParameters(animation)
                .Time(animation.Length * 0.65f).Weight(0.5f));
            controller.Update(0.0f);
        });
        var layered = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.AddAnimation(new AnimationParameters(animation)
                .Layer(0).Time(0.0f));
            var startBone = model.GetSkeleton().GetBones()
                .FirstOrDefault(static bone => !string.IsNullOrEmpty(bone.Name) && bone.ParentIndex != 0)?.Name;
            controller.AddAnimation(new AnimationParameters(animation)
                .Layer(1).StartBone(startBone ?? string.Empty)
                .Time(animation.Length * 0.6f).Weight(0.75f));
            controller.Update(0.0f);
        });

        fixture.AssertMatches(
            new VisualRegressionImageCapture(blended, "animation/blended-weights.png"),
            new VisualRegressionImageCapture(layered, "animation/layered-start-bone.png"));
    }

    [VisualFact]
    public void Animation_additive_and_reverse_playback_change_the_pose()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var additive = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation)
                .Looped().Time(animation.Length * 0.45f).Additive().Weight(0.75f));
            controller.Update(0.0f);
        });
        var reverse = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation)
                .Looped().Time(animation.Length * 0.75f).Speed(-1.0f));
            controller.Update(animation.Length * 0.2f);
        });

        fixture.AssertMatches(
            new VisualRegressionImageCapture(additive, "animation/additive.png"),
            new VisualRegressionImageCapture(reverse, "animation/reverse-speed.png"));
    }

    [VisualFact]
    public void Animation_controller_time_weight_speed_and_skeleton_reset_are_visible()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        var configured = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation).Looped());
            Assert.True(controller.UpdateAnimationTime(animation, animation.Length * 0.55f));
            Assert.True(controller.UpdateAnimationWeight(animation, 0.8f));
            Assert.True(controller.UpdateAnimationSpeed(animation, 1.25f));
            controller.UpdatePose();
        });
        var reset = CaptureConfigured(fixture, animation, (controller, model) =>
        {
            controller.PlayNewExclusive(new AnimationParameters(animation)
                .Looped().Time(animation.Length * 0.55f));
            controller.UpdatePose();
            controller.SetSkeletonReset(true);
            controller.StopAll();
            controller.Update(0.0f);
        });

        fixture.AssertMatches(
            new VisualRegressionImageCapture(configured, "animation/controller-parameters.png"),
            new VisualRegressionImageCapture(reset, "animation/skeleton-reset.png"));
    }

    [VisualFact]
    public void Animated_model_morph_weights_render_zero_half_and_full_shapes()
    {
        using var fixture = new VisualRegressionFixture();
        var zero = CaptureMorph(fixture, 0.0f, byName: false);
        var half = CaptureMorph(fixture, 0.5f, byName: true);
        var full = CaptureMorph(fixture, 1.0f, byName: false);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(zero, "animation/morph-zero.png"),
            new VisualRegressionImageCapture(half, "animation/morph-half.png"),
            new VisualRegressionImageCapture(full, "animation/morph-full.png"));
    }

    [VisualFact]
    public void Animated_model_bone_attachment_follows_a_fixed_pose()
    {
        using var fixture = new VisualRegressionFixture();
        var animation = fixture.Resources.Get<Animation>("Models/Ninja_Attack3.ani");
        using var scene = CreateAnimatedScene(fixture, animation, out var camera, out var model, out var controller);
        controller.PlayNewExclusive(new AnimationParameters(animation)
            .Looped().Time(animation.Length * 0.42f));
        controller.UpdatePose();

        var bone = model.GetSkeleton().GetBones()
            .FirstOrDefault(static candidate => candidate.Node is not null && candidate.ParentIndex != 0);
        Assert.NotNull(bone?.Node);
        var attachment = bone!.Node!.CreateChild("BoneAttachment");
        attachment.SetPosition(new Vector3f(0.0f, 0.0f, 0.12f));
        attachment.SetScale(0.18f);
        var attachedModel = attachment.CreateComponent<StaticModel>();
        attachedModel.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        attachedModel.SetMaterial(fixture.Resources.Get<Material>("Materials/Constant/GlowingRed.xml"));

        fixture.AssertMatches(scene, camera, "animation/bone-attachment.png");
    }

    [VisualFact]
    public void Animation_attribute_track_and_trigger_change_a_visible_overlay()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        var node = scene.CreateChild("AnimatedText");
        node.SetPosition(new Vector3f(-0.9f, 0.0f, 0.0f));
        var text = node.CreateComponent<Text3D>();
        text.SetFont(fixture.Resources.Get<Font>("Fonts/Anonymous Pro.ttf"), 40.0f);
        text.SetText("A");

        var animation = new Animation { Name = "VisualAttributeAnimation" };
        animation.SetLength(1.0f);
        var textTrack = animation.CreateVariantTrack("@Text3D/Text");
        textTrack.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = new Variant("A") });
        textTrack.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = new Variant("B") });
        textTrack.Commit();
        animation.AddTrigger(0.5f, false, new Variant("trigger"));

        var controller = node.CreateComponent<AnimationController>();
        controller.PlayNewExclusive(new AnimationParameters(animation).Time(0.0f));
        controller.Update(0.0f);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -4.0f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 2.5f);

        var attributeStart = fixture.Capture(scene, camera);
        Assert.True(controller.UpdateAnimationTime(animation, 1.0f));
        controller.Update(0.0f);
        var attributeEnd = fixture.Capture(scene, camera);
        fixture.AssertMatches(
            new VisualRegressionImageCapture(attributeStart, "animation/attribute-start.png"),
            new VisualRegressionImageCapture(attributeEnd, "animation/attribute-end.png"));

        var triggerSeen = false;
        controller.SubscribeToEvent<DrawableEvents.AnimationTriggerEvent>(
            (ref DrawableEvents.AnimationTriggerEvent eventData) => triggerSeen = true);
        Assert.True(controller.UpdateAnimationTime(animation, 0.0f));
        controller.Update(0.0f);
        controller.Update(0.75f);
        typeof(AnimationController).GetMethod(
            "SendTriggerEvents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(controller, null);
        Assert.True(triggerSeen);
    }

    private static VisualRegressionImage CapturePose(
        VisualRegressionFixture fixture,
        SkinningMode skinningMode,
        Animation animation,
        float time)
    {
        fixture.Renderer.SetSkinningMode(skinningMode);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));
        var node = scene.CreateChild("Ninja");
        node.SetPosition(new Vector3f(0.0f, 0.0f, 0.0f));
        node.SetScale(0.9f);

        var model = node.CreateComponent<AnimatedModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Ninja.mdl"), true);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Ninja.xml"));

        var controller = node.CreateComponent<AnimationController>();
        controller.PlayNewExclusive(new AnimationParameters(animation).Looped().Time(time));
        controller.Update(0.0f);

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f),
            new Vector3f(0.0f, 1.0f, 0.0f), Color.White, brightness: 1.5f);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.25f, -4.2f), new Vector3f(0.0f, 1.0f, 0.0f), 128.0f / 96.0f,
            fov: 40.0f);
        return fixture.Capture(scene, camera);
    }

    private static VisualRegressionImage CaptureConfigured(
        VisualRegressionFixture fixture,
        Animation animation,
        Action<AnimationController, AnimatedModel> configure)
    {
        using var scene = CreateAnimatedScene(fixture, animation, out var camera,
            out var model, out var controller);
        configure(controller, model);
        controller.Update(0.0f);
        model.ApplyAnimation();
        return fixture.Capture(scene, camera);
    }

    private static Scene CreateAnimatedScene(
        VisualRegressionFixture fixture,
        Animation animation,
        out Camera camera,
        out AnimatedModel model,
        out AnimationController controller)
    {
        var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.12f, 0.12f, 0.12f));
        var node = scene.CreateChild("Ninja");
        node.SetScale(0.9f);
        model = node.CreateComponent<AnimatedModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Ninja.mdl"), true);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Ninja.xml"));
        controller = node.CreateComponent<AnimationController>();

        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f),
            new Vector3f(0.0f, 1.0f, 0.0f), Color.White, brightness: 1.5f);
        camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 1.25f, -4.2f), new Vector3f(0.0f, 1.0f, 0.0f),
            128.0f / 96.0f, fov: 40.0f);
        return scene;
    }

    private static VisualRegressionImage CaptureMorph(
        VisualRegressionFixture fixture, float weight, bool byName)
    {
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var node = scene.CreateChild("MorphModel");
        var animated = node.CreateComponent<AnimatedModel>();
        var model = CreateMorphModel();
        animated.SetModel(model, false);
        animated.SetMaterial(fixture.Resources.Get<Material>("Materials/Constant/UnlitWhite.xml"));
        if (byName)
            animated.SetMorphWeight("Raise", weight);
        else
            animated.SetMorphWeight(0, weight);
        animated.ApplyAnimation();
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -4.0f), Vector3f.Zero, 128.0f / 96.0f,
            orthographic: true, orthoSize: 2.8f);
        return fixture.Capture(scene, camera);
    }

    private static Model CreateMorphModel()
    {
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetShadowed(true);
        Assert.True(vertexBuffer.SetSize(4, VertexMask.Position));
        var positions = new[]
        {
            new Vector3f(-0.9f, -0.7f, 0.0f), new Vector3f(0.9f, -0.7f, 0.0f),
            new Vector3f(0.9f, 0.7f, 0.0f), new Vector3f(-0.9f, 0.7f, 0.0f)
        };
        var data = new byte[checked((int)vertexBuffer.GetVertexSize() * positions.Length)];
        for (var i = 0; i < positions.Length; ++i)
            MemoryMarshal.Write(data.AsSpan(i * checked((int)vertexBuffer.GetVertexSize())), in positions[i]);
        vertexBuffer.Update(data);

        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(6));
        indexBuffer.SetUnpackedData([0u, 2u, 1u, 0u, 3u, 2u]);
        var geometry = new Geometry();
        geometry.SetVertexBuffers([vertexBuffer]);
        geometry.SetIndexBuffer(indexBuffer);
        Assert.True(geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 6));

        var model = new Model { Name = "VisualMorphModel" };
        Assert.True(model.SetVertexBuffers([vertexBuffer], [0], [4]));
        Assert.True(model.SetIndexBuffers([indexBuffer]));
        model.SetNumGeometries(1);
        Assert.True(model.SetGeometry(0, 0, geometry));
        model.SetGeometryCenter(0, Vector3f.Zero);
        model.SetBoundingBox(new BoundingBox(-1.0f, 1.0f));
        var morphStride = sizeof(uint) + sizeof(float) * 3;
        var morphData = new byte[checked(positions.Length * morphStride)];
        for (var i = 0; i < positions.Length; ++i)
        {
            var offset = i * morphStride;
            MemoryMarshal.Write(morphData.AsSpan(offset), (uint)i);
            MemoryMarshal.Write(morphData.AsSpan(offset + sizeof(uint)),
                new Vector3f(0.0f, i is 1 or 2 ? 0.35f : -0.1f, 0.0f));
        }
        model.SetMorphs([
            new ModelMorph
            {
                Name = "Raise",
                NameHash = new StringHash("Raise"),
                Buffers = new Dictionary<uint, VertexBufferMorph>
                {
                    [0] = new VertexBufferMorph
                    {
                        ElementMask = VertexMask.Position,
                        VertexCount = (uint)positions.Length,
                        DataSize = (uint)morphData.Length,
                        MorphData = morphData
                    }
                }
            }
        ]);
        return model;
    }
}
