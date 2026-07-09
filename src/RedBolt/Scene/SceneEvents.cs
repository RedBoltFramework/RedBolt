namespace RedBolt;

public static class SceneEvents
{
    public struct NodeAddedEvent
    {
        public Scene Scene;
        public Node Parent;
        public Node Node;
    }

    public struct NodeRemovedEvent
    {
        public Scene Scene;
        public Node Parent;
        public Node Node;
    }

    public struct ComponentAddedEvent
    {
        public Scene Scene;
        public Node Node;
        public Component Component;
    }

    public struct ComponentRemovedEvent
    {
        public Scene Scene;
        public Node Node;
        public Component Component;
    }

    public struct NodeClonedEvent
    {
        public Scene Scene;
        public Node Node;
        public Node CloneNode;
    }

    public struct ComponentClonedEvent
    {
        public Scene Scene;
        public Node Node;
        public Component Component;
        public Component CloneComponent;
    }

    public struct NodeNameChangedEvent
    {
        public Scene Scene;
        public Node Node;
    }

    public struct NodeEnabledChangedEvent
    {
        public Scene Scene;
        public Node Node;
    }

    public struct NodeTagAddedEvent
    {
        public Scene Scene;
        public Node Node;
        public string Tag;
    }

    public struct NodeTagRemovedEvent
    {
        public Scene Scene;
        public Node Node;
        public string Tag;
    }

    public struct ComponentEnabledChangedEvent
    {
        public Scene Scene;
        public Node Node;
        public Component Component;
    }

    public struct AttributeAnimationAddedEvent
    {
        public ObjectAnimation ObjectAnimation;
        public string Name;
    }

    public struct AttributeAnimationRemovedEvent
    {
        public ObjectAnimation ObjectAnimation;
        public string Name;
    }

    public struct TemporaryChangedEvent
    {
        public Scene Scene;
        public Serializable Serializable;
    }

    public struct SceneUpdatesChangedEvent
    {
        public Scene Scene;
    }

    public struct SceneForcedUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }

    public struct SceneUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }

    public struct AttributeAnimationUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }

    public struct SceneSubsystemUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }

    public struct ScenePostUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }

    public struct SceneForcedPostUpdateEvent
    {
        public Scene Scene;
        public float TimeStep;
    }
}
