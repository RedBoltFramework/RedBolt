using RedBolt.Core;

namespace RedBolt.Tests;

public class StopTokenTests
{
    [Fact]
    public void StopToken_is_not_stopped_by_default()
    {
        var token = new StopToken();

        Assert.False(token.IsStopped());
    }

    [Fact]
    public void StopToken_stop_marks_token_as_stopped()
    {
        var token = new StopToken();

        token.Stop();

        Assert.True(token.IsStopped());
    }

    [Fact]
    public void StopToken_value_copy_shares_stop_state()
    {
        var token = new StopToken();
        var copy = token;

        copy.Stop();

        Assert.True(token.IsStopped());
        Assert.True(copy.IsStopped());
    }
}
