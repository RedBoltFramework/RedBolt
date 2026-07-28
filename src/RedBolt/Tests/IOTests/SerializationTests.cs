namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

public sealed class SerializationTests
{
    [Fact]
    public void VectorBuffer_typed_write_read_roundtrips()
    {
        var buffer = new VectorBuffer();
        Assert.True(buffer.WriteInt(42));
        Assert.True(buffer.WriteString("redbolt"));
        Assert.True(buffer.WriteVector3(new Vector3f(1.0f, 2.0f, 3.0f)));
        Assert.True(buffer.WriteVLE(300u));

        buffer.Seek(0);
        Assert.Equal(42, buffer.ReadInt());
        Assert.Equal("redbolt", buffer.ReadString());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), buffer.ReadVector3());
        Assert.Equal(300u, buffer.ReadVLE());
    }

    [Fact]
    public void MemoryBuffer_read_only_works()
    {
        var buffer = new VectorBuffer();
        buffer.WriteInt(42);
        buffer.WriteString("redbolt");
        buffer.WriteVector3(new Vector3f(1.0f, 2.0f, 3.0f));
        buffer.WriteVLE(300u);

        var memory = new MemoryBuffer(buffer.GetBuffer().AsMemory());
        Assert.True(memory.IsReadOnly);
        Assert.Equal(42, memory.ReadInt());
        Assert.Equal("redbolt", memory.ReadString());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), memory.ReadVector3());
        Assert.Equal(300u, memory.ReadVLE());
        Assert.True(memory.Eof);
    }

    [Fact]
    public void MemoryBuffer_backed_by_vector_buffer_is_writable()
    {
        var vectorBuffer = new VectorBuffer();
        vectorBuffer.WriteInt(1);

        var memory = new MemoryBuffer(vectorBuffer);
        Assert.False(memory.IsReadOnly);

        memory.Seek(0);
        Assert.True(memory.WriteInt(42));

        vectorBuffer.Seek(0);
        Assert.Equal(42, vectorBuffer.ReadInt());
    }

    [Fact]
    public void VectorBuffer_stream_constructor_and_partial_set_data_match_actual_read_size()
    {
        var source = new MemoryBuffer(new byte[] { 1, 2, 3 });

        var fromCtor = new VectorBuffer(source, 8);
        Assert.Equal<uint>(3, fromCtor.Size);
        Assert.Equal(new byte[] { 1, 2, 3 }, fromCtor.GetBuffer());

        source.Seek(0);

        var fromSetter = new VectorBuffer();
        fromSetter.SetData(source, 8);
        Assert.Equal<uint>(3, fromSetter.Size);
        Assert.Equal(new byte[] { 1, 2, 3 }, fromSetter.GetBuffer());
    }

    [Fact]
    public void VectorBuffer_pointer_constructor_and_set_data_copy_source_memory()
    {
        var source = new byte[] { 4, 5, 6, 7 };

        unsafe
        {
            fixed (byte* pointer = source)
            {
                var fromCtor = new VectorBuffer((nint)pointer, (uint)source.Length);
                Assert.Equal(source, fromCtor.GetBuffer());

                var fromSetter = new VectorBuffer();
                fromSetter.SetData((nint)pointer, (uint)source.Length);
                Assert.Equal(source, fromSetter.GetBuffer());
            }
        }
    }

    [Fact]
    public void VectorBuffer_byte_array_constructor_copies_source_buffer()
    {
        var source = new byte[] { 9, 8, 7, 6 };

        var buffer = new VectorBuffer(source);
        source[0] = 1;

        Assert.Equal(new byte[] { 9, 8, 7, 6 }, buffer.GetBuffer());
    }

    [Fact]
    public void VectorBuffer_get_buffer_exposes_live_storage()
    {
        var buffer = new VectorBuffer(new byte[] { 1, 2, 3, 4 });
        var data = buffer.GetBuffer();

        data[2] = 9;

        buffer.Seek(2);
        Assert.Equal(9, buffer.ReadUByte());
    }

    [Fact]
    public void Line_read_write_works()
    {
        var buffer = new VectorBuffer();
        buffer.WriteLine("first");
        buffer.WriteLine("second");
        buffer.Seek(0);
        Assert.Equal("first", buffer.ReadLine());
        Assert.Equal("second", buffer.ReadLine());
    }

    [Fact]
    public void Matrix_and_resource_stream_helpers_roundtrip()
    {
        var buffer = new VectorBuffer();
        var matrix3 = new Matrix3(1, 2, 3, 4, 5, 6, 7, 8, 9);
        var matrix3x4 = new Matrix3x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
        var matrix4 = new Matrix4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
        var resourceRef = new ResourceRef(new StringHash("Material"), "Materials/Test.xml");
        var resourceRefList = new ResourceRefList(new StringHash("Texture2D"), ["A.png", "B.png"]);

        Assert.True(buffer.WriteMatrix3(matrix3));
        Assert.True(buffer.WriteMatrix3x4(matrix3x4));
        Assert.True(buffer.WriteMatrix4(matrix4));
        Assert.True(buffer.WriteResourceRef(resourceRef));
        Assert.True(buffer.WriteResourceRefList(resourceRefList));

        buffer.Seek(0);

        Assert.Equal(matrix3, buffer.ReadMatrix3());
        Assert.Equal(matrix3x4, buffer.ReadMatrix3x4());
        Assert.Equal(matrix4, buffer.ReadMatrix4());
        Assert.Equal(resourceRef, buffer.ReadResourceRef());

        var actualList = buffer.ReadResourceRefList();
        Assert.Equal(resourceRefList.Type, actualList.Type);
        Assert.Equal(resourceRefList.Names, actualList.Names);
    }

    [Fact]
    public void Double_vector_stream_helpers_roundtrip()
    {
        var buffer = new VectorBuffer();
        var doubleVector2 = new Vector2d(1.25, 2.5);
        var doubleVector3 = new Vector3d(3.75, 4.5, 5.25);

        Assert.True(buffer.WriteVariant(new Variant(doubleVector2)));
        Assert.True(buffer.WriteVariant(new Variant(doubleVector3)));

        buffer.Seek(0);

        Assert.Equal(doubleVector2, buffer.ReadVariant().GetDoubleVector2());
        Assert.Equal(doubleVector3, buffer.ReadVariant().GetDoubleVector3());
    }

    [Fact]
    public void Variant_stream_helpers_roundtrip_supported_extended_types()
    {
        var buffer = new VectorBuffer();
        var variantVector = new VariantVector([new Variant(42), new Variant("value")]);
        var stringVector = new StringVector(["alpha", "beta"]);
        var variantMap = new VariantMap
        {
            [new StringHash(1)] = new Variant(new Vector3f(1, 2, 3)),
            [new StringHash(2)] = new Variant(123L)
        };
        var stringVariantMap = new StringVariantMap
        {
            ["one"] = new Variant(new Matrix3x4(1, 0, 0, 4, 0, 1, 0, 5, 0, 0, 1, 6)),
            ["two"] = new Variant(new byte[] { 7, 8, 9 })
        };

        Assert.True(buffer.WriteVariant(new Variant(variantVector)));
        Assert.True(buffer.WriteVariant(new Variant(stringVector)));
        Assert.True(buffer.WriteVariant(new Variant(variantMap)));
        Assert.True(buffer.WriteVariant(new Variant(stringVariantMap)));
        Assert.True(buffer.WriteVariant(new Variant(new Vector2d(1.5, 2.5))));
        Assert.True(buffer.WriteVariant(new Variant(new Vector3d(3.5, 4.5, 5.5))));

        buffer.Seek(0);

        var actualVariantVector = buffer.ReadVariant().GetVariantVector();
        Assert.Equal(variantVector.Count, actualVariantVector.Count);
        Assert.Equal(variantVector[0], actualVariantVector[0]);
        Assert.Equal(variantVector[1], actualVariantVector[1]);

        var actualStringVector = buffer.ReadVariant().GetStringVector();
        Assert.Equal(stringVector, actualStringVector);

        var actualVariantMap = buffer.ReadVariant().GetVariantMap();
        Assert.Equal(variantMap.Count, actualVariantMap.Count);
        Assert.Equal(variantMap[new StringHash(1)], actualVariantMap[new StringHash(1)]);
        Assert.Equal(variantMap[new StringHash(2)], actualVariantMap[new StringHash(2)]);

        var actualStringVariantMap = buffer.ReadVariant().GetStringVariantMap();
        Assert.Equal(stringVariantMap.Count, actualStringVariantMap.Count);
        Assert.Equal(stringVariantMap["one"], actualStringVariantMap["one"]);
        Assert.Equal(stringVariantMap["two"], actualStringVariantMap["two"]);

        Assert.Equal(new Vector2d(1.5, 2.5), buffer.ReadVariant().GetDoubleVector2());
        Assert.Equal(new Vector3d(3.5, 4.5, 5.5), buffer.ReadVariant().GetDoubleVector3());
    }

    [Fact]
    public void Variant_type_numeric_order_matches_rbfx_for_streamed_entries()
    {
        Assert.Equal(11, (int)VariantType.String);
        Assert.Equal(12, (int)VariantType.Buffer);
        Assert.Equal(14, (int)VariantType.ResourceRef);
        Assert.Equal(15, (int)VariantType.ResourceRefList);
        Assert.Equal(16, (int)VariantType.VariantVector);
        Assert.Equal(17, (int)VariantType.VariantMap);
        Assert.Equal(7, (int)VariantType.DoubleVector2);
        Assert.Equal(8, (int)VariantType.DoubleVector3);
        Assert.Equal(21, (int)VariantType.Matrix3);
        Assert.Equal(22, (int)VariantType.Matrix3x4);
        Assert.Equal(23, (int)VariantType.Matrix4);
        Assert.Equal(25, (int)VariantType.StringVector);
        Assert.Equal(31, (int)VariantType.StringVariantMap);
    }

    [Fact]
    public void Variant_default_values_and_string_contract_match_rbfx_slice()
    {
        Assert.Equal(Color.Black, new Variant(VariantType.Color).GetColor());
        Assert.Equal(Matrix3.Zero, new Variant(VariantType.Matrix3).GetMatrix3());
        Assert.Equal(Matrix3x4.Zero, new Variant(VariantType.Matrix3x4).GetMatrix3x4());
        Assert.Equal(Matrix4.Zero, new Variant(VariantType.Matrix4).GetMatrix4());
        Assert.Equal("Color", new Variant(Color.White).GetTypeName());
        Assert.Equal(VariantType.DoubleVector3, Variant.GetTypeFromName("DoubleVector3"));
        Assert.Equal("1 2 3", new Variant(new byte[] { 1, 2, 3 }).ToString());
        Assert.True(new Variant(Color.White).IsZero());
        Assert.True(new Variant(Matrix3.Identity).IsZero());
        Assert.False(new Variant(Color.Black).IsZero());
        Assert.Equal(new StringHash(123u), new Variant(123u).GetStringHash());
    }

    [Fact]
    public void Packed_vector_and_quaternion_stream_helpers_roundtrip_supported_encodings()
    {
        var buffer = new VectorBuffer();
        var vector = new Vector3d(1.25, -2.5, 3.75);
        var quaternion = new Quaternion(0.25f, -0.5f, 0.75f, -1.0f).Normalized;

        Assert.True(buffer.WritePackedVector3(vector, VectorBinaryEncoding.Float));
        Assert.True(buffer.WritePackedVector3(vector, VectorBinaryEncoding.Double));
        Assert.True(buffer.WritePackedVector3(vector, VectorBinaryEncoding.Int32, 4.0f));
        Assert.True(buffer.WritePackedVector3(vector, VectorBinaryEncoding.Int16, 4.0f));
        Assert.True(buffer.WritePackedQuaternion(quaternion, VectorBinaryEncoding.Float));
        Assert.True(buffer.WritePackedQuaternion(quaternion, VectorBinaryEncoding.Int16));

        buffer.Seek(0);

        Assert.True(buffer.ReadPackedVector3(VectorBinaryEncoding.Float).ApproximatelyEquivalent(vector, 0.0001f));
        Assert.True(buffer.ReadPackedVector3(VectorBinaryEncoding.Double).ApproximatelyEquivalent(vector, 0.0001f));
        Assert.True(buffer.ReadPackedVector3(VectorBinaryEncoding.Int32, 4.0f).ApproximatelyEquivalent(vector, 0.0001f));
        Assert.True(buffer.ReadPackedVector3(VectorBinaryEncoding.Int16, 4.0f).ApproximatelyEquivalent(vector, 0.001f));
        Assert.True(buffer.ReadPackedQuaternion(VectorBinaryEncoding.Float).ApproximatelyEquivalent(quaternion, 0.0001f));
        Assert.True(buffer.ReadPackedQuaternion(VectorBinaryEncoding.Int16).ApproximatelyEquivalent(quaternion, 0.001f));
    }

    [Fact]
    public void ReadBuffer_in_place_matches_rbfx_helper_shape()
    {
        var buffer = new VectorBuffer();
        Assert.True(buffer.WriteBuffer(new byte[] { 4, 5, 6, 7 }));
        buffer.Seek(0);

        var byteVector = new List<byte> { 1, 2, 3 };
        buffer.ReadBuffer(byteVector);

        Assert.Equal(new byte[] { 4, 5, 6, 7 }, byteVector);
    }
}
