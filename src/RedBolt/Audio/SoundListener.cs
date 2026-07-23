namespace RedBolt;

/// <summary>Sound listener component for 3D audio positioning.</summary>
public sealed class SoundListener : Component
{
    public SoundListener()
    {
        RegisterAttribute("Is Enabled", typeof(bool), self => ((SoundListener)self).Enabled,
            (self, value) => ((SoundListener)self).SetEnabled(value is bool enabled && enabled), true);
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<SoundListener>(ObjectCategory.Category_Audio);
    }

}
