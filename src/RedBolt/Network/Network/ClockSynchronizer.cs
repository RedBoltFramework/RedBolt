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

namespace RedBolt.Network;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;

/// <summary>Utility class to filter random uint value which mostly stays the same.</summary>
public class FilteredUint
{
    public FilteredUint(uint bufferSize, float maxDeviation = 1.0f)
    {
        _bufferSize = bufferSize;
        _maxDeviation = maxDeviation;
    }

    public void AddValue(uint value, bool filter = true)
    {
        if (_offsets.Count == 0)
            _baseValue = value;

        var offset = unchecked((int)(value - _baseValue));
        _offsets.Enqueue(offset);
        if (_offsets.Count > _bufferSize)
            _offsets.Dequeue();

        if (filter)
            Filter();
    }

    public void Filter()
    {
        if (_offsets.Count < 2)
        {
            var offset = _offsets.Count == 0 ? 0 : _offsets.Peek();
            _averageOffset = offset;
            _minOffset = offset;
            _maxOffset = offset;
            _stabilizedMaxAverageOffset = offset;
            return;
        }

        var meanOffset = _offsets.Sum(value => (double)value) / _offsets.Count;

        var varianceAccum = 0.0;
        foreach (var offset in _offsets)
            varianceAccum += (offset - meanOffset) * (offset - meanOffset);

        var deviation = System.Math.Sqrt(varianceAccum) / (_offsets.Count - 1);

        var averageAccum = 0.0;
        var averageCount = 0u;
        _minOffset = int.MaxValue;
        _maxOffset = int.MinValue;
        foreach (var offset in _offsets)
        {
            if (offset - meanOffset <= deviation * _maxDeviation)
            {
                averageAccum += offset;
                ++averageCount;

                _minOffset = System.Math.Min(_minOffset, offset);
                _maxOffset = System.Math.Max(_maxOffset, offset);
            }
        }

        System.Diagnostics.Debug.Assert(averageCount > 0);
        _averageOffset = (int)(averageAccum / averageCount);

        if (_stabilizedMaxAverageOffset < _averageOffset)
            _stabilizedMaxAverageOffset = _averageOffset;
        else if (_stabilizedMaxAverageOffset > _maxOffset)
            _stabilizedMaxAverageOffset = _maxOffset;
    }

    public uint MinValue => unchecked(_baseValue + (uint)_minOffset);
    public uint AverageValue => unchecked(_baseValue + (uint)_averageOffset);
    public uint MaxValue => unchecked(_baseValue + (uint)_maxOffset);
    public uint StabilizedAverageMaxValue => unchecked(_baseValue + (uint)_stabilizedMaxAverageOffset);
    public bool IsInitialized => _offsets.Count != 0;

    private readonly uint _bufferSize;
    private readonly float _maxDeviation;

    private uint _baseValue;
    private readonly Queue<int> _offsets = new();

    private int _minOffset;
    private int _averageOffset;
    private int _maxOffset;
    private int _stabilizedMaxAverageOffset;
}

public enum ClockSynchronizerPhase : uint
{
    /// <summary>Phase 1, server to client. Payload is relative to the server.</summary>
    Ping = 1,
    /// <summary>Phase 2, client to server. Payload is relative to the server.</summary>
    Pong = 2
}

/// <summary>Message exchanged between client and server to synchronize time.</summary>
public struct ClockSynchronizerMessage
{
    public void Load(Deserializer source)
    {
        Magic = source.ReadUInt();
        Phase = (ClockSynchronizerPhase)source.ReadVLE();
        LocalSent = source.ReadUInt();
        RemoteReceived = source.ReadUInt();
        RemoteSent = source.ReadUInt();
    }

    public readonly void Save(Serializer destination)
    {
        destination.WriteUInt(Magic);
        destination.WriteVLE((uint)Phase);
        destination.WriteUInt(LocalSent);
        destination.WriteUInt(RemoteReceived);
        destination.WriteUInt(RemoteSent);
    }

    public override readonly string ToString() => $"{{magic={Magic}, phase={(uint)Phase}, ...}}";

    public uint Magic;
    public ClockSynchronizerPhase Phase;
    public uint LocalSent;
    public uint RemoteReceived;
    public uint RemoteSent;
}

public class ClockSynchronizer
{
    public ClockSynchronizer(uint pingIntervalMs, uint maxPingMs, uint clockBufferSize,
        uint pingBufferSize, Func<uint>? getTimestamp = null)
    {
        _getTimestamp = getTimestamp;
        _pingIntervalMs = pingIntervalMs;
        _maxPingMs = maxPingMs;
        _localToRemote = new FilteredUint(clockBufferSize);
        _roundTripDelay = new FilteredUint(pingBufferSize);
    }

    /// <summary>Process incoming message. Should be called as soon as possible.</summary>
    public void ProcessMessage(ClockSynchronizerMessage message)
    {
        if (message.Phase == ClockSynchronizerPhase.Ping)
        {
            var pong = new PendingPong
            {
                Magic = message.Magic,
                ServerSentTime = message.RemoteSent,
                ClientReceivedTime = GetTimestamp()
            };
            _pendingPongs.Add(pong);
        }
        else if (message.Phase == ClockSynchronizerPhase.Pong)
        {
            var pingIndex = _pendingPings.FindIndex(ping => ping.Magic == message.Magic);
            if (pingIndex == -1)
            {
                Logging.Log.GetLogger().Warning("Expired or invalid clock message was received");
                return;
            }

            var now = GetTimestamp();
            _latestRoundtripTimestamp = now;
            UpdateClocks(_pendingPings[pingIndex].ServerSentTime,
                message.RemoteReceived, message.RemoteSent, now);
        }
    }

    /// <summary>Receive outgoing message if there is any. Should be called as late as possible.</summary>
    public ClockSynchronizerMessage? PollMessage()
    {
        var now = GetTimestamp();
        if (!_latestProbeTimestamp.HasValue
            || unchecked(now - _latestProbeTimestamp.Value) >= _pingIntervalMs)
        {
            _latestProbeTimestamp = now;
            CleanupExpiredPings(now);
            return CreateNewPing(now);
        }

        if (_pendingPongs.Count != 0)
        {
            var pong = _pendingPongs[^1];
            _pendingPongs.RemoveAt(_pendingPongs.Count - 1);
            return CreateNewPong(pong);
        }

        return null;
    }

    /// <summary>Return whether synchronizer is ready to use.</summary>
    public bool IsReady => _localToRemote.IsInitialized;

    /// <summary>Convert from local to remote timestamp.</summary>
    public uint LocalToRemote(uint value) => unchecked(value + _localToRemote.AverageValue);

    /// <summary>Convert from remote to local timestamp.</summary>
    public uint RemoteToLocal(uint value) => unchecked(value - _localToRemote.AverageValue);

    /// <summary>Return ping, i.e. half of round-trip delay excluding remote processing time.</summary>
    public uint Ping => _roundTripDelay.AverageValue / 2;

    /// <summary>Return local timestamp of the latest successful roundtrip.</summary>
    public uint LocalTimeOfLatestRoundtrip => _latestRoundtripTimestamp ?? 0;

    private ClockSynchronizerMessage CreateNewPing(uint now)
    {
        var ping = new PendingPing
        {
            Magic = RandomEngine.GetDefaultEngine().GetUInt(),
            ServerSentTime = now
        };
        _pendingPings.Add(ping);

        return new ClockSynchronizerMessage
        {
            Magic = ping.Magic,
            Phase = ClockSynchronizerPhase.Ping,
            RemoteSent = now
        };
    }

    private ClockSynchronizerMessage CreateNewPong(PendingPong pong)
    {
        return new ClockSynchronizerMessage
        {
            Magic = pong.Magic,
            Phase = ClockSynchronizerPhase.Pong,
            LocalSent = pong.ServerSentTime,
            RemoteReceived = pong.ClientReceivedTime,
            RemoteSent = GetTimestamp()
        };
    }

    private void CleanupExpiredPings(uint now)
    {
        if ((uint)_pendingPings.Count < unchecked(2u * _maxPingMs) / _pingIntervalMs)
            return;

        _pendingPings.RemoveAll(ping => unchecked(now - ping.ServerSentTime) >= _maxPingMs);
    }

    private uint GetTimestamp() => _getTimestamp?.Invoke() ?? Time.GetSystemTime();

    private void UpdateClocks(uint localSent, uint remoteReceived, uint remoteSent, uint localReceived)
    {
        var offset1 = unchecked(remoteReceived - localSent);
        var offset2 = unchecked(remoteSent - localReceived);
        var delta = unchecked((int)(offset2 - offset1));
        var offset = unchecked(offset1 + (uint)(delta / 2));
        _localToRemote.AddValue(offset);

        var outerDelay = unchecked(localReceived - localSent);
        var innerDelay = unchecked(remoteSent - remoteReceived);
        var delay = unchecked(outerDelay - System.Math.Min(outerDelay, innerDelay));
        _roundTripDelay.AddValue(delay);
    }

    private struct PendingPing
    {
        public uint Magic;
        public uint ServerSentTime;
    }

    private struct PendingPong
    {
        public uint Magic;
        public uint ServerSentTime;
        public uint ClientReceivedTime;
    }

    private readonly Func<uint>? _getTimestamp;
    private readonly uint _pingIntervalMs;
    private readonly uint _maxPingMs;

    private uint? _latestProbeTimestamp;
    private readonly List<PendingPing> _pendingPings = [];
    private readonly List<PendingPong> _pendingPongs = [];

    private readonly FilteredUint _localToRemote;
    private readonly FilteredUint _roundTripDelay;
    private uint? _latestRoundtripTimestamp;
}
