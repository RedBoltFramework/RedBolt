using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Audio;

/// <summary>Sound listener component for 3D audio positioning.</summary>
public sealed class SoundListener : Component
{
    public SoundListener()
    {
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddFactoryReflection<SoundListener>(ObjectCategory.Category_Audio);
        RegisterAttribute(reflection, "Is Enabled", typeof(bool), self => ((SoundListener)self).Enabled,
            (self, value) => ((SoundListener)self).SetEnabled(value is bool enabled && enabled), true);
    }

}
