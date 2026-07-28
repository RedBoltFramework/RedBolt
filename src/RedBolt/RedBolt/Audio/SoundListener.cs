using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Audio;

/// <summary>Sound listener component for 3D audio positioning.</summary>
[Reflected(ObjectCategory.Category_Audio)]
public sealed partial class SoundListener : Component
{
    public SoundListener()
    {
    }

    private static object? GetIsEnabledAttributeDefault() => true;


    [Serialized(
        "Is Enabled",
        DefaultSource = nameof(GetIsEnabledAttributeDefault)
    )]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
