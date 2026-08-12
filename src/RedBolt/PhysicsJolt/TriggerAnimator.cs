//
// Copyright (c) 2023-2023 the rbfx project.
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

namespace RedBolt.PhysicsJolt;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = RedBolt.Resource.Resource;
using RedBolt.Scene;

/// <summary>Component that runs animation when triggered.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class TriggerAnimator : Component
{
    private bool _isSubscribed;
    private Animation? _enterAnimation;
    private Animation? _exitAnimation;
    private readonly HashSet<Node> _activeCollisions = [];

    /// <summary>Set enter animation attribute.</summary>
    public void SetEnterAnimationAttr(ResourceRef value)
    {
        _enterAnimation = GetSubsystem<ResourceCache>()!.GetResourceOrNull<Animation>(value.Name);
    }

    /// <summary>Return enter animation attribute.</summary>
    [Serialized("Enter Animation", DefaultSource = nameof(GetAnimationAttributeDefault),
        Setter = nameof(SetEnterAnimationAttr))]
    public ResourceRef GetEnterAnimationAttr() =>
        ResourceType.GetResourceRef(_enterAnimation, ObjectReflection<Animation>.TypeId);

    /// <summary>Set enter animation.</summary>
    public void SetEnterAnimation(Animation? value)
    {
        _enterAnimation = value;
    }

    /// <summary>Return enter animation.</summary>
    public Animation? GetEnterAnimation() => _enterAnimation;

    /// <summary>Set exit animation attribute.</summary>
    public void SetExitAnimationAttr(ResourceRef value)
    {
        _exitAnimation = GetSubsystem<ResourceCache>()!.GetResourceOrNull<Animation>(value.Name);
    }

    /// <summary>Return exit animation attribute.</summary>
    [Serialized("Exit Animation", DefaultSource = nameof(GetAnimationAttributeDefault),
        Setter = nameof(SetExitAnimationAttr))]
    public ResourceRef GetExitAnimationAttr() =>
        ResourceType.GetResourceRef(_exitAnimation, ObjectReflection<Animation>.TypeId);

    [Serialized("Is Entered")]
    private bool IsEnteredAttribute { get; set; }

    /// <summary>Set exit animation.</summary>
    public void SetExitAnimation(Animation? value)
    {
        _exitAnimation = value;
    }

    /// <summary>Return exit animation.</summary>
    public Animation? GetExitAnimation() => _exitAnimation;

    /// <summary>Executed when first compatible body enters the trigger.</summary>
    public virtual void OnEnter()
    {
    }

    /// <summary>Executed when last compatible body leaves the trigger.</summary>
    public virtual void OnExit()
    {
    }

    /// <summary>Filter entering node. Return true if the trigger should react on the object.</summary>
    public virtual bool Filter(Node node) => true;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        UpdateSubscriptions();
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        UpdateSubscriptions();
    }

    /// <summary>Register entering node.</summary>
    protected void RegisterEnter(Node? node)
    {
        if (node is null || !Filter(node))
            return;

        if (_activeCollisions.Count == 0 && !IsEnteredAttribute)
        {
            IsEnteredAttribute = true;
            StartAnimation(_enterAnimation);
            OnEnter();
        }
        _activeCollisions.Add(node);
    }

    /// <summary>Register exiting node.</summary>
    protected void RegisterExit(Node? node)
    {
        if (node is null)
            return;

        _activeCollisions.Remove(node);
        if (_activeCollisions.Count == 0 && IsEnteredAttribute)
        {
            IsEnteredAttribute = false;
            StartAnimation(_exitAnimation);
            OnExit();
        }
    }

    /// <summary>Start selected animation. The argument should be either enter or exit animation.</summary>
    protected void StartAnimation(Animation? animation)
    {
        if (animation is null)
            return;

        var animationController = GetComponent<AnimationController>();
        if (animationController is null)
        {
            Log.GetLogger().Error("TriggerAnimator can't start animation: no AnimationController found");
            return;
        }

        var animationParameters = new AnimationParameters(animation)
        {
            RemoveOnZeroWeight = true
        };

        if (animationController.GetNumAnimations() > 0)
        {
            var otherAnimation = ReferenceEquals(animation, _enterAnimation) ? _exitAnimation : _enterAnimation;
            var existingAnimationIndex = animationController.FindLastAnimation(otherAnimation);
            if (existingAnimationIndex >= 0)
            {
                var state = animationController.GetAnimationParameters(existingAnimationIndex);
                var time = state.GetTime() / (state.GetAnimation()!.Length + MathDefs.Epsilon);
                animationParameters.Time((1.0f - time) * animation.Length);
            }
        }
        animationController.PlayExistingExclusive(animationParameters);
    }

    protected void HandleNodeCollisionStart(ref PhysicsEvents.NodeCollisionStartEvent eventData)
    {
        RegisterEnter(eventData.OtherNode);
    }

    protected void HandleNodeCollisionEnd(ref PhysicsEvents.NodeCollisionEndEvent eventData)
    {
        RegisterExit(eventData.OtherNode);
    }

    protected void UpdateSubscriptions()
    {
        var subscribe = TryGetNode() is not null && IsEnabledEffective;
        if (subscribe == _isSubscribed)
            return;

        _isSubscribed = subscribe;
        if (_isSubscribed)
        {
            SubscribeToEvent<PhysicsEvents.NodeCollisionStartEvent>(Node, HandleNodeCollisionStart);
            SubscribeToEvent<PhysicsEvents.NodeCollisionEndEvent>(Node, HandleNodeCollisionEnd);
        }
        else
        {
            UnsubscribeFromEvent<PhysicsEvents.NodeCollisionStartEvent>();
            UnsubscribeFromEvent<PhysicsEvents.NodeCollisionEndEvent>();
        }
    }

    private static object? GetAnimationAttributeDefault() =>
        new ResourceRef(ObjectReflection<Animation>.TypeId);
}
