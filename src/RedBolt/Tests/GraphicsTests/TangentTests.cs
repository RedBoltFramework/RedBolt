namespace RedBolt.Tests;

using System.Runtime.InteropServices;

using Xunit;

using RedBolt.Graphics;
using RedBolt.Math;

public sealed class TangentTests
{
    [Fact]
    public void Generate_tangents_writes_orthogonal_handed_tangent()
    {
        const int vertexSize = 48;
        const int normalOffset = 12;
        const int texCoordOffset = 24;
        const int tangentOffset = 32;
        var vertexData = new byte[vertexSize * 3];
        WriteVertex(vertexData, 0, new Vector3f(0, 0, 0), new Vector3f(0, 0, 1), new Vector2f(0, 0));
        WriteVertex(vertexData, 1, new Vector3f(1, 0, 0), new Vector3f(0, 0, 1), new Vector2f(1, 0));
        WriteVertex(vertexData, 2, new Vector3f(0, 1, 0), new Vector3f(0, 0, 1), new Vector2f(0, 1));

        var indexData = new byte[sizeof(ushort) * 3];
        ushort index0 = 0;
        ushort index1 = 1;
        ushort index2 = 2;
        MemoryMarshal.Write(indexData.AsSpan(0), in index0);
        MemoryMarshal.Write(indexData.AsSpan(sizeof(ushort)), in index1);
        MemoryMarshal.Write(indexData.AsSpan(sizeof(ushort) * 2), in index2);

        Tangent.GenerateTangents(vertexData, vertexSize, indexData, sizeof(ushort), 0, 3,
            normalOffset, texCoordOffset, tangentOffset);

        for (var i = 0; i < 3; ++i)
        {
            Vector4f tangent = MemoryMarshal.Read<Vector4f>(vertexData.AsSpan(i * vertexSize + tangentOffset));
            Assert.True(tangent.ApproximatelyEquivalent(new Vector4f(1, 0, 0, 1)));
        }
    }

    private static void WriteVertex(byte[] vertexData, int index, Vector3f position, Vector3f normal, Vector2f texCoord)
    {
        var destination = vertexData.AsSpan(index * 48);
        MemoryMarshal.Write(destination, in position);
        MemoryMarshal.Write(destination[12..], in normal);
        MemoryMarshal.Write(destination[24..], in texCoord);
    }
}
