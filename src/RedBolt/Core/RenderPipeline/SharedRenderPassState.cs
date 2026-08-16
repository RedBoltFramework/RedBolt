// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using RedBolt.Container;
using RedBolt.Graphics;

/// <summary>
/// State of render pipeline that can be accessed by render path and render passes.
/// </summary>
public sealed class SharedRenderPassState : IDisposable
{
    public const int MaxRenderBuffers = 128;

    public static readonly StringHash AlbedoBufferId = "GeometryBuffer.Albedo";
    public static readonly StringHash SpecularBufferId = "GeometryBuffer.Specular";
    public static readonly StringHash NormalBufferId = "GeometryBuffer.Normal";

    public IRenderPipelineInterface? RenderPipelineInterface { get; set; }

    public WeakReference<Camera>? RenderCamera { get; set; }

    public RenderBufferManager? RenderBufferManager { get; set; }

    public IDictionary<StringHash, RenderBuffer?> RenderBuffers { get; } = new Dictionary<StringHash, RenderBuffer?>(MaxRenderBuffers);

    public void Dispose()
    {
        RenderBuffers.Clear();
        RenderBufferManager = null;
        RenderCamera = null;
        RenderPipelineInterface = null;
    }
}
