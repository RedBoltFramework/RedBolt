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
