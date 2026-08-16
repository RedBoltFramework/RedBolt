// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using RedBolt.Scene;

public static class CoreEvents
{
    /// <summary>Frame begin event.</summary>
    public struct BeginFrameEvent
    {
        public uint FrameNumber;
        public float TimeStep;
    }

    /// <summary>Pre-update event that indicates that both local and network input has been processed.</summary>
    public struct InputReadyEvent
    {
        public float TimeStep;
    }

    /// <summary>Application-wide logic update event.</summary>
    public struct UpdateEvent
    {
        public float TimeStep;
    }

    /// <summary>Application-wide logic post-update event.</summary>
    public struct PostUpdateEvent
    {
        public float TimeStep;
    }

    /// <summary>Render update event.</summary>
    public struct RenderUpdateEvent
    {
        public float TimeStep;
    }

    /// <summary>Post-render update event.</summary>
    public struct PostRenderUpdateEvent
    {
        public float TimeStep;
    }

    /// <summary>Frame end event.</summary>
    public struct EndFrameEvent
    {
    }

    /// <summary>Frame end event, only for tools and testing.</summary>
    public struct EndFramePrivateEvent
    {
    }
}
