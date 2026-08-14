// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics2D;

/// <summary>Entry point for scene-level 2D components.</summary>
public static class Scene2D
{
    public static void RegisterUrho2DLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Graphics2D.RegisterGraphics2DLibrary(context);
        context.RegisterAssembly(typeof(Scene2D).Assembly);
    }
}
