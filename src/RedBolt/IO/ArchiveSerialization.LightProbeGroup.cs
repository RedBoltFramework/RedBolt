using System.Runtime.InteropServices;

namespace RedBolt;

public static partial class ArchiveSerialization
{
    /// <summary>Serialize light probe.</summary>
    public static void SerializeValue(Archive archive, string name, ref LightProbe value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        SerializeValue(archive, "position", ref value.Position);
    }

    /// <summary>Serialize light probe baked data.</summary>
    public static void SerializeValue(Archive archive, string name, ref LightProbeCollectionBakedData value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        const uint currentVersion = 1;
        var version = archive.SerializeVersion(currentVersion);
        if (version == currentVersion)
        {
            SerializeSphericalHarmonicsVector(archive, "sphericalHarmonics", value.SphericalHarmonics);

            // Generate ambient if loading
            if (archive.IsInput)
            {
                var numLightProbes = value.Size;
                value.Resize(numLightProbes);
                for (var i = 0u; i < numLightProbes; ++i)
                    value.Ambient[(int)i] = value.SphericalHarmonics[(int)i].GetDebugColor().ToVector3();
            }
        }
    }

    public static void SerializeValue(Archive archive, string name, List<LightProbe> values)
    {
        var size = checked((uint)values.Count);
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            values.Clear();
            for (var index = 0u; index < block.SizeHint; ++index)
                values.Add(default);
        }

        for (var index = 0u; index < (archive.IsInput ? block.SizeHint : size); ++index)
        {
            var value = values[checked((int)index)];
            SerializeValue(archive, "element", ref value);
            values[checked((int)index)] = value;
        }
    }

    private static void SerializeSphericalHarmonicsVector(
        Archive archive, string name, List<SphericalHarmonicsDot9> values)
    {
        if (!archive.IsHumanReadable)
        {
            using var byteBlock = archive.OpenUnorderedBlock(name);
            var elementSize = checked((uint)Marshal.SizeOf<SphericalHarmonicsDot9>());
            var sizeInBytes = archive.IsInput ? 0u : checked((uint)values.Count * elementSize);
            archive.SerializeVle("size", ref sizeInBytes);

            if (archive.IsInput)
            {
                if (sizeInBytes % elementSize != 0)
                    throw new InvalidDataException($"'{archive.CurrentBlockPath}/{name}' has unexpected size in bytes");

                values.Clear();
                values.AddRange(new SphericalHarmonicsDot9[checked((int)(sizeInBytes / elementSize))]);
            }

            var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(values));
            archive.SerializeBytes("data", bytes[..checked((int)sizeInBytes)]);
            return;
        }

        var size = checked((uint)values.Count);
        using var arrayBlock = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            values.Clear();
            for (var index = 0u; index < arrayBlock.SizeHint; ++index)
                values.Add(default);
        }

        for (var index = 0u; index < (archive.IsInput ? arrayBlock.SizeHint : size); ++index)
        {
            var value = values[checked((int)index)];
            SerializeValue(archive, "element", ref value);
            values[checked((int)index)] = value;
        }
    }
}
