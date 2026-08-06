//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Replica;

using RedBolt.Container;
using RedBolt.Core;

public readonly struct NetworkSetting
{
    public NetworkSetting(string name, Variant defaultValue)
    {
        Name = new ConstString(name);
        DefaultValue = defaultValue;
    }

    public ConstString Name { get; }
    public Variant DefaultValue { get; }
}

/// <summary>Network parameters supported by the engine.</summary>
public static class NetworkSettings
{
    /// <summary>Return network setting or default value.</summary>
    public static Variant GetNetworkSetting(VariantMap map, NetworkSetting key) =>
        map.TryGetValue(key.Name.GetHash(), out var value) ? value : key.DefaultValue;

    /// <summary>Set network setting in the map.</summary>
    public static void SetNetworkSetting(VariantMap map, NetworkSetting key, Variant value)
    {
        map[key.Name] = value;
    }

    /// <summary>Set network setting in the map to default value.</summary>
    public static void SetDefaultNetworkSetting(VariantMap map, NetworkSetting key)
    {
        map[key.Name] = key.DefaultValue;
    }

    /// <summary>Version of internal protocol.</summary>
    public static readonly NetworkSetting InternalProtocolVersion = new(nameof(InternalProtocolVersion), 1u);
    /// <summary>Update frequency of the server, frames per second.</summary>
    public static readonly NetworkSetting UpdateFrequency = new(nameof(UpdateFrequency), 30u);
    /// <summary>Connection ID of current client.</summary>
    public static readonly NetworkSetting ConnectionId = new(nameof(ConnectionId), 0u);

    /// <summary>Maximum allowed delay between server time and replica time.</summary>
    public static readonly NetworkSetting InterpolationLimit = new(nameof(InterpolationLimit), 0.25f);
    /// <summary>Maximum number of input frames tracked by the client.</summary>
    public static readonly NetworkSetting MaxInputFrames = new(nameof(MaxInputFrames), 256u);
    /// <summary>Maximum number of input frames sent to server including relevant frame.</summary>
    public static readonly NetworkSetting MaxInputRedundancy = new(nameof(MaxInputRedundancy), 32u);

    /// <summary>Interval in seconds between periodic clock updates.</summary>
    public static readonly NetworkSetting PeriodicClockInterval = new(nameof(PeriodicClockInterval), 1.0f);
    /// <summary>Number of clock ticks used to filter input delay.</summary>
    public static readonly NetworkSetting InputDelayFilterBufferSize = new(nameof(InputDelayFilterBufferSize), 11u);
    /// <summary>Number of clock ticks used to filter input buffer.</summary>
    public static readonly NetworkSetting InputBufferingFilterBufferSize = new(nameof(InputBufferingFilterBufferSize), 11u);
    /// <summary>Number of frames used to evaluate recommended input buffering.</summary>
    public static readonly NetworkSetting InputBufferingWindowSize = new(nameof(InputBufferingWindowSize), 128u);
    /// <summary>Input buffering coefficient A.</summary>
    public static readonly NetworkSetting InputBufferingTweakA = new(nameof(InputBufferingTweakA), 1.3f);
    /// <summary>Input buffering coefficient B.</summary>
    public static readonly NetworkSetting InputBufferingTweakB = new(nameof(InputBufferingTweakB), 1.0f);
    public static readonly NetworkSetting InputBufferingMin = new(nameof(InputBufferingMin), 0u);
    public static readonly NetworkSetting InputBufferingMax = new(nameof(InputBufferingMax), 8u);
    /// <summary>Interval between an object becoming unneeded and replication stopping.</summary>
    public static readonly NetworkSetting RelevanceTimeout = new(nameof(RelevanceTimeout), 5.0f);
    /// <summary>Duration of value tracking on server.</summary>
    public static readonly NetworkSetting ServerTracingDuration = new(nameof(ServerTracingDuration), 5.0f);

    /// <summary>Minimal time error that is not ignored.</summary>
    public static readonly NetworkSetting TimeErrorTolerance = new(nameof(TimeErrorTolerance), 0.002f);
    /// <summary>Limit of smooth time adjustment.</summary>
    public static readonly NetworkSetting TimeSnapThreshold = new(nameof(TimeSnapThreshold), 2.5f);
    /// <summary>Minimal time dilation factor.</summary>
    public static readonly NetworkSetting MinTimeDilation = new(nameof(MinTimeDilation), 0.7f);
    /// <summary>Maximal time dilation factor.</summary>
    public static readonly NetworkSetting MaxTimeDilation = new(nameof(MaxTimeDilation), 1.5f);
    /// <summary>Delay before delivered updates are used for interpolation.</summary>
    public static readonly NetworkSetting InterpolationDelay = new(nameof(InterpolationDelay), 0.1f);
    /// <summary>Duration of value tracking on client.</summary>
    public static readonly NetworkSetting ClientTracingDuration = new(nameof(ClientTracingDuration), 3.0f);
    /// <summary>Duration of value extrapolation.</summary>
    public static readonly NetworkSetting ExtrapolationLimit = new(nameof(ExtrapolationLimit), 0.5f);
}
