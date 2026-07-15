namespace RedBolt;

/// <summary>Sound listener component for 3D audio positioning.</summary>
public sealed class SoundListener : Component
{
    public SoundListener()
    {
    }

    // TODO: port - C++ code:
    // static void RegisterObject(Context* context)
    // {
    //     context->AddFactoryReflection<SoundListener>(Category_Audio);
    //     URHO3D_ACCESSOR_ATTRIBUTE("Is Enabled", IsEnabled, SetEnabled, bool, true, AM_DEFAULT);
    // }
}
