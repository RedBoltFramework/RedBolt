namespace RedBolt.Scene;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Math;

/// <summary>
/// Component that maintains world origin around the node.
/// </summary>
[Reflected(ObjectCategory.Category_Scene)]
public partial class WorldOrigin : LogicComponent
{
    public const uint MaxDistance = 8192;
    public const uint DefaultStep = 128;

    public WorldOrigin()
    {
        SetUpdateEventMask(UpdateEvent.UseUpdate);

    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }


    /// <summary>
    /// Implement LogicComponent.
    /// </summary>
    public override void Update(float timeStep)
    {
        var position = Node!.GetWorldPosition();
        var distanceToOrigin = SystemMath.Max(
            SystemMath.Max(_updateX ? SystemMath.Abs(position.X) : 0.0f, _updateY ? SystemMath.Abs(position.Y) : 0.0f),
            _updateZ ? SystemMath.Abs(position.Z) : 0.0f);
        if (distanceToOrigin > _maxDistance)
        {
            var offset = new Vector3i(
                _updateX ? (int)MathDefs.SnapRound(position.X, _step) : 0,
                _updateY ? (int)MathDefs.SnapRound(position.Y, _step) : 0,
                _updateZ ? (int)MathDefs.SnapRound(position.Z, _step) : 0);
            if (offset != Vector3i.Zero)
            {
                var scene = Node.Scene!;
                var currentOrigin = scene.GetWorldOrigin();
                var newWorldOrigin = new IntVector3(
                    currentOrigin.X + offset.X,
                    currentOrigin.Y + offset.Y,
                    currentOrigin.Z + offset.Z);
                var weakScene = new WeakReference<Scene>(scene);
                var weakSelf = new WeakReference<WorldOrigin>(this);
                void ApplyRebase()
                {
                    if (!weakScene.TryGetTarget(out var targetScene))
                        return;

                    var oldPosition = weakSelf.TryGetTarget(out var targetSelf)
                        ? targetSelf.Node!.GetWorldPosition()
                        : Vector3f.Zero;
                    targetScene.UpdateWorldOrigin(newWorldOrigin);

                    if (weakSelf.TryGetTarget(out targetSelf))
                    {
                        var newPosition = targetSelf.Node!.GetWorldPosition();
                        if (newPosition.ApproximatelyEquivalent(oldPosition))
                        {
                            targetSelf.SetEnabled(false);
                            Log.GetLogger().LogError(
                                "WorldOrigin '{WorldOrigin}' was not moved from {Position} during rebase, disabling",
                                targetSelf.GetFullNameDebug(), newPosition);
                        }
                    }
                }

                var workQueue = GetSubsystem<WorkQueue>();
                workQueue.PostDelayedTaskForMainThread(ApplyRebase);
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

    [Serialized("Max Distance")]
    private uint _maxDistance = MaxDistance;
    [Serialized("Step")]
    private uint _step = DefaultStep;
    [Serialized("Update X")]
    private bool _updateX = true;
    [Serialized("Update Y")]
    private bool _updateY;
    [Serialized("Update Z")]
    private bool _updateZ = true;

    private static object? GetIsEnabledAttributeDefault() => true;

    [Serialized("Is Enabled", DefaultSource = nameof(GetIsEnabledAttributeDefault))]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
