namespace RedBolt;

internal interface ISceneUpdateListener
{
    void OnSceneUpdate(float timeStep);

    void OnScenePostUpdate(float timeStep);
}
