using System;
using System.Threading;

using Xunit;

using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class NamedPipeTests
{
    [Fact]
    public void NamedPipe_is_an_extensible_noncopyable_class()
    {
        Assert.True(typeof(NamedPipe).IsClass);
        Assert.False(typeof(NamedPipe).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(NamedPipe)));
    }

    [Fact]
    public void NamedPipe_server_and_client_exchange_bytes()
    {
        var name = "RedBoltNamedPipe-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipe(name, true);
        using var client = new NamedPipe(name, false);

        Assert.True(server.IsOpen);
        Assert.True(server.IsServer());
        Assert.True(client.IsOpen);
        Assert.False(client.IsServer());
        Assert.Equal(0u, server.Seek(100));

        var request = new byte[] { 1, 2, 3, 4 };
        Assert.Equal((uint)request.Length, client.Write(request));
        Assert.True(TryRead(server, request.Length, out var received));
        Assert.Equal(request, received);

        var response = new byte[] { 5, 6, 7 };
        Assert.Equal((uint)response.Length, server.Write(response));
        Assert.True(TryRead(client, response.Length, out var returned));
        Assert.Equal(response, returned);

        server.Close();
        Assert.False(server.IsOpen);
        Assert.True(server.IsEof());
    }

    [Fact]
    public void NamedPipe_client_requires_an_existing_server()
    {
        using var pipe = new NamedPipe("RedBoltMissingPipe-" + Guid.NewGuid().ToString("N"), false);
        Assert.False(pipe.IsOpen);
        Assert.False(pipe.IsServer());
        Assert.True(pipe.IsEof());
    }

    [Fact]
    public void NamedPipe_can_close_and_reopen_after_read_loop_teardown()
    {
        var firstName = "RedBoltNamedPipe-" + Guid.NewGuid().ToString("N");
        var secondName = "RedBoltNamedPipe-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipe(firstName, true);
        using var client = new NamedPipe(firstName, false);

        Assert.True(server.IsOpen);
        server.Close();
        client.Close();

        Assert.True(server.Open(secondName, true));
        Assert.True(client.Open(secondName, false));
        Assert.Equal(1u, client.Write(new byte[] { 42 }));
        Assert.True(TryRead(server, 1, out var received));
        Assert.Equal(new byte[] { 42 }, received);
    }

    private static bool TryRead(NamedPipe pipe, int size, out byte[] result)
    {
        result = new byte[size];
        for (var i = 0; i < 20; ++i)
        {
            if (pipe.Read(result) == size)
                return true;

            Thread.Sleep(50);
        }

        return false;
    }
}
