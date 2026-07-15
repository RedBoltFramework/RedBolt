namespace RedBolt;

// Non-allowed divergence: ISceneUpdateListener does not exist in C++ Scene.
// C++ uses the event system (URHO3D_EVENT) for scene update callbacks
// (E_SCENEUPDATE, E_SCENEPOSTUPDATE, etc.) rather than an interface.
internal interface ISceneUpdateListener
{
    void OnSceneUpdate(float timeStep);

    void OnScenePostUpdate(float timeStep);
}
