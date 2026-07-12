// Copyright (c) 2026 RedBoltFramework
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

namespace RedBolt;

/// <summary>
/// Base component for objects that provide animation states to animated models.
/// </summary>
public abstract class AnimationStateSource : Component
{
    // TODO: port: AnimationState and AnimationStateVector are not available yet; preserve the C++ vector as an opaque managed list.
    protected readonly List<object> _animationStates = [];
    protected bool _resetSkeleton;

    protected AnimationStateSource()
    {
    }

    /// <summary>
    /// Mark that animation state tracks are dirty and should be reconnected.
    /// Should be called on every substantial change in animated structure.
    /// </summary>
    public abstract void MarkAnimationStateTracksDirty();

    /// <summary>
    /// Return animations states for AnimatedModel.
    /// </summary>
    public IReadOnlyList<object> GetAnimationStates() => _animationStates;

    /// <summary>
    /// Return whether animation throttling is disabled.
    /// </summary>
    public bool IsAnimationThrottlingAllowed() => !_resetSkeleton;
}
