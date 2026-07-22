using System.Runtime.InteropServices;

namespace RedBolt;

public static partial class ArchiveSerialization
{
    public static void SerializeValue(Archive archive, string name, ref Vector3f value)
    {
        SerializePrimitiveArray(archive, name, ref value);
    }

    public static void SerializeValue(Archive archive, string name, ref Vector4f value)
    {
        SerializePrimitiveArray(archive, name, ref value);
    }

    /// <summary>Serialize SH to archive.</summary>
    public static void SerializeValue(Archive archive, string name, ref SphericalHarmonicsDot9 value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        SerializeValue(archive, "Ar", ref value.Ar);
        SerializeValue(archive, "Ag", ref value.Ag);
        SerializeValue(archive, "Ab", ref value.Ab);
        SerializeValue(archive, "Br", ref value.Br);
        SerializeValue(archive, "Bg", ref value.Bg);
        SerializeValue(archive, "Bb", ref value.Bb);
        SerializeValue(archive, "C", ref value.C);
    }

    public static void SerializeValue(Archive archive, string name, ref Matrix3x4 value)
    {
        SerializePrimitiveArray(archive, name, ref value);
    }

    public static void SerializeValue(Archive archive, string name, ref Tetrahedron value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        SerializeValue(archive, "index0", ref value.Indices[0]);
        SerializeValue(archive, "index1", ref value.Indices[1]);
        SerializeValue(archive, "index2", ref value.Indices[2]);
        SerializeValue(archive, "index3", ref value.Indices[3]);
        SerializeValue(archive, "neighbor0", ref value.Neighbors[0]);
        SerializeValue(archive, "neighbor1", ref value.Neighbors[1]);
        SerializeValue(archive, "neighbor2", ref value.Neighbors[2]);
        SerializeValue(archive, "neighbor3", ref value.Neighbors[3]);
        SerializeValue(archive, "matrix", ref value.Matrix);
    }

    public static void SerializeValue(Archive archive, string name, ref TetrahedralMesh value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        SerializeVector3f(archive, "vertices", value.Vertices);
        SerializeTetrahedronVector(archive, "tetrahedrons", value.Tetrahedrons);
        SerializeVector3f(archive, "hullNormals", value.HullNormals);
        SerializeValue(archive, "numInnerTetrahedrons", ref value.NumInnerTetrahedrons);
    }

    private static void SerializePrimitiveArray<T>(Archive archive, string name, ref T value)
        where T : unmanaged
    {
        if (!archive.IsHumanReadable)
        {
            var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));
            archive.SerializeBytes(name, bytes);
            return;
        }

        var components = MemoryMarshal.Cast<T, float>(MemoryMarshal.CreateSpan(ref value, 1));
        var text = archive.IsInput ? string.Empty : NumberArrayToString(components);
        archive.Serialize(name, ref text);
        if (archive.IsInput)
            StringToNumberArray(text, components);
    }

    private static void SerializeVector3f(Archive archive, string name, List<Vector3f> values)
    {
        if (!archive.IsHumanReadable)
        {
            using var block = archive.OpenUnorderedBlock(name);
            var sizeInBytes = archive.IsInput ? 0u : checked((uint)(values.Count * sizeof(float) * 3));
            archive.SerializeVle("size", ref sizeInBytes);

            if (archive.IsInput)
            {
                if (sizeInBytes % (sizeof(float) * 3) != 0)
                    throw new InvalidDataException($"'{archive.CurrentBlockPath}/{name}' has unexpected size in bytes");

                values.Clear();
                values.AddRange(new Vector3f[checked((int)(sizeInBytes / (sizeof(float) * 3)))]);
            }

            var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(values));
            archive.SerializeBytes("data", bytes[..checked((int)sizeInBytes)]);
            return;
        }

        var size = values.Count;
        using var arrayBlock = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            values.Clear();
            values.AddRange(new Vector3f[arrayBlock.SizeHint]);
        }

        for (var index = 0; index < (archive.IsInput ? arrayBlock.SizeHint : size); ++index)
        {
            var value = values[index];
            SerializeValue(archive, "element", ref value);
            values[index] = value;
        }
    }

    private static void SerializeTetrahedronVector(Archive archive, string name, List<Tetrahedron> values)
    {
        if (!archive.IsHumanReadable)
        {
            using var byteBlock = archive.OpenUnorderedBlock(name);
            var elementSize = checked((uint)Marshal.SizeOf<Tetrahedron>());
            var sizeInBytes = archive.IsInput ? 0u : checked((uint)values.Count * elementSize);
            archive.SerializeVle("size", ref sizeInBytes);

            if (archive.IsInput)
            {
                if (sizeInBytes % elementSize != 0)
                    throw new InvalidDataException($"'{archive.CurrentBlockPath}/{name}' has unexpected size in bytes");

                values.Clear();
                values.AddRange(new Tetrahedron[checked((int)(sizeInBytes / elementSize))]);
            }

            var bytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(values));
            archive.SerializeBytes("data", bytes[..checked((int)sizeInBytes)]);
            return;
        }

        var size = values.Count;
        using var block = archive.OpenArrayBlock(name, size);
        if (archive.IsInput)
        {
            values.Clear();
            for (var index = 0; index < block.SizeHint; ++index)
                values.Add(new Tetrahedron());
        }

        for (var index = 0; index < (archive.IsInput ? block.SizeHint : size); ++index)
        {
            var value = values[index];
            SerializeValue(archive, "element", ref value);
            values[index] = value;
        }
    }
}
