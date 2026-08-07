// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.

#if REDBOLT_PLATFORM_BROWSER
namespace RedBolt.Network;

using Microsoft.Extensions.Logging;

using RedBolt.Core;

[Reflected(ObjectCategory.Category_Network, Factory = false, InheritAttributes = false)]
public partial class DataChannelServer : NetworkServer
{
    public DataChannelServer()
    {
        SetConnectionFactory(static () => new DataChannelConnection());
    }

    public override bool Listen(URL url)
    {
        RedBolt.Logging.Log.GetLogger().LogError(
            "Web browsers cannot host a DataChannel server. Run the sample server as a desktop process.");
        return false;
    }

    public override void Stop() { }
    public override bool IsListening => false;

    public void SetTLSCertificate(
        string certificatePemFile, string keyPemFile, string keyPassword) { }
    public void SetIceServers(IEnumerable<string> servers) { }
    public void SetPortRange(ushort begin, ushort end) { }
    public void SetIceUdpMux(bool enable) { }
    public void SetBindAddress(string address) { }
    public void SetMtu(nuint mtu) { }
}
#endif
