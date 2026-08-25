// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Bindings.DearImGui;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.Diligent;
using RedBolt.Graphics;
using RedBolt.Math;

public static partial class ImGui
{
    public static void Image(Texture2D texture, in Vector2f imageSize)
    {
        Image(ImGuiBackendReferenceTexture(texture), in imageSize);
    }

    public static void Image(Texture2D texture, in Vector2f imageSize, in Vector2f? uv0, in Vector2f? uv1 = default)
    {
        Image(ImGuiBackendReferenceTexture(texture), in imageSize, in uv0, in uv1);
    }

    public static void ImageWithBg(Texture2D texture, in Vector2f imageSize)
    {
        ImageWithBg(ImGuiBackendReferenceTexture(texture), in imageSize);
    }

    public static void ImageWithBg(Texture2D texture, in Vector2f imageSize,
        in Vector2f? uv0, in Vector2f? uv1 = default, in Vector4f? backgroundColor = default,
        in Vector4f? tintColor = default)
    {
        ImageWithBg(ImGuiBackendReferenceTexture(texture), in imageSize,
            in uv0, in uv1, in backgroundColor, in tintColor);
    }

    public static bool ImageButton(ReadOnlySpan<byte> id, Texture2D texture, in Vector2f imageSize)
    {
        return ImageButton(id, ImGuiBackendReferenceTexture(texture), in imageSize);
    }

    public static bool ImageButton(ReadOnlySpan<byte> id, Texture2D texture, in Vector2f imageSize,
        in Vector2f? uv0, in Vector2f? uv1 = default, in Vector4f? backgroundColor = default,
        in Vector4f? tintColor = default)
    {
        return ImageButton(id, ImGuiBackendReferenceTexture(texture), in imageSize,
            in uv0, in uv1, in backgroundColor, in tintColor);
    }

    internal static ImTextureRef ImGuiBackendReferenceTexture(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        var rawTexture = texture.GetRawTexture()
            ?? throw new InvalidOperationException("The Texture2D has no GPU texture.");
        var textureView = rawTexture.GetHandles().ShaderResourceView;
        if (textureView is null || textureView.IsNull)
            throw new InvalidOperationException("The Texture2D has no shader resource view.");

        _ = textureView.AddRef();
        ReferencedTextures.Add(textureView);

        TextureReference ??= new ImTextureRef();
        TextureReference.TexID = (ulong)(nuint)textureView.Handle;
        return TextureReference;
    }

    internal static void ImGuiBackendClearReferencedTextures()
    {
        for (var i = ReferencedTextures.Count - 1; i >= 0; --i)
            _ = ReferencedTextures[i].Release();
        ReferencedTextures.Clear();
    }

    internal static void ImGuiBackendShutdownTextureOverloads()
    {
        ImGuiBackendClearReferencedTextures();
        TextureReference?.Dispose();
        TextureReference = null;
    }

    private static readonly List<ITextureView> ReferencedTextures = [];
    private static ImTextureRef? TextureReference;
}
