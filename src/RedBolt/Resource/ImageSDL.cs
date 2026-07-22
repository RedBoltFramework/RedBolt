// C# extraction into separate file uses generated native SDL bindings for the inline C++ functionality.
using RedBolt.Bindings.Sdl;
using System.Runtime.InteropServices;

namespace RedBolt;

internal static partial class ImageSdl
{
    private const int ColorLutSize = 16;

    public static bool TryLoad(Image image, Stream source)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(source);

        if (!TryDecodeRgba(source, out var decoded))
            return false;

        if (!image.SetSize(decoded.Width, decoded.Height, 4))
            return false;

        image.SetData(decoded.Data);
        return true;
    }

    public static bool TryLoadColorLut(Image image, Stream source)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(source);

        if (!TryDecodeRgba(source, out var decoded))
            return false;

        if (decoded.Width < ColorLutSize * ColorLutSize || decoded.Height < ColorLutSize)
            return false;

        if (!image.SetSize(ColorLutSize, ColorLutSize, ColorLutSize, 3))
            return false;

        var destination = image.GetData();
        for (var z = 0; z < ColorLutSize; ++z)
        {
            for (var y = 0; y < ColorLutSize; ++y)
            {
                var sourceOffset = ((y * decoded.Width) + z * ColorLutSize) * 4;
                var destinationOffset = (((z * ColorLutSize) + y) * ColorLutSize) * 3;
                for (var x = 0; x < ColorLutSize; ++x)
                {
                    var pixelOffset = sourceOffset + x * 4;
                    var outputOffset = destinationOffset + x * 3;
                    destination[outputOffset + 0] = decoded.Data[pixelOffset + 0];
                    destination[outputOffset + 1] = decoded.Data[pixelOffset + 1];
                    destination[outputOffset + 2] = decoded.Data[pixelOffset + 2];
                }
            }
        }

        return true;
    }

    public static bool SavePng(Image image, string fileName)
        => SaveEncoded(image, fileName, static (surface, path) => SdlNativeBindings.ImgSavePNG(surface, path));

    public static bool SaveJpg(Image image, string fileName, int quality)
        => SaveEncoded(image, fileName, (surface, path) => SdlNativeBindings.ImgSaveJPG(surface, path, Math.Clamp(quality, 0, 100)));

    public static SdlSurface? GetSdlSurface(Image image, IntRect rect)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Depth > 1 || image.IsCompressed() || image.Components < 3)
            return null;

        var imageRect = rect;
        if (imageRect.Left < 0 || imageRect.Top < 0 || imageRect.Right > image.Width || imageRect.Bottom > image.Height
            || imageRect.Left >= imageRect.Right || imageRect.Top >= imageRect.Bottom)
        {
            imageRect = new IntRect(0, 0, image.Width, image.Height);
        }

        var width = imageRect.Width;
        var height = imageRect.Height;
        var surface = SdlNativeBindings.SdlCreateRGBSurface(0, width, height, (int)image.Components,
            0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
        if (surface.IsNull)
            return null;

        try
        {
            if (SdlNativeBindings.SdlLockSurface(surface) != 0)
            {
                SdlNativeBindings.SdlFreeSurface(surface);
                return null;
            }

            try
            {
                var source = image.GetData();
                var sourceOffset = (int)image.Components * (image.Width * imageRect.Top + imageRect.Left);
                var rowSize = checked((int)image.Components * width);
                for (var y = 0; y < height; ++y)
                {
                    Marshal.Copy(source, sourceOffset + y * (int)(image.Components * image.Width),
                        IntPtr.Add(surface.Pixels, y * surface.Pitch), rowSize);
                }
            }
            finally
            {
                SdlNativeBindings.SdlUnlockSurface(surface);
            }

            return surface;
        }
        catch (DllNotFoundException)
        {
            SdlNativeBindings.SdlFreeSurface(surface);
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            SdlNativeBindings.SdlFreeSurface(surface);
            return null;
        }
    }

    private static bool SaveEncoded(Image image, string fileName, Func<SdlSurface, byte[], int> save)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var rgba = image.Components == 4 ? image : image.ConvertToRGBA();
        var data = rgba.GetData();
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var surface = SdlNativeBindings.SdlCreateRGBSurfaceWithFormatFrom(
                handle.AddrOfPinnedObject(), rgba.Width, rgba.Height, 32, rgba.Width * 4,
                (uint)SdlPixelFormatEnum.SdlPixelformatRgba32);
            if (surface.IsNull)
                return false;
            try
            {
                return save(surface, System.Text.Encoding.UTF8.GetBytes(fileName + "\0")) == 0;
            }
            finally
            {
                SdlNativeBindings.SdlFreeSurface(surface);
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool TryDecodeRgba(Stream source, out DecodedImage decoded)
    {
        decoded = default;

        try
        {
            var bytes = ReadAllBytes(source);
            if (bytes.Length == 0)
                return false;

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var rwops = SdlNativeBindings.SdlRWFromConstMem(handle.AddrOfPinnedObject(), bytes.Length);
                if (rwops.IsNull)
                    return false;

                SdlSurface? surface = null;
                SdlSurface? convertedSurface = null;
                try
                {
                    surface = SdlNativeBindings.ImgLoadRw(rwops, 1);
                    if (surface.IsNull)
                        return false;

                    convertedSurface = SdlNativeBindings.SdlConvertSurfaceFormat(
                        surface,
                        (uint)SdlPixelFormatEnum.SdlPixelformatRgba32,
                        0);
                    if (convertedSurface.IsNull)
                        return false;

                    if (convertedSurface.W <= 0 || convertedSurface.H <= 0 || convertedSurface.Pixels == 0)
                        return false;

                    var pixelData = new byte[checked(convertedSurface.W * convertedSurface.H * 4)];
                    if (SdlNativeBindings.SdlLockSurface(convertedSurface) != 0)
                        return false;

                    try
                    {
                        var rowSize = convertedSurface.W * 4;
                        for (var y = 0; y < convertedSurface.H; ++y)
                        {
                            var sourceOffset = IntPtr.Add(convertedSurface.Pixels, y * convertedSurface.Pitch);
                            Marshal.Copy(sourceOffset, pixelData, y * rowSize, rowSize);
                        }
                    }
                    finally
                    {
                        SdlNativeBindings.SdlUnlockSurface(convertedSurface);
                    }

                    decoded = new DecodedImage(convertedSurface.W, convertedSurface.H, pixelData);
                    return true;
                }
                finally
                {
                    if (convertedSurface is not null && !convertedSurface.IsNull)
                        SdlNativeBindings.SdlFreeSurface(convertedSurface);

                    if (surface is not null && !surface.IsNull)
                        SdlNativeBindings.SdlFreeSurface(surface);
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static byte[] ReadAllBytes(Stream source)
    {
        if (source.CanSeek)
            source.Seek(0, SeekOrigin.Begin);

        using var memory = new MemoryStream();
        source.CopyTo(memory);
        return memory.ToArray();
    }

    private readonly record struct DecodedImage(int Width, int Height, byte[] Data);
}
