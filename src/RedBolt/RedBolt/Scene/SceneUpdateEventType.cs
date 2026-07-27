namespace RedBolt;

public abstract class SceneUpdateEventType
{
    public abstract Type Type { get; }

    public static SceneUpdateEventType Get<T>()
        where T : struct, SceneEvents.ISceneUpdateEvent
    {
        return Implementation<T>.Instance;
    }

    internal abstract void Subscribe(LogicComponent receiver, Scene scene, Action<float> handler);

    private sealed class Implementation<T> : SceneUpdateEventType
        where T : struct, SceneEvents.ISceneUpdateEvent
    {
        public static readonly Implementation<T> Instance = new();

        public override Type Type => typeof(T);

        internal override void Subscribe(LogicComponent receiver, Scene scene, Action<float> handler)
        {
            Context.Instance.SubscribeToEvent<T>(receiver, scene, (ref T eventData) => handler(eventData.TimeStep));
        }
    }
}
