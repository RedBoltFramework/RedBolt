//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.RmlUI;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.RmlUi;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;

public class RmlRenderer : RenderInterfaceCompatibility
{
    private sealed class EventReceiver : RedObject
    {
    }

    /// <summary>Internal vertex type used to render RmlUI geometry.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RmlVertex
    {
        public Vector3f Position;
        public uint Color;
        public Vector2f TexCoord;
    }

    /// <summary>Internal RmlUI texture holder.</summary>
    private sealed class CachedRmlTexture : IDisposable
    {
        public Image? Image;
        public Texture2D? Texture;

        public void Dispose()
        {
            if (Image is not null)
            {
                Texture?.Dispose();
                Image.Dispose();
            }
            Image = null;
            Texture = null;
        }
    }

    /// <summary>Wrap CachedRmlTexture pointer to RmlUI handle.</summary>
    private static nuint WrapTextureHandle(CachedRmlTexture texture) =>
        (nuint)GCHandle.ToIntPtr(GCHandle.Alloc(texture));

    /// <summary>Unwrap RmlUI handle to CachedRmlTexture pointer.</summary>
    private static CachedRmlTexture? UnwrapTextureHandle(nuint texture) =>
        texture == 0 ? null : (CachedRmlTexture?)GCHandle.FromIntPtr((nint)texture).Target;

    /// <summary>Roughly transform scissor rect.</summary>
    private static IntRect TransformScissorRect(IntRect rect, Matrix3x4 transform)
    {
        Span<Vector3f> corners = stackalloc Vector3f[4]
        {
            new(rect.Left, rect.Top, 0.0f),
            new(rect.Right, rect.Top, 0.0f),
            new(rect.Right, rect.Bottom, 0.0f),
            new(rect.Left, rect.Bottom, 0.0f)
        };

        var minCorner = new Vector2i(int.MaxValue, int.MaxValue);
        var maxCorner = new Vector2i(int.MinValue, int.MinValue);
        foreach (var corner in corners)
        {
            var transformedCorner = Vector2f.RoundToInt((transform * corner).ToVector2());
            minCorner.X = System.Math.Min(minCorner.X, transformedCorner.X);
            minCorner.Y = System.Math.Min(minCorner.Y, transformedCorner.Y);
            maxCorner.X = System.Math.Max(maxCorner.X, transformedCorner.X);
            maxCorner.Y = System.Math.Max(maxCorner.Y, transformedCorner.Y);
        }
        return new IntRect(minCorner, maxCorner);
    }

    private readonly EventReceiver _eventReceiver;

    /// <summary>Default materials.</summary>
    private Material? _noTextureMaterial;
    private Material? _alphaMapMaterial;
    private Material? _diffMapMaterial;

    /// <summary>Cached between frames and calls.</summary>
    private DefaultUIBatchStateCache? _batchStateCache;
    private DynamicVertexBuffer? _vertexBuffer;
    private DynamicIndexBuffer? _indexBuffer;

    /// <summary>Constant between BeginRendering/EndRendering.</summary>
    private UIBatchStateCreateContext _batchStateCreateContext;
    private bool _flipRect;
    private bool _isRenderSurfaceSRGB;
    private Vector2i _viewportSize;
    private DrawCommandQueue? _drawQueue;
    private readonly List<Texture2D> _textures = [];
    private Matrix4 _projection;

    private bool _scissorEnabled;
    private IntRect _scissor;

    private bool _transformEnabled;
    private Matrix3x4 _transform = Matrix3x4.Identity;

    public RmlRenderer()
    {
        _eventReceiver = new EventReceiver();
        InitializeGraphics();
        Context.Instance.SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            _eventReceiver, (ref GraphicsEvents.ScreenModeEvent _) => InitializeGraphics());
    }

    public void BeginRendering()
    {
        var renderDevice = Context.Instance.GetSubsystem<RenderDevice>()!;
        var renderContext = renderDevice.GetRenderContext();

        _drawQueue = renderDevice.GetDefaultQueue();
        _vertexBuffer!.Discard();
        _indexBuffer!.Discard();
        _drawQueue.Reset();
        _textures.Clear();

        var vertexBuffer = _vertexBuffer.GetVertexBuffer();
        var indexBuffer = _indexBuffer.GetIndexBuffer();

        RawVertexBufferArray vertexBuffers = default;
        vertexBuffers[0] = vertexBuffer.GetRawBuffer();
        _drawQueue.SetVertexBuffers(vertexBuffers);
        _drawQueue.SetIndexBuffer(indexBuffer.GetRawBuffer());

        _batchStateCreateContext.VertexBuffer = vertexBuffer;
        _batchStateCreateContext.IndexBuffer = indexBuffer;

        var backend = renderDevice.GetBackend();
        var outputDesc = renderContext.GetCurrentRenderTargetsDesc();
        var isSwapChain = renderContext.IsSwapChainRenderTarget();
        _isRenderSurfaceSRGB = RenderAPIUtils.IsTextureFormatSRGB(outputDesc.RenderTargetFormats[0]);
        _viewportSize = renderContext.GetCurrentViewport().Size;

        // On OpenGL, flip the projection if rendering to a texture so that the texture can be addressed in the
        // same way as a render texture produced on Direct3D.
        _flipRect = !isSwapChain && backend == RenderBackend.OpenGL;

        var invScreenSize = new Vector2f(1.0f / _viewportSize.X, 1.0f / _viewportSize.Y);
        var scale = new Vector2f(2.0f * invScreenSize.X, -2.0f * invScreenSize.Y);
        var offset = new Vector2f(-1.0f, 1.0f);
        if (_flipRect)
        {
            offset.Y = -offset.Y;
            scale.Y = -scale.Y;
        }

        const float FarClip = 1000.0f;
        _projection = Matrix4.Identity;
        _projection.M00 = scale.X;
        _projection.M03 = offset.X;
        _projection.M11 = scale.Y;
        _projection.M13 = offset.Y;
        _projection.M22 = 1.0f / FarClip;
        _projection.M23 = 0.0f;
        _projection.M33 = 1.0f;
    }

    public void EndRendering()
    {
        var renderDevice = Context.Instance.GetSubsystem<RenderDevice>()!;
        var renderContext = renderDevice.GetRenderContext();
        using var renderScope = RenderScope.Acquire(renderContext, "RmlRenderer::EndRendering");

        _vertexBuffer!.Commit();
        _indexBuffer!.Commit();
        renderContext.Execute(_drawQueue!);
        _drawQueue = null;
    }

    public override unsafe bool GenerateTexture(nint textureHandle, nint source, nint sourceDimensions)
    {
        var size = *(RmlUiLegacyVector2i*)sourceDimensions;
        var image = new Image();
        image.SetSize(size.X, size.Y, 4);
        new ReadOnlySpan<byte>((void*)source, checked(size.X * size.Y * 4)).CopyTo(image.GetData());

        var texture = new Texture2D();
        texture.SetData(image);

        var cachedTexture = new CachedRmlTexture { Image = image, Texture = texture };
        *(nuint*)textureHandle = WrapTextureHandle(cachedTexture);
        return true;
    }

    public override void ReleaseTexture(nuint textureHandle)
    {
        if (textureHandle == 0)
            return;
        var cachedTexture = UnwrapTextureHandle(textureHandle);
        cachedTexture?.Dispose();
        GCHandle.FromIntPtr((nint)textureHandle).Free();
    }

    public override unsafe bool LoadTexture(nint textureHandle, nint textureDimensions, Std.String source)
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var texture = cache.GetResourceOrNull<Texture2D>(source.ToString());
        if (texture is not null)
        {
            var size = (RmlUiLegacyVector2i*)textureDimensions;
            size->X = texture.Width;
            size->Y = texture.Height;
        }
        var cachedTexture = new CachedRmlTexture { Texture = texture };
        *(nuint*)textureHandle = WrapTextureHandle(cachedTexture);
        return true;
    }

    public override unsafe void RenderGeometry(
        nint vertices, int numVertices, nint indices, int numIndices, nuint textureHandle, nint translation)
    {
        var renderDevice = Context.Instance.GetSubsystem<RenderDevice>()!;
        var renderContext = renderDevice.GetRenderContext();

        var (firstVertex, vertexData) = _vertexBuffer!.AddVertices((uint)numVertices);
        var (firstIndex, indexData) = _indexBuffer!.AddIndices((uint)numIndices);

        var sourceVertices = new ReadOnlySpan<RmlUiLegacyVertex>((void*)vertices, numVertices);
        var destVertices = MemoryMarshal.Cast<byte, RmlVertex>(vertexData.Span);
        var offset = *(RmlUiLegacyVector2f*)translation;
        for (var i = 0; i < numVertices; ++i)
        {
            destVertices[i].Position.X = sourceVertices[i].Position.X + offset.X;
            destVertices[i].Position.Y = sourceVertices[i].Position.Y + offset.Y;
            destVertices[i].Position.Z = 0.0f;
            destVertices[i].Color = sourceVertices[i].Colour;
            destVertices[i].TexCoord.X = sourceVertices[i].TexCoord.X;
            destVertices[i].TexCoord.Y = sourceVertices[i].TexCoord.Y;
        }

        var sourceIndices = new ReadOnlySpan<int>((void*)indices, numIndices);
        var destIndices = MemoryMarshal.Cast<byte, uint>(indexData.Span);
        for (var i = 0; i < numIndices; ++i)
            destIndices[i] = unchecked((uint)sourceIndices[i] + firstVertex);

        // Restore texture data if lost
        var cachedTexture = UnwrapTextureHandle(textureHandle);
        var texture = cachedTexture?.Texture;
        if (texture is not null && texture.IsDataLost())
        {
            texture.SetData(cachedTexture!.Image);
            texture.ClearDataLost();
        }

        var material = GetBatchMaterial(texture);
        var pass = material.GetDefaultPass()!;

        var samplerStateHash = texture?.GetSamplerStateDesc().ToHash() ?? 0;
        _batchStateCreateContext.DefaultSampler = texture?.GetSamplerStateDesc();

        var batchStateKey = new UIBatchStateKey(_isRenderSurfaceSRGB,
            renderContext.GetCurrentRenderTargetsDesc(), material, pass, BlendMode.Alpha, samplerStateHash);
        var pipelineState = _batchStateCache!.GetOrCreatePipelineState(batchStateKey, _batchStateCreateContext);

        IntRect scissor;
        if (!_scissorEnabled)
            scissor = new IntRect(new Vector2i(0, 0), _viewportSize);
        else if (_transformEnabled)
            scissor = TransformScissorRect(_scissor, _transform);
        else
            scissor = _scissor;

        _drawQueue!.SetScissorRect(scissor);
        _drawQueue.SetPipelineState(pipelineState!);

        if (texture is not null)
        {
            GraphicsUtils.AddShaderResource(_drawQueue, ShaderResources.Albedo, texture);
            _textures.Add(texture);
        }
        _drawQueue.CommitShaderResources();

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Camera))
        {
            _drawQueue.AddShaderParameter(GraphicsDefs.VspViewProj, _projection);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Camera);
        }

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Material))
        {
            _drawQueue.AddShaderParameter(GraphicsDefs.PspMatDiffColor, Color.White.ToVector4());
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Material);
        }

        if (_drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Object, true))
        {
            _drawQueue.AddShaderParameter(GraphicsDefs.VspModel, _transform);
            _drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Object);
        }

        _drawQueue.DrawIndexed((int)firstIndex, numIndices);
    }

    public override void EnableScissorRegion(bool enable)
    {
        _scissorEnabled = enable;
    }

    public override void SetScissorRegion(int x, int y, int width, int height)
    {
        _scissor.Left = x;
        _scissor.Top = y;
        _scissor.Bottom = y + height;
        _scissor.Right = x + width;

        if (_flipRect)
        {
            var top = _scissor.Top;
            var bottom = _scissor.Bottom;
            _scissor.Top = _viewportSize.Y - bottom;
            _scissor.Bottom = _viewportSize.Y - top;
        }

        // TODO: Support transformed scissors by doing scissor test on CPU
    }

    public override unsafe void SetTransform(nint transform)
    {
        _transformEnabled = transform != 0;
        _transform = transform != 0
            ? new Matrix3x4(new Matrix4(new ReadOnlySpan<float>((void*)transform, 16)))
            : Matrix3x4.Identity;
    }

    /// <summary>Perform initialization tasks that require graphics subsystem.</summary>
    private void InitializeGraphics()
    {
        var renderDevice = Context.Instance.GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        _batchStateCache?.Dispose();
        _batchStateCache = new DefaultUIBatchStateCache();

        _vertexBuffer?.Dispose();
        _vertexBuffer = new DynamicVertexBuffer();
        _vertexBuffer.Initialize(1024,
            VertexBuffer.GetElements((uint)(VertexMask.Position | VertexMask.Color | VertexMask.TexCoord1)));
        _indexBuffer?.Dispose();
        _indexBuffer = new DynamicIndexBuffer();
        _indexBuffer.Initialize(1024, true);

        const string BaseDefines = "VERTEXCOLOR ";
        const string AlphaMapDefines = BaseDefines + "ALPHAMAP ";
        const string DiffMapDefines = BaseDefines + "DIFFMAP ";

        _noTextureMaterial?.Dispose();
        _noTextureMaterial = Material.CreateBaseMaterial("v2/X_Basic", BaseDefines, BaseDefines);
        _alphaMapMaterial?.Dispose();
        _alphaMapMaterial = Material.CreateBaseMaterial("v2/X_Basic", AlphaMapDefines, AlphaMapDefines);
        _diffMapMaterial?.Dispose();
        _diffMapMaterial = Material.CreateBaseMaterial("v2/X_Basic", DiffMapDefines, DiffMapDefines);
    }

    private Material GetBatchMaterial(Texture2D? texture)
    {
        if (texture is null)
            return _noTextureMaterial!;
        else if (texture.Format == TextureFormat.TexFormatR8Unorm)
            return _alphaMapMaterial!;
        else
            return _diffMapMaterial!;
    }

    protected override bool ReleaseNativeHandle(nint handle)
    {
        _textures.Clear();
        _indexBuffer?.Dispose();
        _vertexBuffer?.Dispose();
        _batchStateCache?.Dispose();
        _diffMapMaterial?.Dispose();
        _alphaMapMaterial?.Dispose();
        _noTextureMaterial?.Dispose();
        _eventReceiver.Dispose();
        return base.ReleaseNativeHandle(handle);
    }
}
