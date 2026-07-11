using RedBolt;

namespace RedBolt.Tests;

public sealed class ResourceTests
{
    [Fact]
    public void Failed_load_restores_done_state()
    {
        var resource = new FailingResource();

        Assert.False(resource.Load(new MemoryStream()));
        Assert.Equal(AsyncLoadState.Done, resource.AsyncLoadState);
    }

    [Fact]
    public void Empty_resource_name_is_allowed()
    {
        var resource = new FailingResource();

        resource.SetName(string.Empty);

        Assert.Equal(string.Empty, resource.Name);
    }

    private sealed class FailingResource : Resource
    {
        public FailingResource()
            : base(string.Empty)
        {
        }

        public override bool BeginLoad(Stream source) => false;
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BinaryFileTests
{
    public BinaryFileTests(HeadlessEngineFixture fixture)
    {
        _ = fixture;
    }

    [Fact]
    public void Archive_callbacks_roundtrip_values()
    {
        var file = new BinaryFile();
        var outputValue = 42;

        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref outputValue);
        }));

        var inputValue = 0;
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenSequentialBlock("object");
            archive.Serialize("value", ref inputValue);
        }));
        Assert.Equal(outputValue, inputValue);
    }

    [Fact]
    public void ReadLines_does_not_append_line_after_terminal_newline()
    {
        var file = new BinaryFile();
        file.SetText("first\nsecond\n");

        Assert.Equal(["first", "second"], file.ReadLines());
    }
}
