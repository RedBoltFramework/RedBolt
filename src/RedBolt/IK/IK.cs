// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using RedBolt.Core;

namespace RedBolt.IK;

public static class IK
{
    /// <summary>Registers all IK systems to the specified context.</summary>
    public static void RegisterIKLibrary(Context context)
    {
        context.RegisterAssembly(typeof(IK).Assembly);
    }
}
