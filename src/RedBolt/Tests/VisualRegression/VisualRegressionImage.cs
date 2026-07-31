namespace RedBolt.Tests;

using System.Globalization;
using System.Text;
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

        return string.Equals(Path.GetExtension(fileName), ".ppm", StringComparison.OrdinalIgnoreCase)
            ? LoadPpm(fileName)
            : LoadImage(fileName);
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

    public VisualRegressionImage Resize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; ++y)
        {
            var sourceTop = y * Height / height;
            var sourceBottom = SystemMath.Max(sourceTop + 1, (y + 1) * Height / height);
            for (var x = 0; x < width; ++x)
            {
                var sourceLeft = x * Width / width;
                var sourceRight = SystemMath.Max(sourceLeft + 1, (x + 1) * Width / width);
                var sums = new int[4];
                var samples = 0;
                for (var sourceY = sourceTop; sourceY < sourceBottom; ++sourceY)
                {
                    for (var sourceX = sourceLeft; sourceX < sourceRight; ++sourceX)
                    {
                        var sourceIndex = (sourceY * Width + sourceX) * 4;
                        for (var channel = 0; channel < 4; ++channel)
                            sums[channel] += Pixels[sourceIndex + channel];
                        ++samples;
                    }
                }

                var targetIndex = (y * width + x) * 4;
                for (var channel = 0; channel < 4; ++channel)
                    pixels[targetIndex + channel] = checked((byte)((sums[channel] + samples / 2) / samples));
            }
        }

        return new VisualRegressionImage(width, height, pixels);
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

    public void SavePpm(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directoryName))
            Directory.CreateDirectory(directoryName);

        using var writer = new StreamWriter(fileName, false, Encoding.ASCII);
        writer.WriteLine("P3");
        writer.WriteLine($"{Width} {Height}");
        writer.WriteLine("255");
        for (var y = 0; y < Height; ++y)
        {
            for (var x = 0; x < Width; ++x)
            {
                var index = (y * Width + x) * 4;
                writer.Write(Pixels[index].ToString(CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(Pixels[index + 1].ToString(CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(Pixels[index + 2].ToString(CultureInfo.InvariantCulture));
                writer.Write(' ');
            }

            writer.WriteLine();
        }
    }

    private static VisualRegressionImage LoadImage(string fileName)
    {
        using var source = new VectorBuffer(HostFile.ReadAllBytes(fileName));
        using var image = new Image();
        if (!image.BeginLoad(source))
            throw new InvalidDataException($"Could not load visual regression image '{fileName}'.");
        return FromImage(image);
    }

    private static VisualRegressionImage LoadPpm(string fileName)
    {
        var data = HostFile.ReadAllBytes(fileName);
        var reader = new PpmReader(data);
        if (!string.Equals(reader.ReadToken(), "P3", StringComparison.Ordinal))
            throw new InvalidDataException($"Reference '{fileName}' is not an ASCII PPM image.");

        var width = reader.ReadInt();
        var height = reader.ReadInt();
        var maxValue = reader.ReadInt();
        if (width <= 0 || height <= 0 || maxValue <= 0)
            throw new InvalidDataException($"Reference '{fileName}' has invalid dimensions or color range.");

        var pixels = new byte[checked(width * height * 4)];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = ScalePpmValue(reader.ReadInt(), maxValue);
            pixels[i + 1] = ScalePpmValue(reader.ReadInt(), maxValue);
            pixels[i + 2] = ScalePpmValue(reader.ReadInt(), maxValue);
            pixels[i + 3] = byte.MaxValue;
        }

        return new VisualRegressionImage(width, height, pixels);
    }

    private static byte ScalePpmValue(int value, int maxValue)
    {
        if (value < 0 || value > maxValue)
            throw new InvalidDataException("PPM color component is outside its declared range.");
        return checked((byte)((value * byte.MaxValue + maxValue / 2) / maxValue));
    }

    private sealed class PpmReader
    {
        private readonly byte[] _data;
        private int _position;

        public PpmReader(byte[] data)
        {
            _data = data;
        }

        public string ReadToken()
        {
            SkipWhitespaceAndComments();
            var start = _position;
            while (_position < _data.Length && _data[_position] > (byte)' ')
                ++_position;
            if (start == _position)
                throw new InvalidDataException("Unexpected end of PPM image.");
            return Encoding.ASCII.GetString(_data, start, _position - start);
        }

        public int ReadInt()
        {
            var token = ReadToken();
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new InvalidDataException($"Invalid PPM integer '{token}'.");
            return value;
        }

        private void SkipWhitespaceAndComments()
        {
            while (_position < _data.Length)
            {
                if (_data[_position] <= (byte)' ')
                {
                    ++_position;
                    continue;
                }

                if (_data[_position] != (byte)'#')
                    return;

                while (_position < _data.Length && _data[_position] != (byte)'\n')
                    ++_position;
            }
        }
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
