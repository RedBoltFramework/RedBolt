// Copyright (c) 2017-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using RedBolt.Scene;

namespace RedBolt.Graphics;

public abstract class AnimationStateSource : Component
{
    public AnimationStateSource()
    {
    }

    public override void Dispose()
    {
        _animationStates.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Mark that animation state tracks are dirty and should be reconnected.
    /// Should be called on every substantial change in animated structure.
    /// </summary>
    public abstract void MarkAnimationStateTracksDirty();

    /// <summary>
    /// Return animations states for AnimatedModel.
    /// </summary>
    public IReadOnlyList<AnimationState> GetAnimationStates() => _animationStates;

    /// <summary>
    /// Return whether animation throttling is disabled.
    /// </summary>
    public bool IsAnimationThrottlingAllowed() => !_resetSkeleton;

    /// <summary>
    /// Animation states. Shared with AnimatedModel when possible.
    /// </summary>
    protected readonly List<AnimationState> _animationStates = [];

    /// <summary>
    /// Whether skeleton is reset on every frame.
    /// </summary>
    protected bool _resetSkeleton;
}
