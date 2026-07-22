using RedBolt;

namespace RedBolt.Tests;

public sealed class AnimationTests
{
    [Fact]
    public void Animation_types_preserve_cpp_kinds()
    {
        Assert.True(typeof(Animation).IsClass);
        Assert.False(typeof(Animation).IsSealed);
        Assert.True(typeof(AnimationTriggerPoint).IsValueType);
    }

    [Fact]
    public void GetTrigger_returns_borrowed_vector_element()
    {
        var animation = new Animation();
        animation.SetNumTriggers(1);

        ref var trigger = ref animation.GetTrigger(0);
        trigger.Time = 2.0f;
        ref var missingTrigger = ref animation.GetTrigger(-1);

        Assert.Equal(2.0f, animation.Triggers[0].Time);
        Assert.True(System.Runtime.CompilerServices.Unsafe.IsNullRef(ref missingTrigger));
    }

    [Fact]
    public void Animation_registers_empty_named_factory()
    {
        var context = new Context();
        Animation.RegisterObject(context);

        using var animation = Assert.IsType<Animation>(context.CreateObject(ObjectReflection<Animation>.TypeId));
        Assert.Equal(string.Empty, animation.Name);
    }

    [Fact]
    public void Animation_tracks_and_triggers_follow_resource_state_rules()
    {
        var animation = new Animation { Name = "animation" };
        animation.SetAnimationName("walk");
        animation.SetLength(-1.0f);
        Assert.Equal(0.0f, animation.Length);
        Assert.Equal(new StringHash("walk"), animation.AnimationNameHash);

        animation.SetLength(2.0f);
        var track = animation.CreateTrack("root");
        Assert.Same(track, animation.CreateTrack("root"));
        Assert.Equal(1, animation.NumTracks);

        animation.AddTrigger(1.5f, false, 1);
        animation.AddTrigger(0.5f, true, 2);
        Assert.Equal(1.0f, animation.Triggers[0].Time);
        Assert.Equal(1.5f, animation.Triggers[1].Time);
        Assert.Equal(2, animation.Triggers[0].Data.GetInt());

        animation.SetNumTriggers(3);
        Assert.Equal(3, animation.NumTriggers);
        animation.RemoveTrigger(2);
        Assert.Equal(2, animation.NumTriggers);
        Assert.True(animation.RemoveTrack("root"));
        Assert.False(animation.RemoveTrack("root"));
    }

    [Fact]
    public void Animation_copies_reference_backed_track_and_trigger_values()
    {
        using var animation = new Animation { Name = "animation" };
        using var triggerData = new Variant(new byte[] { 1, 2, 3 });
        animation.AddTrigger(0.5f, false, triggerData);

        var variantTrack = animation.CreateVariantTrack("buffer");
        variantTrack.KeyFrames.Add(new VariantCurvePoint
        {
            Time = 0.0f,
            Value = new Variant(new byte[] { 4, 5, 6 })
        });
        variantTrack.Commit();

        using var clone = animation.Clone("clone");

        triggerData.GetBuffer()[0] = 9;
        variantTrack.KeyFrames[0].Value.GetBuffer()[0] = 8;

        Assert.Equal(1, animation.Triggers[0].Data.GetBuffer()[0]);
        Assert.Equal(1, clone.Triggers[0].Data.GetBuffer()[0]);
        Assert.Equal(4, clone.GetVariantTrack("buffer")!.KeyFrames[0].Value.GetBuffer()[0]);

        using var replacementData = new Variant(new byte[] { 7, 8, 9 });
        animation.SetTrigger(0, new AnimationTriggerPoint { Time = 1.0f, Data = replacementData });
        replacementData.GetBuffer()[0] = 0;

        Assert.Equal(7, animation.Triggers[0].Data.GetBuffer()[0]);
        Assert.Equal(1, clone.Triggers[0].Data.GetBuffer()[0]);
    }

    [Fact]
    public void Animation_clone_values_survive_source_destruction()
    {
        var source = new Animation { Name = "animation" };
        using var triggerData = new Variant(new byte[] { 1, 2, 3 });
        source.AddTrigger(0.5f, false, triggerData);

        var variantTrack = source.CreateVariantTrack("buffer");
        variantTrack.KeyFrames.Add(new VariantCurvePoint
        {
            Time = 0.0f,
            Value = new Variant(new byte[] { 4, 5, 6 })
        });
        variantTrack.Commit();

        using var clone = source.Clone("clone");
        source.Dispose();

        Assert.Equal(1, clone.Triggers[0].Data.GetBuffer()[0]);
        Assert.Equal(4, clone.GetVariantTrack("buffer")!.KeyFrames[0].Value.GetBuffer()[0]);
    }

    [Fact]
    public void Animation_loads_xml_tracks_variants_triggers_and_metadata()
    {
        var xml = new XMLFile { Name = "walk.xml" };
        Assert.True(xml.FromString("""
            <animation name="walk" length="2">
              <transform name="root" position="true" rotation="true" positionweight="0.25">
                <keyframe time="1" position="1 0 0" rotation="0 0 0 1" />
                <keyframe time="0" position="0 0 0" rotation="0 0 0 1" />
              </transform>
              <variant name="speed" type="Float" interpolation="linear">
                <keyframe time="1" value="2" />
                <keyframe time="0" value="1" />
              </variant>
              <trigger normalizedtime="0.5" type="String" value="footstep" />
              <metadata name="source" type="String" value="test" />
            </animation>
            """));

        var animation = new Animation();
        Assert.True(animation.LoadXML(xml.Root));
        Assert.Equal("walk", animation.AnimationName);
        Assert.Equal(2.0f, animation.Length);

        var track = animation.GetTrack("root");
        Assert.NotNull(track);
        Assert.Equal(AnimationChannel.Position | AnimationChannel.Rotation, track.ChannelMask);
        Assert.Equal(0.25f, track.PositionWeight);
        Assert.Equal(0.0f, track.KeyFrames[0].Time);
        Assert.Equal(1.0f, track.KeyFrames[1].Time);

        var variantTrack = animation.GetVariantTrack("speed");
        Assert.NotNull(variantTrack);
        Assert.Equal(VariantType.Float, variantTrack.GetType());
        Assert.Equal(1.0f, variantTrack.KeyFrames[0].Value.GetFloat());
        Assert.Equal(2.0f, variantTrack.KeyFrames[1].Value.GetFloat());

        Assert.Equal(1, animation.NumTriggers);
        Assert.Equal(1.0f, animation.Triggers[0].Time);
        Assert.Equal("footstep", animation.Triggers[0].Data.GetString());
        Assert.Equal("test", animation.GetMetadata("source").GetString());
    }

    [Fact]
    public void Animation_begin_load_selects_xml_by_source_name()
    {
        var animation = new Animation { Name = "walk.ani" };
        using var source = new MemoryBuffer(System.Text.Encoding.UTF8.GetBytes(
            "<animation name=\"walk\" length=\"2\" />"));
        source.SetName("source.xml");

        Assert.True(animation.BeginLoad(source));
        Assert.Equal("walk", animation.AnimationName);
        Assert.Equal(2.0f, animation.Length);
    }

    [Fact]
    public void Animation_save_uses_destination_name_for_trigger_sidecar()
    {
        var previousContext = Context.Instance;
        Context.SetInstance(new Context());
        var directory = Path.Combine(Path.GetTempPath(), $"RedBoltAnimation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var virtualFileSystem = new VirtualFileSystem();
            virtualFileSystem.MountDir(directory);
            Context.Instance.RegisterSubsystem(virtualFileSystem);

            using var animation = new Animation { Name = "resource.ani" };
            animation.AddTrigger(0.5f, false, 1);
            using var destination = new VectorBuffer();
            destination.SetName("saved.ani");

            Assert.True(animation.Save(destination));
            Assert.True(System.IO.File.Exists(Path.Combine(directory, "saved.xml")));
            Assert.False(System.IO.File.Exists(Path.Combine(directory, "resource.xml")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Animation_binary_save_and_load_round_trips_tracks()
    {
        var previousContext = Context.Instance;
        Context.SetInstance(new Context());

        try
        {
            using var cache = new ResourceCache();
            cache.RegisterDefaultResources();
            Context.Instance.RegisterSubsystem(cache);
            var animation = new Animation();
            animation.SetAnimationName("walk");
            animation.SetLength(1.0f);
            var track = animation.CreateTrack("root");
            track.ChannelMask = AnimationChannel.Position | AnimationChannel.Scale;
            track.KeyFrames.Add(new AnimationKeyFrame(0.0f, Vector3f.Zero, Quaternion.IDENTITY, Vector3f.One));
            track.KeyFrames.Add(new AnimationKeyFrame(1.0f, new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.IDENTITY, new Vector3f(2.0f, 2.0f, 2.0f)));

            var variantTrack = animation.CreateVariantTrack("speed");
            variantTrack.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
            variantTrack.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 2.0f });
            variantTrack.Commit();

            using var stream = new VectorBuffer();
            Assert.True(animation.Save(stream));
            stream.Seek(0);

            var loaded = new Animation();
            Assert.True(loaded.BeginLoad(stream));
            Assert.Equal("walk", loaded.AnimationName);
            Assert.Equal(1.0f, loaded.Length);
            Assert.Equal(1, loaded.NumTracks);
            Assert.Equal(1, loaded.NumVariantTracks);
            var loadedTrack = loaded.GetTrack("root");
            var loadedVariantTrack = loaded.GetVariantTrack("speed");
            Assert.NotNull(loadedTrack);
            Assert.NotNull(loadedVariantTrack);
            Assert.Equal(2.0f, loadedTrack!.KeyFrames[1].Position.X);
            Assert.Equal(2.0f, loadedVariantTrack!.KeyFrames[1].Value.GetFloat());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
