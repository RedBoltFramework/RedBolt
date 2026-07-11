using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class AudioTests
{
    private readonly HeadlessEngineFixture _fixture;

    public AudioTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Sound_source_starts_from_the_first_sample()
    {
        var scene = new Scene();
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

        Assert.True(sound.LoadWav(new MemoryStream(wav)));
        var data = sound.GetData()!;
        Assert.Equal(unchecked((sbyte)0xFF), data[0]);
        Assert.Equal(unchecked((sbyte)0xF8), data[1]);
    }

    [Fact]
    public void Ogg_vorbis_stream_decodes_pcm()
    {
        var path = Path.Combine(RbfxTestData.Data, "Music", "Ninja Gods.ogg");
        Assert.True(File.Exists(path));

        var sound = new Sound();
        using (var source = File.OpenRead(path))
            Assert.True(sound.LoadOggVorbis(source));

        Assert.True(sound.GetIntFrequency() > 0);
        var stream = new OggVorbisSoundStream(sound);
        var data = new sbyte[4096];
        var bytes = stream.GetData(data, (uint)data.Length);

        Assert.True(bytes > 0);
        Assert.Contains(data[..(int)bytes], sample => sample != 0);
        Assert.True(stream.Seek(0));
    }
}
