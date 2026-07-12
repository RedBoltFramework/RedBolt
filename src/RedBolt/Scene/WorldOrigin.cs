using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Component that maintains world origin around the node.
/// </summary>
public sealed class WorldOrigin : LogicComponent
{
    public const uint MaxDistance = 8192;
    public const uint DefaultStep = 128;

    public WorldOrigin()
    {
        SetUpdateEventMask(UpdateEvent.UseUpdate);

        RegisterAttribute(
            "Max Distance",
            typeof(uint),
            self => ((WorldOrigin)self)._maxDistance,
            (self, value) => ((WorldOrigin)self)._maxDistance = value is uint distance ? distance : 0u,
            MaxDistance);
        RegisterAttribute(
            "Step",
            typeof(uint),
            self => ((WorldOrigin)self)._step,
            (self, value) => ((WorldOrigin)self)._step = value is uint step ? step : 0u,
            DefaultStep);
        RegisterAttribute(
            "Update X",
            typeof(bool),
            self => ((WorldOrigin)self)._updateX,
            (self, value) => ((WorldOrigin)self)._updateX = value is bool enabled && enabled,
            true);
        RegisterAttribute(
            "Update Y",
            typeof(bool),
            self => ((WorldOrigin)self)._updateY,
            (self, value) => ((WorldOrigin)self)._updateY = value is bool enabled && enabled,
            false);
        RegisterAttribute(
            "Update Z",
            typeof(bool),
            self => ((WorldOrigin)self)._updateZ,
            (self, value) => ((WorldOrigin)self)._updateZ = value is bool enabled && enabled,
            true);
    }

    public static void RegisterObject(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddFactoryReflection<WorldOrigin>(ObjectCategory.Category_Scene);

        // URHO3D_ACCESSOR_ATTRIBUTE("Is Enabled", IsEnabled, SetEnabled, bool, true, AM_DEFAULT);
        // URHO3D_ATTRIBUTE("Max Distance", unsigned, maxDistance_, MaxDistance, AM_DEFAULT);
        // URHO3D_ATTRIBUTE("Step", unsigned, step_, DefaultStep, AM_DEFAULT);
        // URHO3D_ATTRIBUTE("Update X", bool, updateX_, true, AM_DEFAULT);
        // URHO3D_ATTRIBUTE("Update Y", bool, updateY_, false, AM_DEFAULT);
        // URHO3D_ATTRIBUTE("Update Z", bool, updateZ_, true, AM_DEFAULT);
    }

    /// <summary>
    /// Implement LogicComponent.
    /// </summary>
    public override void Update(float timeStep)
    {
        var position = Node.GetWorldPosition();
        var distanceToOrigin = Math.Max(
            Math.Max(_updateX ? Math.Abs(position.X) : 0.0f, _updateY ? Math.Abs(position.Y) : 0.0f),
            _updateZ ? Math.Abs(position.Z) : 0.0f);
        if (distanceToOrigin > _maxDistance)
        {
            var offset = new Vector3i(
                _updateX ? (int)MathDefs.SnapRound(position.X, _step) : 0,
                _updateY ? (int)MathDefs.SnapRound(position.Y, _step) : 0,
                _updateZ ? (int)MathDefs.SnapRound(position.Z, _step) : 0);
            if (offset != Vector3i.Zero)
            {
                var scene = Node.GetScene();
                var currentOrigin = scene.GetWorldOrigin();
                var newWorldOrigin = new IntVector3(
                    currentOrigin.X + offset.X,
                    currentOrigin.Y + offset.Y,
                    currentOrigin.Z + offset.Z);
                var oldPosition = Node.GetWorldPosition();
                scene.UpdateWorldOrigin(newWorldOrigin);
                if (Node.GetWorldPosition() == oldPosition)
                {
                    SetEnabled(false);
                    Log.Logger.LogError(
                        "WorldOrigin '{WorldOrigin}' was not moved from {Position} during rebase, disabling.",
                        GetFullNameDebug(), Node.GetWorldPosition());
                }
            }
        }
    }

    /// <summary>
    /// Set maximum distance from the origin.
    /// </summary>
    public void SetMaxDistance(uint distance) => _maxDistance = distance;

    /// <summary>
    /// Return maximum distance from the origin.
    /// </summary>
    public uint GetMaxDistance() => _maxDistance;

    /// <summary>
    /// Set origin update step.
    /// </summary>
    public void SetStep(uint step) => _step = step;

    /// <summary>
    /// Return origin update step.
    /// </summary>
    public uint GetStep() => _step;

    /// <summary>
    /// Set whether to update the X coordinate.
    /// </summary>
    public void SetUpdateX(uint value) => _updateX = value != 0;

    /// <summary>
    /// Return whether to update the X coordinate.
    /// </summary>
    public uint GetUpdateX() => _updateX ? 1u : 0u;

    /// <summary>
    /// Set whether to update the Y coordinate.
    /// </summary>
    public void SetUpdateY(uint value) => _updateY = value != 0;

    /// <summary>
    /// Return whether to update the Y coordinate.
    /// </summary>
    public uint GetUpdateY() => _updateY ? 1u : 0u;

    /// <summary>
    /// Set whether to update the Z coordinate.
    /// </summary>
    public void SetUpdateZ(uint value) => _updateZ = value != 0;

    /// <summary>
    /// Return whether to update the Z coordinate.
    /// </summary>
    public uint GetUpdateZ() => _updateZ ? 1u : 0u;

    private uint _maxDistance = MaxDistance;
    private uint _step = DefaultStep;
    private bool _updateX = true;
    private bool _updateY;
    private bool _updateZ = true;
}
