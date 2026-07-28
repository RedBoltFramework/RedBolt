using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Audio;

/// <summary>Sound listener component for 3D audio positioning.</summary>
[Reflected(ObjectCategory.Category_Audio)]
[Serialized(
    "Is Enabled",
    typeof(bool),
    DefaultSource = nameof(GetIsEnabledAttributeDefault),
    Getter = nameof(Enabled),
    Setter = nameof(SetEnabled)
)]
public sealed partial class SoundListener : Component
{
    public SoundListener()
    {
    }



    private static object? GetIsEnabledAttributeDefault() => true;
}
