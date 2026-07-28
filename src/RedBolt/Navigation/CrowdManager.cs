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

using Math = System.Math;

using Microsoft.Extensions.Logging;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Crowd;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = RedBolt.Scene.Scene;

namespace RedBolt.Navigation;

/// <summary>Parameter structure for obstacle avoidance params.</summary>
public struct CrowdObstacleAvoidanceParams
{
    public float VelBias;
    public float WeightDesVel;
    public float WeightCurVel;
    public float WeightSide;
    public float WeightToi;
    public float HorizTime;
    public byte GridSize;
    public byte AdaptiveDivs;
    public byte AdaptiveRings;
    public byte AdaptiveDepth;
}

/// <summary>Crowd manager scene component. Should be added only to the root scene node.</summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class CrowdManager : Component
{
    private const uint DefaultMaxAgents = 512;
    private const float DefaultMaxAgentRadius = 0.0f;

    private DtCrowd? _crowd;
    private CrowdAgentVelocityCallback? _velocityCallback;
    private CrowdAgentPositionCallback? _positionCallback;
    private WeakReference<NavigationMesh>? _navigationMesh;
    [Serialized("Navigation Mesh", Mode = AttributeMode.Default | AttributeMode.ComponentId)]
    private uint _navigationMeshId;
    private uint _maxAgents = DefaultMaxAgents;
    private float _maxAgentRadius = DefaultMaxAgentRadius;
    private uint _numQueryFilterTypes;
    private readonly uint[] _numAreas = new uint[DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE];
    private uint _numObstacleAvoidanceTypes;

    /// <summary>Construct.</summary>
    public CrowdManager()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _crowd = null;
        base.Dispose();
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        _maxAgents = global::System.Math.Max(1u, _maxAgents);
        _maxAgentRadius = MathF.Max(0.0f, _maxAgentRadius);

        var scene = Scene;
        if (scene is not null && _navigationMeshId != 0)
        {
            var navMesh = scene.GetComponent(_navigationMeshId) as NavigationMesh;
            if (navMesh is not null && !ReferenceEquals(navMesh, NavigationMesh))
            {
                NavigationMesh = navMesh;
                return;
            }
        }
        _navigationMeshId = NavigationMesh?.Id ?? 0;

        if (_crowd is null || _crowd.Config().maxAgentRadius != _maxAgentRadius)
            CreateCrowd();
    }

    /// <summary>Draw the agents' pathing debug data.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || _crowd is null)
            return;

        foreach (var agent in _crowd.GetActiveAgents())
        {
            if (agent.option.userData is not CrowdAgent crowdAgent)
                continue;

            crowdAgent.DrawDebugGeometry(debug, depthTest);
            if (crowdAgent.TargetState is CrowdAgentTargetState.None or CrowdAgentTargetState.Velocity)
                continue;

            var color = new Color(0.6f, 0.2f, 0.2f, 1.0f);
            var pos1 = FromRecast(agent.npos);
            for (var i = 0; i < agent.ncorners; ++i)
            {
                var pos2 = FromRecast(agent.corners[i].pos);
                debug.AddLine(pos1, pos2, color, depthTest);
                pos1 = pos2;
            }
            var target = FromRecast(agent.targetPos);
            debug.AddLine(pos1, target, color, depthTest);
            debug.AddSphere(new Sphere(target, 0.5f), color, depthTest);
        }
    }

    /// <summary>Add debug geometry to the debug renderer.</summary>
    public void DrawDebugGeometry(bool depthTest)
    {
        var scene = Scene;
        if (scene is not null)
            DrawDebugGeometry(scene.GetComponent<DebugRenderer>(), depthTest);
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

    /// <summary>Update agent velocity using velocity callback.</summary>
    public void UpdateAgentVelocity(CrowdAgent agent, float timeStep, ref Vector3f desiredVelocity, ref float desiredSpeed)
    {
        var callback = agent.VelocityCallback ?? _velocityCallback;
        callback?.Invoke(agent, timeStep, ref desiredVelocity, ref desiredSpeed);
    }

    /// <summary>Update agent Y position using height callback.</summary>
    public void UpdateAgentPosition(CrowdAgent agent, float timeStep, ref Vector3f position)
    {
        var callback = agent.PositionCallback ?? _positionCallback;
        callback?.Invoke(agent, timeStep, ref position);
    }

    /// <summary>Set the crowd target position. The target position is set to all crowd agents found in the specified node. Defaulted to scene node.</summary>
    public void SetCrowdTarget(Vector3f position, Node? node = null)
    {
        if (_crowd is null)
            return;

        var agents = GetAgents(node, false);
        var moveTarget = position;
        for (var i = 0; i < agents.Count; ++i)
        {
            var agent = agents[i];
            var eventData = new NavigationEvents.CrowdAgentFormationEvent
            {
                Node = agent.Node,
                CrowdAgent = agent,
                Index = i,
                Size = agents.Count,
                Position = moveTarget
            };
            SendEvent(ref eventData);
            moveTarget = eventData.Position;
            agent.TargetPosition = moveTarget;
        }
    }

    /// <summary>Set the crowd move velocity. The move velocity is applied to all crowd agents found in the specified node. Defaulted to scene node.</summary>
    public void SetCrowdVelocity(Vector3f velocity, Node? node = null)
    {
        if (_crowd is null)
            return;
        var agents = GetAgents(node, true);
        for (var i = 0; i < agents.Count; ++i)
            agents[i].TargetVelocity = velocity;
    }

    /// <summary>Reset any crowd target for all crowd agents found in the specified node. Defaulted to scene node.</summary>
    public void ResetCrowdTarget(Node? node = null)
    {
        if (_crowd is null)
            return;
        var agents = GetAgents(node, true);
        for (var i = 0; i < agents.Count; ++i)
            agents[i].ResetTarget();
    }

    /// <summary>Set the maximum number of agents.</summary>
    [Serialized("Max Agents", DefaultSource = nameof(GetDefaultMaxAgents))]
    public uint MaxAgents
    {
        get => _maxAgents;
        set
        {
            if (value != _maxAgents && value > 0)
            {
                _maxAgents = value;
                CreateCrowd();
            }
        }
    }

    /// <summary>Set the maximum radius of any agent.</summary>
    [Serialized("Max Agent Radius")]
    public float MaxAgentRadius
    {
        get => _maxAgentRadius;
        set
        {
            if (value != _maxAgentRadius && value > 0.0f)
            {
                _maxAgentRadius = value;
                CreateCrowd();
            }
        }
    }

    /// <summary>Assigns the navigation mesh for the crowd.</summary>
    public NavigationMesh? NavigationMesh
    {
        get => _navigationMesh is not null && _navigationMesh.TryGetTarget(out var navigationMesh) ? navigationMesh : null;
        set
        {
            if (ReferenceEquals(value, NavigationMesh))
                return;

            UnsubscribeFromEvent<NavigationEvents.NavigationMeshRebuiltEvent>();
            UnsubscribeFromEvent<SceneEvents.ComponentRemovedEvent>();

            _navigationMesh = value is not null ? new WeakReference<NavigationMesh>(value) : null;
            _navigationMeshId = value?.Id ?? 0;

            var scene = Scene;
            if (value is not null && scene is not null)
            {
                SubscribeToEvent<NavigationEvents.NavigationMeshRebuiltEvent>(value, HandleNavigationMeshRebuilt);
                SubscribeToEvent<SceneEvents.ComponentRemovedEvent>(scene, HandleComponentRemoved);
            }
            CreateCrowd();
        }
    }

    [Serialized("Filter Types", DefaultSource = nameof(GetDefaultVariantVector))]
    public VariantVector QueryFilterTypesAttr
    {
        get => GetQueryFilterTypesAttr();
        set => SetQueryFilterTypesAttr(value);
    }

    [Serialized("Obstacle Avoidance Types", DefaultSource = nameof(GetDefaultVariantVector))]
    public VariantVector ObstacleAvoidanceTypesAttr
    {
        get => GetObstacleAvoidanceTypesAttr();
        set => SetObstacleAvoidanceTypesAttr(value);
    }

    /// <summary>Set all the query filter types configured in the crowd based on the corresponding attribute.</summary>
    public void SetQueryFilterTypesAttr(VariantVector value)
    {
        if (_crowd is null)
            return;
        var index = 0;
        uint queryFilterType = 0;
        _numQueryFilterTypes = index < value.Count
            ? global::System.Math.Min(value[index++].GetUInt(), (uint)DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE) : 0;
        while (queryFilterType < _numQueryFilterTypes)
        {
            if (index + 3 <= value.Count && GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter)
            {
                filter.SetIncludeFlags(checked((int)value[index++].GetUInt()));
                filter.SetExcludeFlags(checked((int)value[index++].GetUInt()));
                var previousNumAreas = _numAreas[queryFilterType];
                _numAreas[queryFilterType] = global::System.Math.Min(
                    value[index++].GetUInt(), checked((uint)DtDetour.DT_MAX_AREAS));
                if (index + previousNumAreas <= value.Count)
                {
                    for (uint i = 0; i < previousNumAreas; ++i)
                        filter.SetAreaCost(checked((int)i), value[index++].GetFloat());
                }
            }
            ++queryFilterType;
        }
    }

    /// <summary>Set all the obstacle avoidance types configured in the crowd based on the corresponding attribute.</summary>
    public void SetObstacleAvoidanceTypesAttr(VariantVector value)
    {
        if (_crowd is null)
            return;
        var index = 0;
        uint obstacleAvoidanceType = 0;
        _numObstacleAvoidanceTypes = index < value.Count
            ? global::System.Math.Min(value[index++].GetUInt(), (uint)DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS) : 0;
        while (obstacleAvoidanceType < _numObstacleAvoidanceTypes)
        {
            if (index + 10 <= value.Count)
            {
                var parameters = new CrowdObstacleAvoidanceParams
                {
                    VelBias = value[index++].GetFloat(),
                    WeightDesVel = value[index++].GetFloat(),
                    WeightCurVel = value[index++].GetFloat(),
                    WeightSide = value[index++].GetFloat(),
                    WeightToi = value[index++].GetFloat(),
                    HorizTime = value[index++].GetFloat(),
                    GridSize = checked((byte)value[index++].GetUInt()),
                    AdaptiveDivs = checked((byte)value[index++].GetUInt()),
                    AdaptiveRings = checked((byte)value[index++].GetUInt()),
                    AdaptiveDepth = checked((byte)value[index++].GetUInt())
                };
                _crowd.SetObstacleAvoidanceParams(checked((int)obstacleAvoidanceType), ToDetour(parameters));
            }
            ++obstacleAvoidanceType;
        }
    }

    /// <summary>Set the include flags for the specified query filter type.</summary>
    public void SetIncludeFlags(uint queryFilterType, ushort flags)
    {
        if (GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter)
        {
            filter.SetIncludeFlags(flags);
            _numQueryFilterTypes = global::System.Math.Max(_numQueryFilterTypes, queryFilterType + 1);
        }
    }

    /// <summary>Set the exclude flags for the specified query filter type.</summary>
    public void SetExcludeFlags(uint queryFilterType, ushort flags)
    {
        if (GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter)
        {
            filter.SetExcludeFlags(flags);
            _numQueryFilterTypes = global::System.Math.Max(_numQueryFilterTypes, queryFilterType + 1);
        }
    }

    /// <summary>Set the cost of an area for the specified query filter type.</summary>
    public void SetAreaCost(uint queryFilterType, uint areaID, float cost)
    {
        if (GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter && areaID < DtDetour.DT_MAX_AREAS)
        {
            filter.SetAreaCost(checked((int)areaID), cost);
            _numQueryFilterTypes = global::System.Math.Max(_numQueryFilterTypes, queryFilterType + 1);
            _numAreas[queryFilterType] = global::System.Math.Max(_numAreas[queryFilterType], areaID + 1);
        }
    }

    /// <summary>Set the params for the specified obstacle avoidance type.</summary>
    public void SetObstacleAvoidanceParams(uint obstacleAvoidanceType, CrowdObstacleAvoidanceParams parameters)
    {
        if (_crowd is not null && obstacleAvoidanceType < DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS)
        {
            _crowd.SetObstacleAvoidanceParams(checked((int)obstacleAvoidanceType), ToDetour(parameters));
            _numObstacleAvoidanceTypes = global::System.Math.Max(_numObstacleAvoidanceTypes, obstacleAvoidanceType + 1);
        }
    }

    /// <summary>Get all the crowd agent components in the specified node hierarchy.</summary>
    public IReadOnlyList<CrowdAgent> GetAgents(Node? node = null, bool inCrowdFilter = true)
    {
        node ??= Scene;
        if (node is null)
            return [];
        var found = node.FindComponents<CrowdAgent>(ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived);
        if (!inCrowdFilter)
            return found;
        var result = new List<CrowdAgent>();
        for (var i = 0; i < found.Count; ++i)
        {
            if (found[i].IsInCrowd)
                result.Add(found[i]);
        }
        return result;
    }

    /// <summary>Find the nearest point on the navigation mesh.</summary>
    public Vector3f FindNearestPoint(Vector3f point, int queryFilterType, out long nearestRef)
    {
        nearestRef = 0;
        var navMesh = NavigationMesh;
        return _crowd is not null && navMesh is not null
            ? navMesh.FindNearestPoint(point, FromRecast(_crowd.GetQueryExtents()), _crowd.GetFilter(queryFilterType), out nearestRef)
            : point;
    }

    public Vector3f FindNearestPoint(Vector3f point, int queryFilterType = 0) =>
        FindNearestPoint(point, queryFilterType, out _);

    /// <summary>Try to move along the surface from one point to another.</summary>
    public Vector3f MoveAlongSurface(Vector3f start, Vector3f end, int queryFilterType, int maxVisited = 3)
    {
        var navMesh = NavigationMesh;
        return _crowd is not null && navMesh is not null
            ? navMesh.MoveAlongSurface(start, end, FromRecast(_crowd.GetQueryExtents()), maxVisited, _crowd.GetFilter(queryFilterType))
            : end;
    }

    /// <summary>Find a path between world space points.</summary>
    public void FindPath(ICollection<Vector3f> destination, Vector3f start, Vector3f end, int queryFilterType)
    {
        if (_crowd is not null && NavigationMesh is { } navMesh)
            navMesh.FindPath(destination, start, end, FromRecast(_crowd.GetQueryExtents()), _crowd.GetFilter(queryFilterType));
    }

    /// <summary>Return a random point on the navigation mesh.</summary>
    public Vector3f GetRandomPoint(int queryFilterType, out long randomRef)
    {
        randomRef = 0;
        return _crowd is not null && NavigationMesh is { } navMesh
            ? navMesh.GetRandomPoint(_crowd.GetFilter(queryFilterType), out randomRef)
            : Vector3f.Zero;
    }

    public Vector3f GetRandomPoint(int queryFilterType = 0) => GetRandomPoint(queryFilterType, out _);

    /// <summary>Return a random point on the navigation mesh within a circle.</summary>
    public Vector3f GetRandomPointInCircle(Vector3f center, float radius, int queryFilterType, out long randomRef)
    {
        randomRef = 0;
        return _crowd is not null && NavigationMesh is { } navMesh
            ? navMesh.GetRandomPointInCircle(center, radius, FromRecast(_crowd.GetQueryExtents()), _crowd.GetFilter(queryFilterType), out randomRef)
            : center;
    }

    public Vector3f GetRandomPointInCircle(Vector3f center, float radius, int queryFilterType = 0) =>
        GetRandomPointInCircle(center, radius, queryFilterType, out _);

    /// <summary>Return distance to wall from a point.</summary>
    public float GetDistanceToWall(Vector3f point, float radius, int queryFilterType,
        out Vector3f hitPos, out Vector3f hitNormal)
    {
        hitPos = Vector3f.Zero;
        hitNormal = Vector3f.Down;
        return _crowd is not null && NavigationMesh is { } navMesh
            ? navMesh.GetDistanceToWall(point, radius, FromRecast(_crowd.GetQueryExtents()), _crowd.GetFilter(queryFilterType), out hitPos, out hitNormal)
            : radius;
    }

    public float GetDistanceToWall(Vector3f point, float radius, int queryFilterType = 0) =>
        GetDistanceToWall(point, radius, queryFilterType, out _, out _);

    /// <summary>Perform a walkability raycast on the navigation mesh.</summary>
    public Vector3f Raycast(Vector3f start, Vector3f end, int queryFilterType, out Vector3f hitNormal)
    {
        hitNormal = Vector3f.Down;
        return _crowd is not null && NavigationMesh is { } navMesh
            ? navMesh.Raycast(start, end, FromRecast(_crowd.GetQueryExtents()), _crowd.GetFilter(queryFilterType), out hitNormal)
            : end;
    }

    public Vector3f Raycast(Vector3f start, Vector3f end, int queryFilterType = 0) =>
        Raycast(start, end, queryFilterType, out _);

    /// <summary>Get the number of configured query filter types.</summary>
    public uint NumQueryFilterTypes => _numQueryFilterTypes;

    /// <summary>Get the number of configured area in the specified query filter type.</summary>
    public uint GetNumAreas(uint queryFilterType) => queryFilterType < _numQueryFilterTypes ? _numAreas[queryFilterType] : 0;

    /// <summary>Get the include flags for the specified query filter type.</summary>
    public ushort GetIncludeFlags(uint queryFilterType)
    {
        if (queryFilterType >= _numQueryFilterTypes)
            Log.GetLogger().LogWarning("Query filter type {QueryFilterType} is not configured yet, returning the default include flags initialized by DetourCrowd", queryFilterType);
        return GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter ? checked((ushort)filter.GetIncludeFlags()) : ushort.MaxValue;
    }

    /// <summary>Get the exclude flags for the specified query filter type.</summary>
    public ushort GetExcludeFlags(uint queryFilterType)
    {
        if (queryFilterType >= _numQueryFilterTypes)
            Log.GetLogger().LogWarning("Query filter type {QueryFilterType} is not configured yet, returning the default exclude flags initialized by DetourCrowd", queryFilterType);
        return GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter ? checked((ushort)filter.GetExcludeFlags()) : (ushort)0;
    }

    /// <summary>Get the cost of an area for the specified query filter type.</summary>
    public float GetAreaCost(uint queryFilterType, uint areaID)
    {
        if (queryFilterType >= _numQueryFilterTypes || areaID >= _numAreas[queryFilterType])
            Log.GetLogger().LogWarning("Query filter type {QueryFilterType} and/or area id {AreaId} are not configured yet, returning the default area cost initialized by DetourCrowd", queryFilterType, areaID);
        return GetDetourQueryFilter(queryFilterType) is CrowdQueryFilter filter
            ? filter.GetAreaCost(checked((int)areaID)) : 1.0f;
    }

    /// <summary>Get the number of configured obstacle avoidance types.</summary>
    public uint NumObstacleAvoidanceTypes => _numObstacleAvoidanceTypes;

    /// <summary>Get the params for the specified obstacle avoidance type.</summary>
    public CrowdObstacleAvoidanceParams GetObstacleAvoidanceParams(uint obstacleAvoidanceType)
    {
        if (_crowd is null || obstacleAvoidanceType >= DtCrowdConst.DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS)
            return default;
        return FromDetour(_crowd.GetObstacleAvoidanceParams(checked((int)obstacleAvoidanceType)));
    }

    /// <summary>Return all the filter types configured in the crowd as attribute.</summary>
    public VariantVector GetQueryFilterTypesAttr()
    {
        var result = new VariantVector();
        if (_crowd is null)
        {
            result.Add(new Variant(0));
            return result;
        }
        result.Add(new Variant(_numQueryFilterTypes));
        for (uint i = 0; i < _numQueryFilterTypes; ++i)
        {
            result.Add(new Variant((uint)GetIncludeFlags(i)));
            result.Add(new Variant((uint)GetExcludeFlags(i)));
            result.Add(new Variant(_numAreas[i]));
            for (uint j = 0; j < _numAreas[i]; ++j)
                result.Add(new Variant(GetAreaCost(i, j)));
        }
        return result;
    }

    /// <summary>Return all the obstacle avoidance types configured in the crowd as attribute.</summary>
    public VariantVector GetObstacleAvoidanceTypesAttr()
    {
        var result = new VariantVector();
        result.Add(new Variant(_numObstacleAvoidanceTypes));
        if (_crowd is null)
            return result;
        for (uint i = 0; i < _numObstacleAvoidanceTypes; ++i)
        {
            var parameters = GetObstacleAvoidanceParams(i);
            result.Add(new Variant(parameters.VelBias));
            result.Add(new Variant(parameters.WeightDesVel));
            result.Add(new Variant(parameters.WeightCurVel));
            result.Add(new Variant(parameters.WeightSide));
            result.Add(new Variant(parameters.WeightToi));
            result.Add(new Variant(parameters.HorizTime));
            result.Add(new Variant((uint)parameters.GridSize));
            result.Add(new Variant((uint)parameters.AdaptiveDivs));
            result.Add(new Variant((uint)parameters.AdaptiveRings));
            result.Add(new Variant((uint)parameters.AdaptiveDepth));
        }
        return result;
    }

    internal DtCrowd? Crowd => _crowd;

    /// <summary>Create and initialize internal Detour crowd object.</summary>
    protected bool CreateCrowd()
    {
        var navigationMesh = NavigationMesh;
        if (navigationMesh is null || !navigationMesh.InitializeQueryForCrowd() || navigationMesh.DetourNavMesh is null)
            return false;

        var filterConfiguration = CaptureFilterConfiguration();
        var avoidanceConfiguration = CaptureAvoidanceConfiguration();

        if (_maxAgentRadius == 0.0f)
            _maxAgentRadius = navigationMesh.AgentRadius;
        _crowd = new DtCrowd(new DtCrowdConfig(_maxAgentRadius), navigationMesh.DetourNavMesh,
            static _ => new CrowdQueryFilter());

        RestoreFilterConfiguration(filterConfiguration);
        RestoreAvoidanceConfiguration(avoidanceConfiguration);

        var agents = GetAgents(null, false);
        for (var i = 0; i < agents.Count; ++i)
        {
            if (agents[i].AddAgentToCrowd(true) == -1)
            {
                Log.GetLogger().LogWarning("CrowdManager: {Count} crowd agents orphaned", agents.Count - i);
                break;
            }
        }
        return true;
    }

    /// <summary>Create and add a Detour crowd agent.</summary>
    internal int AddAgent(CrowdAgent agent, Vector3f position)
    {
        var navigationMesh = NavigationMesh;
        if (_crowd is null || navigationMesh is null || _crowd.GetActiveAgents().Count >= _maxAgents)
            return -1;
        if (agent.Radius == 0.0f)
            agent.Radius = navigationMesh.AgentRadius;
        if (agent.Height == 0.0f)
            agent.Height = navigationMesh.AgentHeight;
        var parameters = new DtCrowdAgentParams
        {
            userData = agent,
            queryFilterType = checked((int)agent.QueryFilterType)
        };
        return _crowd.AddAgent(ToRecast(position), parameters).idx;
    }

    /// <summary>Remove the Detour crowd agent.</summary>
    internal void RemoveAgent(CrowdAgent agent)
    {
        var detourAgent = GetEditableDetourCrowdAgent(agent.AgentCrowdId);
        if (_crowd is null || detourAgent is null)
            return;
        detourAgent.option.userData = null!;
        _crowd.RemoveAgent(detourAgent);
    }

    /// <summary>Handle scene being assigned.</summary>
    protected override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        if (scene is not null)
        {
            if (!ReferenceEquals(scene, _node))
            {
                Log.GetLogger().Error("CrowdManager is a scene component and should only be attached to the scene node");
                return;
            }

            SubscribeToEvent<SceneEvents.SceneSubsystemUpdateEvent>(scene, HandleSceneSubsystemUpdate);
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);

            if (_navigationMeshId == 0)
            {
                var navMesh = scene.FindComponent<NavigationMesh>(ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived);
                if (navMesh is not null)
                    NavigationMesh = navMesh;
                else
                    SubscribeToEvent<SceneEvents.ComponentAddedEvent>(scene, HandleComponentAdded);
            }
        }
        else
        {
            UnsubscribeFromEvent<SceneEvents.SceneSubsystemUpdateEvent>();
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>();
            UnsubscribeFromEvent<NavigationEvents.NavigationMeshRebuiltEvent>();
            UnsubscribeFromEvent<SceneEvents.ComponentAddedEvent>();
            UnsubscribeFromEvent<SceneEvents.ComponentRemovedEvent>();
            _navigationMesh = null;
        }
    }

    /// <summary>Update the crowd simulation.</summary>
    protected void Update(float delta)
    {
        if (_crowd is null)
            return;
        _crowd.Update(delta, null!);
        var agents = _crowd.GetActiveAgents();
        for (var i = 0; i < agents.Count; ++i)
        {
            var agent = agents[i];
            if (agent.option.userData is CrowdAgent crowdAgent)
            {
                crowdAgent.OnCrowdVelocityUpdate(agent, delta);
                crowdAgent.OnCrowdPositionUpdate(agent, delta);
            }
        }
    }

    internal DtCrowdAgent? GetDetourCrowdAgent(int agent) => _crowd?.GetAgent(agent);
    internal DtCrowdAgent? GetEditableDetourCrowdAgent(int agent) => _crowd?.GetAgent(agent);

    private IDtQueryFilter? GetDetourQueryFilter(uint queryFilterType)
    {
        return _crowd is not null && queryFilterType < DtCrowdConst.DT_CROWD_MAX_QUERY_FILTER_TYPE
            ? _crowd.GetFilter(checked((int)queryFilterType)) : null;
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        if (_crowd is null)
            return;
        var delta = new Vector3f(eventData.Delta.X, eventData.Delta.Y, eventData.Delta.Z);
        foreach (var agent in _crowd.GetActiveAgents())
        {
            if (agent.option.userData is CrowdAgent crowdAgent)
                crowdAgent.UpdateWorldOrigin(delta);
        }
    }

    private void HandleSceneSubsystemUpdate(ref SceneEvents.SceneSubsystemUpdateEvent eventData)
    {
        if (_crowd is not null && NavigationMesh is not null && IsEnabledEffective)
            Update(eventData.TimeStep);
    }

    private void HandleNavigationMeshRebuilt(ref NavigationEvents.NavigationMeshRebuiltEvent eventData)
    {
        if (ReferenceEquals(eventData.Mesh, NavigationMesh))
        {
            _navigationMesh = null;
            NavigationMesh = eventData.Mesh;
        }
    }

    private void HandleComponentRemoved(ref SceneEvents.ComponentRemovedEvent eventData)
    {
        if (ReferenceEquals(eventData.Component, NavigationMesh))
            NavigationMesh = null;
    }

    private void HandleComponentAdded(ref SceneEvents.ComponentAddedEvent eventData)
    {
        var scene = Scene;
        if (scene is null)
            return;
        var navMesh = scene.FindComponent<NavigationMesh>(ComponentSearchFlag.SelfOrChildrenRecursive | ComponentSearchFlag.Derived);
        if (navMesh is not null)
            NavigationMesh = navMesh;
    }

    private FilterConfiguration[] CaptureFilterConfiguration()
    {
        var result = new FilterConfiguration[_numQueryFilterTypes];
        for (var i = 0; i < result.Length; ++i)
        {
            result[i].IncludeFlags = GetIncludeFlags(checked((uint)i));
            result[i].ExcludeFlags = GetExcludeFlags(checked((uint)i));
            result[i].AreaCosts = new float[_numAreas[i]];
            for (var j = 0; j < result[i].AreaCosts.Length; ++j)
                result[i].AreaCosts[j] = GetAreaCost(checked((uint)i), checked((uint)j));
        }
        return result;
    }

    private void RestoreFilterConfiguration(FilterConfiguration[] configuration)
    {
        for (var i = 0; i < configuration.Length; ++i)
        {
            SetIncludeFlags(checked((uint)i), configuration[i].IncludeFlags);
            SetExcludeFlags(checked((uint)i), configuration[i].ExcludeFlags);
            for (var j = 0; j < configuration[i].AreaCosts.Length; ++j)
                SetAreaCost(checked((uint)i), checked((uint)j), configuration[i].AreaCosts[j]);
        }
    }

    private CrowdObstacleAvoidanceParams[] CaptureAvoidanceConfiguration()
    {
        var result = new CrowdObstacleAvoidanceParams[_numObstacleAvoidanceTypes];
        for (var i = 0; i < result.Length; ++i)
            result[i] = GetObstacleAvoidanceParams(checked((uint)i));
        return result;
    }

    private void RestoreAvoidanceConfiguration(CrowdObstacleAvoidanceParams[] configuration)
    {
        for (var i = 0; i < configuration.Length; ++i)
            SetObstacleAvoidanceParams(checked((uint)i), configuration[i]);
    }

    private struct FilterConfiguration
    {
        public ushort IncludeFlags;
        public ushort ExcludeFlags;
        public float[] AreaCosts;
    }

    private static DtObstacleAvoidanceParams ToDetour(CrowdObstacleAvoidanceParams value) => new()
    {
        velBias = value.VelBias,
        weightDesVel = value.WeightDesVel,
        weightCurVel = value.WeightCurVel,
        weightSide = value.WeightSide,
        weightToi = value.WeightToi,
        horizTime = value.HorizTime,
        gridSize = value.GridSize,
        adaptiveDivs = value.AdaptiveDivs,
        adaptiveRings = value.AdaptiveRings,
        adaptiveDepth = value.AdaptiveDepth
    };

    private static CrowdObstacleAvoidanceParams FromDetour(DtObstacleAvoidanceParams value) => new()
    {
        VelBias = value.velBias,
        WeightDesVel = value.weightDesVel,
        WeightCurVel = value.weightCurVel,
        WeightSide = value.weightSide,
        WeightToi = value.weightToi,
        HorizTime = value.horizTime,
        GridSize = checked((byte)value.gridSize),
        AdaptiveDivs = checked((byte)value.adaptiveDivs),
        AdaptiveRings = checked((byte)value.adaptiveRings),
        AdaptiveDepth = checked((byte)value.adaptiveDepth)
    };

    private static uint GetDefaultMaxAgents() => DefaultMaxAgents;
    private static VariantVector GetDefaultVariantVector() => new();

    private static RcVec3f ToRecast(Vector3f value) => new(value.X, value.Y, value.Z);
    private static Vector3f FromRecast(RcVec3f value) => new(value.X, value.Y, value.Z);
}
