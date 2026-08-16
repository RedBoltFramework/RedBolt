// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using RedBolt.Core;
using RedBolt.Scene;

public static class DrawableEvents
{
    /// <summary>AnimatedModel bone hierarchy created.</summary>
    public struct BoneHierarchyCreatedEvent
    {
        public Node? Node; // Node pointer
    }

    /// <summary>AnimatedModel animation trigger.</summary>
    public struct AnimationTriggerEvent
    {
        public Node? Node; // Node pointer
        public Animation? Animation; // Animation pointer
        public string Name; // String
        public float Time; // Float
        public Variant Data; // User-defined data type
    }

    /// <summary>AnimatedModel animation finished or looped.</summary>
    public struct AnimationFinishedEvent
    {
        public Node? Node; // Node pointer
        public Animation? Animation; // Animation pointer
        public string Name; // String
        public bool Looped; // Bool
    }

    /// <summary>Particle effect finished.</summary>
    public struct ParticleEffectFinishedEvent
    {
        public Node? Node; // Node pointer
        public ParticleEffect? Effect; // ParticleEffect pointer
    }

    /// <summary>Terrain geometry created.</summary>
    public struct TerrainCreatedEvent
    {
        public Node? Node; // Node pointer
    }
}
