//
// Copyright (c) 2008-2022 the Urho3D project.
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

namespace RedBolt.Navigation;

using Math = System.Math;

using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Crowd;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

public enum CrowdAgentRequestedTarget
{
    None = 0,
    Position,
    Velocity
}

public enum CrowdAgentTargetState
{
    None = 0,
    Failed,
    Valid,
    Requesting,
    WaitingForQueue,
    WaitingForPath,
    Velocity
}

public enum CrowdAgentState
{
    Invalid = 0,
    Walking,
    OffMesh
}

public enum NavigationQuality
{
    Low = 0,
    Medium = 1,
    High = 2
}

public delegate void CrowdAgentVelocityCallback(
    CrowdAgent agent, float timeStep, ref Vector3f desiredVelocity, ref float desiredSpeed);
public delegate void CrowdAgentPositionCallback(CrowdAgent agent, float timeStep, ref Vector3f position);

/// <summary>Crowd agent component, requires a CrowdManager component in the scene. When not set explicitly, agent's radius and height are defaulted to navigation mesh's agent radius and height, respectively.</summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class CrowdAgent : Component
{
    public const float DefaultCollisionQueryRange = 5.0f;
    public const float DefaultSeparationWeight = 2.0f;
    public const uint DefaultCollisionLayer = 0;
    public const uint DefaultCollisionMask = 0xffffffff;

    private const int ScopeNavigationQualityParams = 1;
    private const int ScopeNavigationPushinessParams = 2;
    private const int ScopeBaseParams = ~ScopeNavigationQualityParams & ~ScopeNavigationPushinessParams;

    private WeakReference<CrowdManager>? _crowdManager;
    private CrowdAgentVelocityCallback? _velocityCallback;
    private CrowdAgentPositionCallback? _positionCallback;
    private int _agentCrowdId = -1;
    private Vector3f _targetPosition;
    private Vector3f _targetVelocity;
    private Vector3f _actualVelocity;
    private CrowdAgentRequestedTarget _requestedTargetType;
    private bool _updateNodePosition = true;

    private float _maxAccel;
    private float _maxSpeed;
    private float _radius;
    private float _height;
    private uint _queryFilterType;
    private uint _obstacleAvoidanceType;
    private NavigationQuality _navQuality = NavigationQuality.High;
    private float _collisionQueryRange = DefaultCollisionQueryRange;
    private float _separationWeight = DefaultSeparationWeight;
    private uint _collisionLayer = DefaultCollisionLayer;
    private uint _collisionMask = DefaultCollisionMask;

    private Vector3f _previousPosition;
    private CrowdAgentTargetState _previousTargetState;
    private CrowdAgentState _previousAgentState = CrowdAgentState.Walking;
    private bool _ignoreTransformChanges;

    /// <summary>Construct.</summary>
    public CrowdAgent()
    {
        SubscribeToEvent<NavigationEvents.NavigationTileAddedEvent>(HandleNavigationTileAdded);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        RemoveAgentFromCrowd();
        base.Dispose();
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        _maxAccel = MathF.Max(0.0f, _maxAccel);
        _maxSpeed = MathF.Max(0.0f, _maxSpeed);
        _radius = MathF.Max(0.0f, _radius);
        _height = MathF.Max(0.0f, _height);
        _queryFilterType = global::System.Math.Min(_queryFilterType, DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE - 1u);
        _obstacleAvoidanceType = global::System.Math.Min(_obstacleAvoidanceType, DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS - 1u);

        UpdateParameters();

        var requestedTargetType = _requestedTargetType;
        if (_requestedTargetType != CrowdAgentRequestedTarget.None)
        {
            _requestedTargetType = CrowdAgentRequestedTarget.None;
            if (requestedTargetType == CrowdAgentRequestedTarget.Position)
                TargetPosition = _targetPosition;
            else
                TargetVelocity = _targetVelocity;
        }
        else
        {
            _requestedTargetType = CrowdAgentRequestedTarget.Position;
            ResetTarget();
        }
    }

    /// <summary>Handle enabled/disabled state change.</summary>
    public override void OnSetEnabled()
    {
        if (IsEnabledEffective && !IsInCrowd)
            AddAgentToCrowd();
        else if (!IsEnabledEffective && IsInCrowd)
            RemoveAgentFromCrowd();
    }

    /// <summary>Draw debug geometry.</summary>
    public void DrawDebugGeometry(bool depthTest)
    {
        var scene = Scene;
        if (scene is not null)
            DrawDebugGeometry(scene.GetComponent<DebugRenderer>(), depthTest);
    }

    /// <summary>Draw debug feelers.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (_node is not null && debug is not null)
        {
            var pos = Position;
            var vel = ActualVelocity;
            var desiredVel = DesiredVelocity;
            var agentHeightVec = new Vector3f(0.0f, _height, 0.0f);

            debug.AddLine(pos + 0.5f * agentHeightVec, pos + vel + 0.5f * agentHeightVec, Color.Green, depthTest);
            debug.AddLine(pos + 0.25f * agentHeightVec, pos + desiredVel + 0.25f * agentHeightVec, Color.Red, depthTest);
            debug.AddCylinder(pos, _radius, _height, HasArrived ? Color.Green : Color.White, depthTest);
        }
    }

    /// <summary>Set velocity callback.</summary>
    public CrowdAgentVelocityCallback? VelocityCallback
    {
        get => _velocityCallback;
        set => _velocityCallback = value;
    }

    /// <summary>Set height callback.</summary>
    public CrowdAgentPositionCallback? PositionCallback
    {
        get => _positionCallback;
        set => _positionCallback = value;
    }

    /// <summary>Submit a new target position request for this agent.</summary>
    [Serialized("Target Position")]
    public Vector3f TargetPosition
    {
        get => _targetPosition;
        set
        {
            if (value != _targetPosition || _requestedTargetType != CrowdAgentRequestedTarget.Position)
            {
                _targetPosition = value;
                _requestedTargetType = CrowdAgentRequestedTarget.Position;

                if (!IsInCrowd)
                    AddAgentToCrowd();
                if (IsInCrowd && TryGetCrowdManager(out var crowdManager))
                {
                    var nearestPos = crowdManager.FindNearestPoint(value, checked((int)_queryFilterType), out var nearestRef);
                    crowdManager.Crowd!.RequestMoveTarget(GetDetourCrowdAgent()!, nearestRef, ToRecast(nearestPos));
                }
            }
        }
    }

    /// <summary>Submit a new target velocity request for this agent.</summary>
    [Serialized("Target Velocity")]
    public Vector3f TargetVelocity
    {
        get => _targetVelocity;
        set
        {
            if (value != _targetVelocity || _requestedTargetType != CrowdAgentRequestedTarget.Velocity)
            {
                _targetVelocity = value;
                _requestedTargetType = CrowdAgentRequestedTarget.Velocity;

                if (IsInCrowd && TryGetCrowdManager(out var crowdManager))
                    crowdManager.Crowd!.RequestMoveVelocity(GetDetourCrowdAgent()!, ToRecast(value));
            }
        }
    }

    /// <summary>Set actual immediate velocity. Target is not affected.</summary>
    [Serialized("Actual Velocity")]
    public Vector3f ActualVelocity
    {
        get => GetDetourCrowdAgent() is { } agent ? FromRecast(agent.vel) : Vector3f.Zero;
        set
        {
            _actualVelocity = value;
            if (GetEditableDetourCrowdAgent() is { } agent)
                agent.vel = ToRecast(value);
        }
    }

    /// <summary>Reset any target request for the specified agent. Note that the agent will continue to move into the current direction; set a zero target velocity to actually stop.</summary>
    public void ResetTarget()
    {
        if (_requestedTargetType != CrowdAgentRequestedTarget.None)
        {
            _requestedTargetType = CrowdAgentRequestedTarget.None;
            if (IsInCrowd && TryGetCrowdManager(out var crowdManager))
                crowdManager.Crowd!.ResetMoveTarget(GetDetourCrowdAgent()!);
        }
    }

    /// <summary>Update the node position. When set to false, the node position should be updated by other means (e.g. using Physics) in response to the CrowdAgentReposition event.</summary>
    [Serialized("Update Node Position")]
    public bool UpdateNodePosition
    {
        get => _updateNodePosition;
        set => _updateNodePosition = value;
    }

    /// <summary>Set the agent's max acceleration.</summary>
    [Serialized("Max Accel")]
    public float MaxAccel
    {
        get => _maxAccel;
        set
        {
            if (value != _maxAccel && value >= 0.0f)
            {
                _maxAccel = value;
                UpdateParameters(ScopeBaseParams);
            }
        }
    }

    /// <summary>Set the agent's max velocity.</summary>
    [Serialized("Max Speed")]
    public float MaxSpeed
    {
        get => _maxSpeed;
        set
        {
            if (value != _maxSpeed && value >= 0.0f)
            {
                _maxSpeed = value;
                UpdateParameters(ScopeBaseParams);
            }
        }
    }

    /// <summary>Set the agent's radius.</summary>
    [Serialized("Radius")]
    public float Radius
    {
        get => _radius;
        set
        {
            if (value != _radius && value > 0.0f)
            {
                _radius = value;
                UpdateParameters(ScopeBaseParams | ScopeNavigationPushinessParams);
            }
        }
    }

    /// <summary>Set the agent's height.</summary>
    [Serialized("Height")]
    public float Height
    {
        get => _height;
        set
        {
            if (value != _height && value > 0.0f)
            {
                _height = value;
                UpdateParameters(ScopeBaseParams);
            }
        }
    }

    /// <summary>Set the agent's query filter type.</summary>
    [Serialized("Query Filter Type")]
    public uint QueryFilterType
    {
        get => _queryFilterType;
        set
        {
            if (value == _queryFilterType)
                return;
            if (value >= DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE)
            {
                Log.GetLogger().Error("The specified filter type index ({FilterType}) exceeds the maximum allowed value ({Maximum})",
                    value, DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE);
                return;
            }
            _queryFilterType = value;
            UpdateParameters(ScopeBaseParams);
        }
    }

    /// <summary>Set the agent's obstacle avoidance type.</summary>
    [Serialized("Obstacle Avoidance Type")]
    public uint ObstacleAvoidanceType
    {
        get => _obstacleAvoidanceType;
        set
        {
            if (value == _obstacleAvoidanceType)
                return;
            if (value >= DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS)
            {
                Log.GetLogger().Error("The specified obstacle avoidance type index ({AvoidanceType}) exceeds the maximum allowed value ({Maximum})",
                    value, DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS);
                return;
            }
            _obstacleAvoidanceType = value;
            UpdateParameters(ScopeBaseParams);
        }
    }

    /// <summary>Set the agent's navigation quality.</summary>
    [Serialized("Navigation Quality")]
    public NavigationQuality NavigationQuality
    {
        get => _navQuality;
        set
        {
            if (value != _navQuality)
            {
                _navQuality = value;
                UpdateParameters(ScopeNavigationQualityParams);
            }
        }
    }

    [Serialized("Collision Query Range")]
    public float CollisionQueryRange
    {
        get => _collisionQueryRange;
        set
        {
            if (_collisionQueryRange != value)
            {
                _collisionQueryRange = value;
                UpdateParameters(ScopeNavigationPushinessParams);
            }
        }
    }

    [Serialized("Separation Weight")]
    public float SeparationWeight
    {
        get => _separationWeight;
        set
        {
            if (_separationWeight != value)
            {
                _separationWeight = value;
                UpdateParameters(ScopeNavigationPushinessParams);
            }
        }
    }

    [Serialized("Collision Layer")]
    public uint CollisionLayer
    {
        get => _collisionLayer;
        set
        {
            if (_collisionLayer != value)
            {
                _collisionLayer = value;
                UpdateParameters(ScopeBaseParams);
            }
        }
    }

    [Serialized("Collision Mask")]
    public uint CollisionMask
    {
        get => _collisionMask;
        set
        {
            if (_collisionMask != value)
            {
                _collisionMask = value;
                UpdateParameters(ScopeBaseParams);
            }
        }
    }

    /// <summary>Return the agent's position.</summary>
    public Vector3f Position => GetDetourCrowdAgent() is { } agent ? FromRecast(agent.npos) : Node.GetWorldPosition();

    /// <summary>Return the agent's desired velocity.</summary>
    public Vector3f DesiredVelocity => GetDetourCrowdAgent() is { } agent ? FromRecast(agent.dvel) : Vector3f.Zero;

    /// <summary>Return the agent's requested target type, if any.</summary>
    [Serialized("Requested Target Type")]
    public CrowdAgentRequestedTarget RequestedTargetType
    {
        get => _requestedTargetType;
        set => _requestedTargetType = value;
    }

    /// <summary>Return the agent's state.</summary>
    public CrowdAgentState AgentState => GetDetourCrowdAgent() is { } agent ? (CrowdAgentState)agent.state : CrowdAgentState.Invalid;

    /// <summary>Return the agent's target state.</summary>
    public CrowdAgentTargetState TargetState => GetDetourCrowdAgent() is { } agent ? (CrowdAgentTargetState)agent.targetState : CrowdAgentTargetState.None;

    /// <summary>Return the agent id.</summary>
    public int AgentCrowdId => _agentCrowdId;

    /// <summary>Return true when the agent has a target.</summary>
    public bool HasRequestedTarget => _requestedTargetType != CrowdAgentRequestedTarget.None;

    /// <summary>Return true when the agent has arrived at its target.</summary>
    public bool HasArrived
    {
        get
        {
            var agent = GetDetourCrowdAgent();
            if (agent is null)
                return false;
            if (agent.ncorners == 0)
                return true;
            var corner = agent.corners[agent.ncorners - 1];
            var dx = agent.npos.X - corner.pos.X;
            var dz = agent.npos.Z - corner.pos.Z;
            return (corner.flags & DtStraightPathFlags.DT_STRAIGHTPATH_END) != 0
                && MathF.Sqrt(dx * dx + dz * dz) <= agent.option.radius;
        }
    }

    /// <summary>Return true when the agent is in crowd (being managed by a crowd manager).</summary>
    public bool IsInCrowd => TryGetCrowdManager(out _) && _agentCrowdId != -1;

    /// <summary>Update world origin.</summary>
    public void UpdateWorldOrigin(Vector3f delta)
    {
        if (_requestedTargetType == CrowdAgentRequestedTarget.Position)
            TargetPosition = _targetPosition - delta;
    }

    internal void OnCrowdVelocityUpdate(DtCrowdAgent agent, float timeStep)
    {
        if (_node is not null && TryGetCrowdManager(out var crowdManager))
        {
            var desiredVelocity = FromRecast(agent.dvel);
            var desiredSpeed = agent.desiredSpeed;
            crowdManager.UpdateAgentVelocity(this, timeStep, ref desiredVelocity, ref desiredSpeed);
            agent.dvel = ToRecast(desiredVelocity);
            agent.desiredSpeed = desiredSpeed;
        }
    }

    internal void OnCrowdPositionUpdate(DtCrowdAgent agent, float timeStep)
    {
        if (_node is null || !TryGetCrowdManager(out var crowdManager))
            return;

        var newPos = FromRecast(agent.npos);
        _actualVelocity = FromRecast(agent.vel);

        _ignoreTransformChanges = true;
        crowdManager.UpdateAgentPosition(this, timeStep, ref newPos);
        _ignoreTransformChanges = false;

        if (newPos != _previousPosition)
        {
            _previousPosition = newPos;
            if (_updateNodePosition)
            {
                _ignoreTransformChanges = true;
                _node.SetWorldPosition(newPos);
                _ignoreTransformChanges = false;
            }

            var eventData = new NavigationEvents.CrowdAgentRepositionEvent
            {
                Node = _node,
                CrowdAgent = this,
                Position = newPos,
                Velocity = _actualVelocity,
                Arrived = HasArrived,
                TimeStep = timeStep
            };
            crowdManager.SendEvent(ref eventData);
            if (_node is null)
                return;
            var nodeEventData = new NavigationEvents.CrowdAgentNodeRepositionEvent
            {
                Node = _node,
                CrowdAgent = this,
                Position = newPos,
                Velocity = _actualVelocity,
                Arrived = HasArrived,
                TimeStep = timeStep
            };
            _node.SendEvent(ref nodeEventData);
            if (_node is null)
                return;
        }

        var newTargetState = TargetState;
        var newAgentState = AgentState;
        if (newAgentState != _previousAgentState || newTargetState != _previousTargetState)
        {
            var eventData = new NavigationEvents.CrowdAgentStateChangedEvent
            {
                Node = _node,
                CrowdAgent = this,
                CrowdTargetState = newTargetState,
                CrowdAgentState = newAgentState,
                Position = newPos,
                Velocity = _actualVelocity
            };
            crowdManager.SendEvent(ref eventData);
            if (_node is null)
                return;
            var nodeEventData = new NavigationEvents.CrowdAgentNodeStateChangedEvent
            {
                Node = _node,
                CrowdAgent = this,
                CrowdTargetState = newTargetState,
                CrowdAgentState = newAgentState,
                Position = newPos,
                Velocity = _actualVelocity
            };
            _node.SendEvent(ref nodeEventData);
            if (_node is null)
                return;

            if (newAgentState == CrowdAgentState.Invalid || newTargetState == CrowdAgentTargetState.Failed)
                SendFailure(newPos);

            _previousAgentState = AgentState;
            _previousTargetState = TargetState;
        }
    }

    /// <summary>Handle node being assigned.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (_node is not null)
            _node.AddListener(this);
    }

    /// <summary>Handle scene being assigned.</summary>
    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is not null)
        {
            if (ReferenceEquals(scene, _node))
                Log.GetLogger().Error("{TypeName} should not be created to the root scene node", GetTypeName());
            var crowdManager = scene.GetOrCreateComponent<CrowdManager>();
            _crowdManager = new WeakReference<CrowdManager>(crowdManager);
            AddAgentToCrowd();
        }
        else
            RemoveAgentFromCrowd();
    }

    /// <summary>Handle node transform being dirtied.</summary>
    protected override void OnMarkedDirty(Node node)
    {
        if (!_ignoreTransformChanges && IsEnabledEffective && GetEditableDetourCrowdAgent() is { } agent)
        {
            var nodePos = node.GetWorldPosition();
            if (nodePos != FromRecast(agent.npos))
            {
                agent.npos = ToRecast(nodePos);
                if (agent.state != DtCrowdAgentState.DT_CROWDAGENT_STATE_WALKING)
                    agent.state = DtCrowdAgentState.DT_CROWDAGENT_STATE_WALKING;
            }
        }
    }

    internal DtCrowdAgent? GetDetourCrowdAgent()
    {
        return IsInCrowd && TryGetCrowdManager(out var crowdManager) ? crowdManager.GetDetourCrowdAgent(_agentCrowdId) : null;
    }

    internal DtCrowdAgent? GetEditableDetourCrowdAgent()
    {
        return IsInCrowd && TryGetCrowdManager(out var crowdManager) ? crowdManager.GetEditableDetourCrowdAgent(_agentCrowdId) : null;
    }

    internal int AddAgentToCrowd(bool force = false)
    {
        if (_node is null || !TryGetCrowdManager(out var crowdManager) || crowdManager.Crowd is null)
            return -1;

        if (force || !IsInCrowd)
        {
            _agentCrowdId = crowdManager.AddAgent(this, _node.GetWorldPosition());
            if (_agentCrowdId == -1)
                return -1;

            ApplyAttributes();
            _previousAgentState = AgentState;
            _previousTargetState = TargetState;
            if (_previousAgentState == CrowdAgentState.Invalid)
            {
                SendFailure(Position);
                _previousAgentState = AgentState;
                _previousTargetState = TargetState;
            }
            _previousPosition = Position;
        }
        return _agentCrowdId;
    }

    internal void RemoveAgentFromCrowd()
    {
        if (IsInCrowd && TryGetCrowdManager(out var crowdManager))
        {
            crowdManager.RemoveAgent(this);
            _agentCrowdId = -1;
        }
    }

    private void UpdateParameters(int scope = -1)
    {
        var agent = GetEditableDetourCrowdAgent();
        if (agent is null || !TryGetCrowdManager(out var crowdManager))
            return;

        agent.vel = ToRecast(_actualVelocity);
        var parameters = agent.option;
        if ((scope & ScopeNavigationQualityParams) != 0)
        {
            parameters.updateFlags = _navQuality switch
            {
                NavigationQuality.Low => DtCrowdAgentUpdateFlags.DT_CROWD_OPTIMIZE_VIS
                    | DtCrowdAgentUpdateFlags.DT_CROWD_ANTICIPATE_TURNS,
                NavigationQuality.Medium => DtCrowdAgentUpdateFlags.DT_CROWD_OPTIMIZE_TOPO
                    | DtCrowdAgentUpdateFlags.DT_CROWD_OPTIMIZE_VIS
                    | DtCrowdAgentUpdateFlags.DT_CROWD_ANTICIPATE_TURNS
                    | DtCrowdAgentUpdateFlags.DT_CROWD_SEPARATION,
                _ => DtCrowdAgentUpdateFlags.DT_CROWD_OPTIMIZE_TOPO
                    | DtCrowdAgentUpdateFlags.DT_CROWD_OPTIMIZE_VIS
                    | DtCrowdAgentUpdateFlags.DT_CROWD_ANTICIPATE_TURNS
                    | DtCrowdAgentUpdateFlags.DT_CROWD_SEPARATION
                    | DtCrowdAgentUpdateFlags.DT_CROWD_OBSTACLE_AVOIDANCE
            };
        }
        if ((scope & ScopeNavigationPushinessParams) != 0)
        {
            parameters.separationWeight = _separationWeight;
            parameters.collisionQueryRange = _collisionQueryRange;
        }
        if ((scope & ScopeBaseParams) != 0)
        {
            parameters.radius = _radius;
            parameters.height = _height;
            parameters.maxAcceleration = _maxAccel;
            parameters.maxSpeed = _maxSpeed;
            parameters.pathOptimizationRange = _radius * 30.0f;
            parameters.queryFilterType = checked((int)_queryFilterType);
            parameters.obstacleAvoidanceType = checked((int)_obstacleAvoidanceType);
            // TODO: port
            // params.layer = collisionLayer_;
            // params.mask = collisionMask_;
        }
        crowdManager.Crowd!.UpdateAgentParameters(agent, parameters);
    }

    private void SendFailure(Vector3f position)
    {
        if (_node is null || !TryGetCrowdManager(out var crowdManager))
            return;
        var eventData = new NavigationEvents.CrowdAgentFailureEvent
        {
            Node = _node,
            CrowdAgent = this,
            CrowdTargetState = TargetState,
            CrowdAgentState = AgentState,
            Position = position,
            Velocity = ActualVelocity
        };
        crowdManager.SendEvent(ref eventData);
        if (_node is null)
            return;
        var nodeEventData = new NavigationEvents.CrowdAgentNodeFailureEvent
        {
            Node = _node,
            CrowdAgent = this,
            CrowdTargetState = TargetState,
            CrowdAgentState = AgentState,
            Position = position,
            Velocity = ActualVelocity
        };
        _node.SendEvent(ref nodeEventData);
    }

    private void HandleNavigationTileAdded(ref NavigationEvents.NavigationTileAddedEvent eventData)
    {
        if (!TryGetCrowdManager(out var crowdManager) || !ReferenceEquals(crowdManager.NavigationMesh, eventData.Mesh))
            return;
        var agentTile = eventData.Mesh.GetTileIndex(Node.GetWorldPosition());
        if (eventData.Tile == agentTile && IsInCrowd)
        {
            RemoveAgentFromCrowd();
            AddAgentToCrowd();
        }
    }
    private bool TryGetCrowdManager(out CrowdManager crowdManager)
    {
        if (_crowdManager is not null && _crowdManager.TryGetTarget(out crowdManager!))
            return true;
        crowdManager = null!;
        return false;
    }


    private static RcVec3f ToRecast(Vector3f value) => new(value.X, value.Y, value.Z);
    private static Vector3f FromRecast(RcVec3f value) => new(value.X, value.Y, value.Z);
}
