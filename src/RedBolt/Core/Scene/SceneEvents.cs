namespace RedBolt.Scene;

public static class SceneEvents
{
    /// <summary>Scene paused or resumed. Check current scene state via IsUpdateEnabled() method.</summary>
    public struct SceneUpdatesChangedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Boolean.</summary>
        public bool UpdateEnabled;
    }

    /// <summary>Define scene update event. It always contains pointer to the scene and the timestep.</summary>
    public interface ISceneUpdateEvent
    {
        float TimeStep { get; }
    }

    /// <summary>Sent even for paused scenes!</summary>
    public struct SceneForcedUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    public struct SceneUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    public struct AttributeAnimationUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    public struct SceneSubsystemUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    public struct ScenePostUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    /// <summary>Sent even for paused scenes!</summary>
    public struct SceneForcedPostUpdateEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    /// <summary>
    /// Network-aware scene update.
    /// In standalone mode, SceneNetworkUpdate is equivalent to SceneUpdate.
    /// In server mode, SceneNetworkUpdate is called once per network frame with fixed timestep.
    /// In client mode, SceneNetworkUpdate is called on each rendered frame like SceneUpdate,
    /// but timesteps are properly scaled according to network clocks.
    /// </summary>
    public struct SceneNetworkUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStepReplica;
        /// <summary>Float.</summary>
        public float TimeStepInput;
    }

    /// <summary>Network-aware scene post-update.</summary>
    public struct SceneNetworkPostUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStepReplica;
        /// <summary>Float.</summary>
        public float TimeStepInput;
    }

    /// <summary>Scene drawable update finished. Custom animation (eg. IK) can be done at this point.</summary>
    public struct SceneDrawableUpdateFinishedEvent : ISceneUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float TimeStep;

        float ISceneUpdateEvent.TimeStep => TimeStep;
    }

    /// <summary>Attribute animation added to object animation.</summary>
    public struct AttributeAnimationAddedEvent
    {
        /// <summary>Object animation pointer.</summary>
        public ObjectAnimation ObjectAnimation;
        /// <summary>String.</summary>
        public string AttributeAnimationName;
    }

    /// <summary>Attribute animation removed from object animation.</summary>
    public struct AttributeAnimationRemovedEvent
    {
        /// <summary>Object animation pointer.</summary>
        public ObjectAnimation ObjectAnimation;
        /// <summary>String.</summary>
        public string AttributeAnimationName;
    }

    /// <summary>Asynchronous scene loading progress.</summary>
    public struct AsyncLoadProgressEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Float.</summary>
        public float Progress;
        /// <summary>Int.</summary>
        public int LoadedNodes;
        /// <summary>Int.</summary>
        public int TotalNodes;
        /// <summary>Int.</summary>
        public int LoadedResources;
        /// <summary>Int.</summary>
        public int TotalResources;
    }

    /// <summary>Asynchronous scene loading finished.</summary>
    public struct AsyncLoadFinishedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
    }

    /// <summary>A child node has been added to a parent node.</summary>
    public struct NodeAddedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Parent;
        /// <summary>Node pointer.</summary>
        public Node Node;
    }

    /// <summary>A child node is about to be removed from a parent node. Note that individual component removal events will not be sent.</summary>
    public struct NodeRemovedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Parent;
        /// <summary>Node pointer.</summary>
        public Node Node;
    }

    /// <summary>A component has been created to a node.</summary>
    public struct ComponentAddedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Component pointer.</summary>
        public Component Component;
    }

    /// <summary>A component is about to be removed from a node.</summary>
    public struct ComponentRemovedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Component pointer.</summary>
        public Component Component;
    }

    /// <summary>A node's name has changed.</summary>
    public struct NodeNameChangedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
    }

    /// <summary>A node's enabled state has changed.</summary>
    public struct NodeEnabledChangedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
    }

    /// <summary>A node's tag has been added.</summary>
    public struct NodeTagAddedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>String tag.</summary>
        public string Tag;
    }

    /// <summary>A node's tag has been removed.</summary>
    public struct NodeTagRemovedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>String tag.</summary>
        public string Tag;
    }

    /// <summary>A component's enabled state has changed.</summary>
    public struct ComponentEnabledChangedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Component pointer.</summary>
        public Component Component;
    }

    /// <summary>A serializable's temporary state has changed.</summary>
    public struct TemporaryChangedEvent
    {
        /// <summary>Serializable pointer.</summary>
        public Serializable Serializable;
    }

    /// <summary>A node (and its children and components) has been cloned.</summary>
    public struct NodeClonedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Node pointer.</summary>
        public Node Node;
        /// <summary>Node pointer.</summary>
        public Node CloneNode;
    }

    /// <summary>A component has been cloned.</summary>
    public struct ComponentClonedEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>Component pointer.</summary>
        public Component Component;
        /// <summary>Component pointer.</summary>
        public Component CloneComponent;
    }

    /// <summary>World origin is about to be updated.</summary>
    public struct WorldOriginUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>IntVector3.</summary>
        public IntVector3 OldOrigin;
        /// <summary>IntVector3.</summary>
        public IntVector3 NewOrigin;
        /// <summary>IntVector3.</summary>
        public IntVector3 Delta;
    }

    /// <summary>World origin was updated.</summary>
    public struct WorldOriginPostUpdateEvent
    {
        /// <summary>Scene pointer.</summary>
        public Scene Scene;
        /// <summary>IntVector3.</summary>
        public IntVector3 OldOrigin;
        /// <summary>IntVector3.</summary>
        public IntVector3 NewOrigin;
        /// <summary>IntVector3.</summary>
        public IntVector3 Delta;
    }
}
