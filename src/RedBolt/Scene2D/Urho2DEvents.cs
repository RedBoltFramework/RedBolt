// Copyright (c) 2008-2022 the Urho3D project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Scene;

public static class Urho2DEvents
{
    public struct ParticlesEndEvent { public Node Node; public ParticleEffect2D Effect; }
    public struct ParticlesDurationEvent { public Node Node; public ParticleEffect2D Effect; }
}
