// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Core;

/// <summary>Entry point for the 2D physics library.</summary>
public static class Physics2D
{
    /// <summary>Register 2D physics library objects.</summary>
    public static void RegisterPhysics2DLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RegisterAssembly(typeof(Physics2D).Assembly);
    }
}
