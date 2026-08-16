// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Audio;

using RedBolt.Core;
using RedBolt.Scene;

/// <summary>Sound listener component for 3D audio positioning.</summary>
[Reflected(ObjectCategory.Category_Audio)]
public sealed partial class SoundListener : Component
{
    public SoundListener()
    {
    }

    private static object? GetIsEnabledAttributeDefault() => true;


    [Serialized("Is Enabled", DefaultSource = nameof(GetIsEnabledAttributeDefault))]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
