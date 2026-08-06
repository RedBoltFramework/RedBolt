// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

/// <summary>
/// Helper class that synchronizes two fixed-timestep clocks.
/// Leader and follower ticks are synchronized when their beginnings correspond to the same moment in time.
/// </summary>
/// <remarks>
/// Leader clock should not tick faster than follower clock. Leader clock should be reset on each tick.
/// Follower clock will never tick ahead of the leader clock.
/// </remarks>
public class TickSynchronizer
{
    public TickSynchronizer(uint leaderFrequency, bool isServer)
    {
        _leaderFrequency = leaderFrequency;
        _isServer = isServer;
        _followerFrequency = leaderFrequency;
    }

    public void SetFollowerFrequency(uint followerFrequency)
    {
        var maxFollowerTicks = System.Math.Max(1u, followerFrequency / _leaderFrequency);
        _followerFrequency = _leaderFrequency * maxFollowerTicks;

        if (_followerFrequency != followerFrequency)
        {
            Logging.Log.GetLogger().Warning(
                "Cannot synchronize follower clock at {} FPS with leading clock at {} FPS. {} FPS is used.",
                followerFrequency, _leaderFrequency, _followerFrequency);
        }
    }

    public uint FollowerFrequency => _followerFrequency;

    /// <summary>Synchronize with a tick of the leader clock.</summary>
    /// <returns>Number of follower ticks before the clocks are synchronized.</returns>
    public uint Synchronize(float overtime)
    {
        var maxFollowerTicks = _followerFrequency / _leaderFrequency;
        if (_isServer)
        {
            _numPendingFollowerTicks = maxFollowerTicks;
            _numFollowerTicks = maxFollowerTicks;
            _timeAccumulator = 0.0f;
            return 0;
        }

        var followerTicksDebt = _numFollowerTicks != 0 ? maxFollowerTicks - _numFollowerTicks : 0;

        _numPendingFollowerTicks = followerTicksDebt + 1;
        _numFollowerTicks = 1;
        _timeAccumulator = overtime;
        NormalizeOnClient();

        return followerTicksDebt;
    }

    /// <summary>Update follower clock within one tick of leader clock.</summary>
    public void Update(float timeStep)
    {
        _numPendingFollowerTicks = 0;
        if (!_isServer)
        {
            _timeAccumulator += timeStep;
            NormalizeOnClient();
        }
    }

    /// <summary>Number of follower ticks expected during current engine update.</summary>
    public uint PendingFollowerTicks => _numPendingFollowerTicks;

    /// <summary>Amount of time elapsed after latest follower tick.</summary>
    public float FollowerAccumulatedTime => _timeAccumulator;

    private void NormalizeOnClient()
    {
        var fixedTimeStep = 1.0f / _followerFrequency;
        while (_timeAccumulator >= fixedTimeStep)
        {
            _timeAccumulator -= fixedTimeStep;
            ++_numPendingFollowerTicks;
            ++_numFollowerTicks;
        }

        var maxFollowerTicks = _followerFrequency / _leaderFrequency;
        if (_numFollowerTicks > maxFollowerTicks)
        {
            var extraFollowerTicks = _numFollowerTicks - maxFollowerTicks;
            _numPendingFollowerTicks -= extraFollowerTicks;
            _numFollowerTicks -= extraFollowerTicks;
        }
    }

    private readonly uint _leaderFrequency;
    private readonly bool _isServer;
    private uint _followerFrequency;

    private float _timeAccumulator;
    private uint _numFollowerTicks;
    private uint _numPendingFollowerTicks;
}
