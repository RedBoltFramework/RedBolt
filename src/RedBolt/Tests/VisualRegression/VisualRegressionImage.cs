namespace RedBolt.Tests;

using SystemMath = global::System.Math;

using RedBolt.IO;
using RedBolt.Resource;
using HostFile = global::System.IO.File;

internal sealed class VisualRegressionImage
{
    private VisualRegressionImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (pixels.Length != checked(width * height * 4))
            throw new ArgumentException("An RGBA image must contain four bytes per pixel.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public static VisualRegressionImage FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Width <= 0 || image.Height <= 0 || image.Components is < 1 or > 4)
            throw new ArgumentException("The image must contain a non-empty 1-4 component image.", nameof(image));

        var source = image.GetData();
        var pixels = new byte[checked(image.Width * image.Height * 4)];
        for (var sourceIndex = 0; sourceIndex < source.Length; sourceIndex += image.Components)
        {
            var pixelIndex = checked(sourceIndex / image.Components * 4);
            pixels[pixelIndex] = source[sourceIndex];
            if (image.Components > 1)
                pixels[pixelIndex + 1] = source[sourceIndex + 1];
            if (image.Components > 2)
                pixels[pixelIndex + 2] = source[sourceIndex + 2];
            pixels[pixelIndex + 3] = image.Components > 3 ? source[sourceIndex + 3] : byte.MaxValue;
        }

        return new VisualRegressionImage(image.Width, image.Height, pixels);
    }

    public static VisualRegressionImage Load(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        if (!string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Visual regression references must be PNG images.", nameof(fileName));

        return LoadPng(fileName);
    }

    public static VisualRegressionImage Solid(int width, int height, byte red, byte green, byte blue,
        byte alpha = byte.MaxValue)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = red;
            pixels[i + 1] = green;
            pixels[i + 2] = blue;
            pixels[i + 3] = alpha;
        }

        return new VisualRegressionImage(width, height, pixels);
    }

    public static VisualRegressionImage FromRgba(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return new VisualRegressionImage(width, height, (byte[])pixels.Clone());
    }

    public VisualRegressionComparison Compare(VisualRegressionImage expected,
        int channelTolerance = 8, double maxDifferingPixelRatio = 0.02,
        double maxAverageChannelError = 2.0)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (channelTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(channelTolerance));
        if (maxDifferingPixelRatio is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(maxDifferingPixelRatio));
        if (maxAverageChannelError < 0.0)
            throw new ArgumentOutOfRangeException(nameof(maxAverageChannelError));

        if (Width != expected.Width || Height != expected.Height)
        {
            return new VisualRegressionComparison(
                false,
                Width,
                Height,
                expected.Width,
                expected.Height,
                1.0,
                double.PositiveInfinity,
                byte.MaxValue,
                checked(Width * Height));
        }

        var differingPixels = 0;
        var maxChannelError = 0;
        long totalChannelError = 0;
        var pixelCount = checked(Width * Height);
        for (var i = 0; i < Pixels.Length; i += 4)
        {
            var pixelMaxError = 0;
            for (var channel = 0; channel < 4; ++channel)
            {
                var channelError = SystemMath.Abs(Pixels[i + channel] - expected.Pixels[i + channel]);
                pixelMaxError = SystemMath.Max(pixelMaxError, channelError);
                maxChannelError = SystemMath.Max(maxChannelError, channelError);
                totalChannelError += channelError;
            }

            if (pixelMaxError > channelTolerance)
                ++differingPixels;
        }

        var differingPixelRatio = (double)differingPixels / pixelCount;
        var averageChannelError = (double)totalChannelError / Pixels.Length;
        var isMatch = differingPixelRatio <= maxDifferingPixelRatio
            && averageChannelError <= maxAverageChannelError;
        return new VisualRegressionComparison(
            isMatch,
            Width,
            Height,
            expected.Width,
            expected.Height,
            differingPixelRatio,
            averageChannelError,
            maxChannelError,
            differingPixels);
    }

    public VisualRegressionImage CreateDifference(VisualRegressionImage expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (Width != expected.Width || Height != expected.Height)
            throw new ArgumentException("Images must have equal dimensions.", nameof(expected));

        var pixels = new byte[Pixels.Length];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            for (var channel = 0; channel < 3; ++channel)
            {
                var difference = SystemMath.Abs(Pixels[i + channel] - expected.Pixels[i + channel]);
                pixels[i + channel] = checked((byte)SystemMath.Min(byte.MaxValue, difference * 8));
            }

            pixels[i + 3] = byte.MaxValue;
        }

        return new VisualRegressionImage(Width, Height, pixels);
    }

    public void SavePng(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directoryName))
            Directory.CreateDirectory(directoryName);

        using var image = new Image();
        if (!image.SetSize(Width, Height, 4))
            throw new InvalidOperationException($"Could not allocate {Width}x{Height} image.");
        image.SetData(Pixels);
        if (!image.SavePNG(fileName))
            throw new IOException($"Could not save visual regression image '{fileName}'.");
    }

    private static VisualRegressionImage LoadPng(string fileName)
    {
        using var source = new VectorBuffer(HostFile.ReadAllBytes(fileName));
        using var image = new Image();
        if (!image.BeginLoad(source))
            throw new InvalidDataException($"Could not load visual regression image '{fileName}'.");
        return FromImage(image);
    }
}

internal sealed class VisualRegressionComparison
{
    public VisualRegressionComparison(bool isMatch, int actualWidth, int actualHeight,
        int expectedWidth, int expectedHeight, double differingPixelRatio,
        double averageChannelError, int maxChannelError, int differingPixels)
    {
        IsMatch = isMatch;
        ActualWidth = actualWidth;
        ActualHeight = actualHeight;
        ExpectedWidth = expectedWidth;
        ExpectedHeight = expectedHeight;
        DifferingPixelRatio = differingPixelRatio;
        AverageChannelError = averageChannelError;
        MaxChannelError = maxChannelError;
        DifferingPixels = differingPixels;
    }

    public bool IsMatch { get; }

    public int ActualWidth { get; }

    public int ActualHeight { get; }

    public int ExpectedWidth { get; }

    public int ExpectedHeight { get; }

    public double DifferingPixelRatio { get; }

    public double AverageChannelError { get; }

    public int MaxChannelError { get; }

    public int DifferingPixels { get; }

    public override string ToString()
    {
        return $"actual={ActualWidth}x{ActualHeight}, expected={ExpectedWidth}x{ExpectedHeight}, "
            + $"differentPixels={DifferingPixels} ({DifferingPixelRatio:P2}), "
            + $"averageChannelError={AverageChannelError:F3}, maxChannelError={MaxChannelError}";
    }
}
