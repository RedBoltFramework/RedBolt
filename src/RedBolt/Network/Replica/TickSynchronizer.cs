// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Replica;

using RedBolt.Core;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

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

public readonly record struct SceneUpdateSynchronizerParams(
    bool IsServer, uint NetworkFrequency, bool AllowZeroUpdatesOnServer = false);

/// <summary>Synchronizes Scene updates with the network clock.</summary>
public sealed class SceneUpdateSynchronizer : RedObject
{
    public SceneUpdateSynchronizer(Scene scene, SceneUpdateSynchronizerParams parameters)
    {
        _parameters = parameters;
        _scene = scene;
        _synchronizer = new TickSynchronizer(parameters.NetworkFrequency, parameters.IsServer);
        _synchronizer.SetFollowerFrequency(parameters.NetworkFrequency);

        _physicsWorld = scene.GetComponent<PhysicsWorld>();

        if (parameters.IsServer)
        {
            scene.SetManualUpdate(true);
            SubscribeToEvent<CoreEvents.UpdateEvent>(UpdateSceneOnServer);
        }

        if (_physicsWorld is not null)
        {
            _physicsWorld.ManualUpdate = true;
            _wasInterpolated = _physicsWorld.Interpolation;
            if (parameters.IsServer)
                _physicsWorld.Interpolation = false;
            SubscribeToEvent<SceneEvents.SceneSubsystemUpdateEvent>(scene, UpdatePhysics);
        }
    }

    public void Synchronize(NetworkFrame networkFrame, float overtime)
    {
        UpdateFollowerFrequency();
        var synchronizedTick = _synchronizer.Synchronize(overtime);
        _synchronizedNetworkFrame = new NetworkFrameSync
        {
            NetworkFrame = networkFrame,
            Offset = synchronizedTick
        };
    }

    public void Update(float timeStep)
    {
        UpdateFollowerFrequency();
        _synchronizer.Update(timeStep);
    }

    public override void Dispose()
    {
        _scene.SetManualUpdate(false);
        if (_physicsWorld is not null)
        {
            _physicsWorld.ManualUpdate = false;
            _physicsWorld.Interpolation = _wasInterpolated;
        }
        base.Dispose();
    }

    private void UpdateFollowerFrequency()
    {
        if (_physicsWorld is not null)
            _synchronizer.SetFollowerFrequency(checked((uint)_physicsWorld.Fps));
    }

    private void UpdatePhysics(ref SceneEvents.SceneSubsystemUpdateEvent eventData)
    {
        if (_physicsWorld is null)
            return;
        var fixedTimeStep = 1.0f / _synchronizer.FollowerFrequency;
        var overtime = _physicsWorld.Interpolation ? _synchronizer.FollowerAccumulatedTime : 0.0f;
        _physicsWorld.CustomUpdate(
            _synchronizer.PendingFollowerTicks,
            fixedTimeStep,
            overtime,
            _synchronizedNetworkFrame is { } synchronized ? checked((long)synchronized.NetworkFrame.Value) : null,
            _synchronizedNetworkFrame?.Offset ?? 0);
        _synchronizedNetworkFrame = null;
    }

    private void UpdateSceneOnServer(ref CoreEvents.UpdateEvent eventData)
    {
        var fixedTimeStep = 1.0f / _synchronizer.FollowerFrequency;
        var numSteps = _synchronizer.PendingFollowerTicks;
        if (numSteps != 0 || _parameters.AllowZeroUpdatesOnServer)
            _scene.Update(numSteps * fixedTimeStep);
    }

    private readonly SceneUpdateSynchronizerParams _parameters;
    private readonly Scene _scene;
    private readonly TickSynchronizer _synchronizer;
    private readonly PhysicsWorld? _physicsWorld;
    private readonly bool _wasInterpolated;
    private NetworkFrameSync? _synchronizedNetworkFrame;
}
