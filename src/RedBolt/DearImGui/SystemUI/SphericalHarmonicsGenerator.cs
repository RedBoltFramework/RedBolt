//
// Copyright (c) 2022-2022 the rbfx project.
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

namespace RedBolt.SystemUI;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using ShaderType = RedBolt.RenderAPI.ShaderType;

/// <summary>Generates spherical harmonics for cube textures. Slow.</summary>
/// <remarks>TODO: Move to dedicated file.</remarks>
public class SphericalHarmonicsGenerator : RedObject
{
    public SphericalHarmonicsGenerator()
    {
        var renderer = GetSubsystem<Renderer>();
        _quadGeometry = renderer.GetQuadGeometry();
    }

    public override void Dispose()
    {
        _tempTextureData = [];
        _tempTexture?.Dispose();
        _tempTexture = null;
        _copyTexturePipelineState = null;
        _quadGeometry = null;
        base.Dispose();
    }

    public SphericalHarmonicsColor9 Generate(TextureCube texture)
    {
        if (_copyTexturePipelineState is null)
            InitializePipelineStates();
        if (_tempTexture is null)
            InitializeTextures();

        if (_copyTexturePipelineState is null || !_copyTexturePipelineState.IsValid || _tempTexture is null)
            return default;

        var renderDevice = GetSubsystem<RenderDevice>();
        var renderContext = renderDevice.GetRenderContext();
        var drawQueue = renderDevice.GetDefaultQueue();

        var faceViews = new ITextureView?[(int)CubeMapFace.MaxCubeMapFaces];
        try
        {
            for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces;
                 face = (CubeMapFace)((int)face + 1))
            {
                var view = CreateCubemapView(texture, face);
                if (view is null)
                    return default;
                faceViews[(int)face] = view;
            }

            using var renderScope = RenderScope.Acquire(renderContext, "SphericalHarmonicsGenerator::Generate");
            ReadOnlySpan<RenderTargetView> renderTargets = [RenderTargetView.Texture(_tempTexture)];
            for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces;
                 face = (CubeMapFace)((int)face + 1))
            {
                var viewportOffset = new Vector2i(0, checked((int)(_textureSize * (uint)face)));
                var viewportSize = Vector2i.One * checked((int)_textureSize);

                renderContext.SetRenderTargets(null, renderTargets);
                renderContext.SetViewport(new IntRect(viewportOffset, viewportOffset + viewportSize));

                drawQueue.Reset();
                drawQueue.SetPipelineState(_copyTexturePipelineState);

                drawQueue.AddShaderResource("Albedo", faceViews[(int)face]!);
                drawQueue.CommitShaderResources();

                GraphicsUtils.SetBuffersFromGeometry(drawQueue, _quadGeometry!);

                drawQueue.DrawIndexed(
                    checked((int)_quadGeometry!.GetIndexStart()), checked((int)_quadGeometry.GetIndexCount()));
                renderContext.Execute(drawQueue);
            }

            _tempTexture.GetRawTexture().Read(
                0, 0, MemoryMarshal.AsBytes(_tempTextureData.AsSpan()));
        }
        finally
        {
            foreach (var view in faceViews)
            {
                if (view is { IsNull: false })
                    _ = view.Release();
            }
        }

        var result = new SphericalHarmonicsColor9();
        var weightSum = 0.0f;

        var isGammaSpace = !texture.GetSRGB() && !texture.GetLinear();
        var textureWidth = (float)_textureSize;
        for (var i = CubeMapFace.PositiveX; i < CubeMapFace.MaxCubeMapFaces;
             i = (CubeMapFace)((int)i + 1))
        {
            for (uint y = 0; y < _textureSize; ++y)
            {
                for (uint x = 0; x < _textureSize; ++x)
                {
                    var rawSample = _tempTextureData[checked((int)(x + (y + (uint)i * _textureSize) * _textureSize))];
                    var sample = isGammaSpace
                        ? new Color(rawSample).GammaToLinear().ToVector3()
                        : rawSample.ToVector3();
                    var uv = new Vector2f((x + 0.5f) / textureWidth, (y + 0.5f) / textureWidth);
                    var offset = ImageCube.ProjectUVOnCube(i, uv);
                    var distance = offset.Length;
                    var weight = 1.0f / (distance * distance * distance);
                    var direction = offset / distance;

                    result += new SphericalHarmonicsColor9(direction, sample) * weight;
                    weightSum += weight;
                }
            }
        }

        result *= 4.0f * MathF.PI / weightSum;
        return result;
    }

    public void Generate(TextureCube texture, XMLFile imageXml)
    {
        var rawSH = Generate(texture);
        var packedSH = new SphericalHarmonicsDot9(rawSH);

        using var rootElement = imageXml.GetRoot();
        while (rootElement.RemoveChild("sh"))
        {
        }

        using var shElement = rootElement.CreateChild("sh");
        shElement.SetVector4("ar", packedSH.Ar);
        shElement.SetVector4("ag", packedSH.Ag);
        shElement.SetVector4("ab", packedSH.Ab);
        shElement.SetVector4("br", packedSH.Br);
        shElement.SetVector4("bg", packedSH.Bg);
        shElement.SetVector4("bb", packedSH.Bb);
        shElement.SetVector4("c", packedSH.C);

        imageXml.SaveFile(new FileIdentifier(imageXml.GetAbsoluteFileName()));
    }

    private static ITextureView? CreateCubemapView(TextureCube texture, CubeMapFace face)
    {
        using var name = new CString($"{texture.GetName()}: face #{(int)face}");
        using var viewDesc = new TextureViewDesc
        {
            Name = name.Pointer,
            ViewType = TextureViewType.TextureViewShaderResource,
            TextureDim = ResourceDimension.ResourceDimTex2d,
            Format = texture.Format,
            FirstArraySlice = (uint)face,
            NumArraySlices = 1
        };

        texture.GetRawTexture().GetHandles().Texture!.CreateView(viewDesc, out var view);
        if (view is null || view.IsNull)
        {
            Log.GetLogger().LogError("Failed to create UAV for texture");
            return null;
        }

        return view;
    }

    private void InitializePipelineStates()
    {
        var graphics = GetSubsystem<Graphics>();
        var renderer = GetSubsystem<Renderer>();
        var pipelineStateCache = GetSubsystem<PipelineStateCache>();

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = "Copy texture",
            VertexShader = graphics.GetShader(ShaderType.VS, "v2/X_CopyTexture", ""),
            PixelShader = graphics.GetShader(ShaderType.PS, "v2/X_CopyTexture", ""),
            ColorWriteEnabled = true
        };
        // Managed Diligent requires the render-target layout before the first draw.
        desc.Output.NumRenderTargets = 1;
        desc.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba32Float;
        desc.Samplers.Add("Albedo", SamplerStateDesc.Trilinear());

        var quadGeometry = renderer.GetQuadGeometry()!;
        GraphicsUtils.InitializeInputLayoutAndPrimitiveType(ref desc, quadGeometry);

        _copyTexturePipelineState = pipelineStateCache.GetGraphicsPipelineState(desc);
    }

    private void InitializeTextures()
    {
        _tempTexture = new Texture2D();
        _tempTexture.SetSize(
            checked((int)_textureSize),
            checked((int)(_textureSize * (uint)CubeMapFace.MaxCubeMapFaces)),
            TextureFormat.TexFormatRgba32Float,
            TextureFlag.BindRenderTarget);

        _tempTextureData = new Vector4f[checked((int)(_textureSize * _textureSize *
            (uint)CubeMapFace.MaxCubeMapFaces))];
    }

    private readonly uint _textureSize = 8;

    private Geometry? _quadGeometry;
    private PipelineState? _copyTexturePipelineState;
    private Texture2D? _tempTexture;
    private Vector4f[] _tempTextureData = [];
}
