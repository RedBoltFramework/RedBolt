using RedBolt;
using RedBolt.Audio;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AudioTests
{
    private sealed class TestSoundStream : SoundStream
    {
        public override uint GetData(sbyte[] dest, uint numBytes) => 0;
    }

    private readonly HeadlessEngineFixture _fixture;

    public AudioTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Audio_constructor_preserves_cpp_default_interpolation_state()
    {
        var audio = _fixture.Engine.GetSubsystem<AudioSystem>()!;

        Assert.False(audio.GetInterpolation());
    }

    [Fact]
    public void Audio_constructor_registers_cpp_audio_library_objects()
    {
        Assert.NotNull(_fixture.Context.GetReflection<Sound>());
        Assert.NotNull(_fixture.Context.GetReflection<SoundSource>());
        Assert.NotNull(_fixture.Context.GetReflection<SoundSource3D>());
        Assert.NotNull(_fixture.Context.GetReflection<SoundListener>());
    }

    [Fact]
    public void Sound_source_starts_from_the_first_sample()
    {
        var scene = new SceneType();
        var source = scene.CreateChild().CreateComponent<SoundSource>();
        var sound = new Sound();
        sound.SetData([0, 0, 64, 0], 4);
        sound.SetFormat(44100, sixteenBit: true, stereo: false);

        source.Play(sound);

        Assert.True(source.IsPlaying());
        Assert.Same(sound, source.GetSound());
        Assert.Equal(0, source.GetPlayPosition());

        source.Stop();
    }

    [Fact]
    public void Sound_source_uses_play_position_as_playing_state()
    {
        var scene = new SceneType();
        var source = scene.CreateChild().CreateComponent<SoundSource>();
        var sound = new Sound();
        sound.SetData([0, 0, 64, 0, 0, 0], 6);
        sound.SetFormat(44100, sixteenBit: true, stereo: false);

        source.Play(sound);
        source.SetPlayPosition(1);

        Assert.True(source.IsPlaying());
        Assert.Equal(2, source.GetPlayPosition());

        source.Stop();

        Assert.False(source.IsPlaying());
        Assert.Equal(0, source.GetPlayPosition());
    }

    [Fact]
    public void Sound_stream_defaults_to_44100_hz()
    {
        var stream = new TestSoundStream();

        Assert.Equal(44100u, stream.GetIntFrequency());
    }

    [Fact]
    public void Buffered_sound_stream_retains_shared_input_storage()
    {
        var stream = new BufferedSoundStream();
        var source = new byte[] { 1, 2, 3, 4 };
        stream.AddData(source, (uint)source.Length);
        source[0] = 9;
        var destination = new sbyte[source.Length];

        Assert.Equal((uint)source.Length, stream.GetData(destination, (uint)destination.Length));
        Assert.Equal(unchecked((sbyte)9), destination[0]);
    }

    [Fact]
    public void Microphone_get_data_is_live_and_copy_data_is_independent()
    {
        var microphone = new Microphone();
        var liveData = microphone.GetData();
        liveData.Add(42);
        var copy = new List<short>();

        microphone.CopyData(copy);
        liveData[0] = 7;

        Assert.Equal(7, microphone.GetData()[0]);
        Assert.Equal(42, copy[0]);
    }

    [Fact]
    public void Microphone_callback_update_appends_sixteen_bit_samples()
    {
        using var microphone = new Microphone();
        microphone.SetEnabled(true);

        microphone.Update([0x34, 0x12, 0xCC, 0xFF], 4);

        Assert.Equal([0x1234, -52], microphone.GetData());
    }

    [Fact]
    public void Microphone_has_generated_object_factory()
    {
        var reflection = _fixture.Context.GetReflection<Microphone>();

        Assert.NotNull(reflection);
        Assert.True(reflection.HasObjectFactory());
    }

    [Fact]
    public void Wav_loader_preserves_sixteen_bit_samples()
    {
        var wav = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 36, 0, 0, 0,
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
            (byte)'f', (byte)'m', (byte)'t', (byte)' ', 16, 0, 0, 0,
            1, 0, 1, 0, 68, 172, 0, 0, 136, 88, 1, 0, 2, 0, 16, 0,
            (byte)'d', (byte)'a', (byte)'t', (byte)'a', 2, 0, 0, 0,
            0xFF, 0xF8
        };
        var sound = new Sound();

        Assert.True(sound.LoadWav(new MemoryBuffer(wav)));
        var data = sound.GetData()!;
        Assert.Equal(unchecked((sbyte)0xFF), data[0]);
        Assert.Equal(unchecked((sbyte)0xF8), data[1]);
    }

    [Fact]
    public void Ogg_vorbis_stream_decodes_pcm()
    {
        var path = Path.Combine(RbfxTestData.Data, "Music", "Ninja Gods.ogg");
        Assert.True(System.IO.File.Exists(path));

        var sound = new Sound();
        using (var source = new MemoryBuffer(System.IO.File.ReadAllBytes(path)))
            Assert.True(sound.LoadOggVorbis(source));

        Assert.True(sound.GetIntFrequency() > 0);
        var stream = new OggVorbisSoundStream(sound);
        var data = new sbyte[4096];
        var bytes = stream.GetData(data, (uint)data.Length);

        Assert.True(bytes > 0);
        Assert.Contains(data[..(int)bytes], sample => sample != 0);
        Assert.True(stream.Seek(0));
    }

    [Fact]
    public void Sound_source_3d_draws_attenuation_geometry()
    {
        var scene = new SceneType();
        var source = scene.CreateChild("SoundSource3D").CreateComponent<SoundSource3D>();
        source.SetDistanceAttenuation(1.0f, 5.0f, 2.0f);
        var debugRenderer = new DebugRenderer();
        debugRenderer.SetView(new Camera());

        source.DrawDebugGeometry(debugRenderer, depthTest: false);

        Assert.True(debugRenderer.HasContent());
    }

    [Fact]
    public void Audio_component_attributes_match_cpp_registration()
    {
        var scene = new SceneType();
        var source = scene.CreateComponent<SoundSource>();
        var source3D = scene.CreateComponent<SoundSource3D>();

        Assert.Equal(
            ["Is Enabled", "Sound", "Type", "Frequency", "Gain", "Attenuation", "Panning", "Reach",
                "Low Frequency Effect", "Ignore Scene Time Scale", "Is Playing", "Autoremove Mode", "Play Position"],
            source.GetAttributes().Select(attribute => attribute.Name));
        Assert.Equal(
            ["Is Enabled", "Sound", "Type", "Frequency", "Gain", "Reach", "Low Frequency Effect",
                "Ignore Scene Time Scale", "Is Playing", "Autoremove Mode", "Play Position", "Near Distance",
                "Far Distance", "Inner Angle", "Outer Angle", "Rolloff Factor"],
            source3D.GetAttributes().Select(attribute => attribute.Name));
    }
}
