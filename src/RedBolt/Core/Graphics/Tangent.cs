namespace RedBolt.Graphics;

using System.Runtime.InteropServices;

using RedBolt.Math;

public static class Tangent
{
    private static uint GetIndex(ReadOnlySpan<byte> indexData, uint indexSize, ref int indexOffset)
    {
        if (indexSize == sizeof(ushort))
        {
            ushort index = MemoryMarshal.Read<ushort>(indexData[indexOffset..]);
            indexOffset += sizeof(ushort);
            return index;
        }

        uint result = MemoryMarshal.Read<uint>(indexData[indexOffset..]);
        indexOffset += sizeof(uint);
        return result;
    }

    /// <summary>Generate tangents to indexed geometry.</summary>
    public static void GenerateTangents(Span<byte> vertexData, uint vertexSize, ReadOnlySpan<byte> indexData,
        uint indexSize, uint indexStart, uint indexCount, uint normalOffset, uint texCoordOffset, uint tangentOffset)
    {
        // Tangent generation from
        // http://www.terathon.com/code/tangent.html
        uint minVertex = uint.MaxValue;
        uint maxVertex = 0;
        int indexOffset = 0;

        for (uint i = indexStart; i < indexStart + indexCount; ++i)
        {
            uint vertex = GetIndex(indexData, indexSize, ref indexOffset);
            if (vertex < minVertex)
                minVertex = vertex;
            if (vertex > maxVertex)
                maxVertex = vertex;
        }

        uint vertexCount = maxVertex + 1;
        var tan1 = new Vector3f[checked((int)vertexCount * 2)];
        var tan2 = tan1.AsSpan(checked((int)vertexCount), checked((int)vertexCount));
        Array.Clear(tan1, 0, tan1.Length);

        indexOffset = 0;
        for (uint i = indexStart; i < indexStart + indexCount; i += 3)
        {
            uint i1 = GetIndex(indexData, indexSize, ref indexOffset);
            uint i2 = GetIndex(indexData, indexSize, ref indexOffset);
            uint i3 = GetIndex(indexData, indexSize, ref indexOffset);

            Vector3f v1 = MemoryMarshal.Read<Vector3f>(vertexData[checked((int)(i1 * vertexSize))..]);
            Vector3f v2 = MemoryMarshal.Read<Vector3f>(vertexData[checked((int)(i2 * vertexSize))..]);
            Vector3f v3 = MemoryMarshal.Read<Vector3f>(vertexData[checked((int)(i3 * vertexSize))..]);

            Vector2f w1 = MemoryMarshal.Read<Vector2f>(vertexData[checked((int)(i1 * vertexSize + texCoordOffset))..]);
            Vector2f w2 = MemoryMarshal.Read<Vector2f>(vertexData[checked((int)(i2 * vertexSize + texCoordOffset))..]);
            Vector2f w3 = MemoryMarshal.Read<Vector2f>(vertexData[checked((int)(i3 * vertexSize + texCoordOffset))..]);

            float x1 = v2.X - v1.X;
            float x2 = v3.X - v1.X;
            float y1 = v2.Y - v1.Y;
            float y2 = v3.Y - v1.Y;
            float z1 = v2.Z - v1.Z;
            float z2 = v3.Z - v1.Z;

            float s1 = w2.X - w1.X;
            float s2 = w3.X - w1.X;
            float t1 = w2.Y - w1.Y;
            float t2 = w3.Y - w1.Y;

            float r = 1.0f / (s1 * t2 - s2 * t1);
            Vector3f sdir = new((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3f tdir = new((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[checked((int)i1)] += sdir;
            tan1[checked((int)i2)] += sdir;
            tan1[checked((int)i3)] += sdir;

            tan2[checked((int)i1)] += tdir;
            tan2[checked((int)i2)] += tdir;
            tan2[checked((int)i3)] += tdir;
        }

        for (uint i = minVertex; i <= maxVertex; ++i)
        {
            Vector3f n = MemoryMarshal.Read<Vector3f>(vertexData[checked((int)(i * vertexSize + normalOffset))..]);
            Vector3f t = tan1[checked((int)i)];

            // Gram-Schmidt orthogonalize
            Vector3f xyz = (t - n * n.DotProduct(t)).Normalized;

            // Calculate handedness
            float w = n.CrossProduct(t).DotProduct(tan2[checked((int)i)]) < 0.0f ? -1.0f : 1.0f;

            Vector4f tangent = new(xyz, w);
            MemoryMarshal.Write(vertexData[checked((int)(i * vertexSize + tangentOffset))..], in tangent);
        }
    }
}
