// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Graphics2D;

using RedBolt.Core;

/// <summary>Entry point for the 2D graphics library.</summary>
public static class Graphics2D
{
    public static void RegisterGraphics2DLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RegisterAssembly(typeof(Graphics2D).Assembly);
    }
}
