// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.RenderAPI;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;

/// <summary>Shared storage and vertex encoding contract for HarfBuzz GPU outlines.</summary>
internal sealed class HarfBuzzGpuAtlas : IDisposable
{
    public const int Width = 512;
    public const int InitialHeight = 8;
    public const int MaximumHeight = 512;
    public const int TexelSize = 8;
    public const int MaximumTexels = Width * MaximumHeight;
    public static ReadOnlySpan<byte> Signature => "RBHBGPU1"u8;

    private byte[] _data = new byte[Width * InitialHeight * TexelSize];
    private Texture2D? _texture;
    private int _uploadedTexels;

    public HarfBuzzGpuAtlas()
    {
        Signature.CopyTo(_data);
    }

    public int Height { get; private set; } = InitialHeight;
    public int UsedTexels { get; private set; } = 1;
    public ReadOnlySpan<byte> Data => _data;

    // Reflection-friendly hooks keep the atlas contract testable without making it public API.
    internal byte[] DataForTests => _data;
    internal bool TryAppendForTests(byte[] blob, out int offset) => TryAppend(blob, out offset);

    public bool TryAppend(ReadOnlySpan<byte> blob, out int offset)
    {
        offset = 0;
        if (blob.IsEmpty || blob.Length % TexelSize != 0)
            return false;

        var numTexels = blob.Length / TexelSize;
        if (numTexels > MaximumTexels - UsedTexels)
            return false;

        var requiredTexels = UsedTexels + numTexels;
        var requiredHeight = Height;
        while (requiredHeight * Width < requiredTexels)
            requiredHeight *= 2;
        if (requiredHeight > MaximumHeight)
            return false;

        if (requiredHeight != Height)
        {
            Array.Resize(ref _data, requiredHeight * Width * TexelSize);
            Height = requiredHeight;
        }

        offset = UsedTexels;
        blob.CopyTo(_data.AsSpan(offset * TexelSize));
        UsedTexels = requiredTexels;
        return true;
    }

    public Texture2D? GetTexture()
    {
        if (Context.Instance.GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null)
            return null;

        if (_texture is null || _texture.Height != Height)
        {
            if (_texture is null)
            {
                _texture = new Texture2D();
                _texture.SetNumLevels(1);
                _texture.SetFilterMode(TextureFilterMode.Nearest);
                _texture.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
                _texture.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
            }
            if (!_texture.SetSize(Width, Height, TextureFormat.TexFormatRgba16Sint))
            {
                _texture.Dispose();
                _texture = null;
                return null;
            }
            if (!_texture.SetData(0, 0, 0, Width, Height, _data))
                return ReleaseFailedTexture();
            _uploadedTexels = UsedTexels;
            return _texture;
        }

        if (_texture.IsDataLost())
        {
            if (!_texture.SetData(0, 0, 0, Width, Height, _data))
                return ReleaseFailedTexture();
            _texture.ClearDataLost();
            _uploadedTexels = UsedTexels;
            return _texture;
        }

        while (_uploadedTexels < UsedTexels)
        {
            var row = _uploadedTexels / Width;
            var x = _uploadedTexels % Width;
            var count = global::System.Math.Min(UsedTexels - _uploadedTexels, Width - x);
            if (!_texture.SetData(0, x, row, count, 1,
                _data.AsSpan(_uploadedTexels * TexelSize, count * TexelSize)))
            {
                return ReleaseFailedTexture();
            }
            _uploadedTexels += count;
        }
        return _texture;
    }

    private Texture2D? ReleaseFailedTexture()
    {
        _texture?.Dispose();
        _texture = null;
        _uploadedTexels = 0;
        return null;
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
    }
}

internal static class HarfBuzzGpuUv
{
    private const float LocalBias = 0.25f;
    private const float LocalScale = 0.5f;

    public static Vector2f Pack(int atlasOffset, float localX, float localY)
    {
        if ((uint)atlasOffset >= HarfBuzzGpuAtlas.MaximumTexels)
            throw new ArgumentOutOfRangeException(nameof(atlasOffset));
        return new Vector2f(
            2 + (atlasOffset & 0x1ff) + LocalBias + LocalScale * localX,
            2 + (atlasOffset >> 9) + LocalBias + LocalScale * localY);
    }

    public static int UnpackOffset(Vector2f uv) =>
        checked((int)MathF.Floor(uv.X) - 2 + ((int)MathF.Floor(uv.Y) - 2) * HarfBuzzGpuAtlas.Width);

    public static Vector2f UnpackLocal(Vector2f uv) =>
        new((uv.X - MathF.Floor(uv.X) - LocalBias) / LocalScale,
            (uv.Y - MathF.Floor(uv.Y) - LocalBias) / LocalScale);

    public static float GetPadding(float dimension) => MathF.Min(1.0f, MathF.Abs(dimension) * 0.25f);
}
