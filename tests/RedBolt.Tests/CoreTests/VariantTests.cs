namespace RedBolt.Tests;

public sealed class VariantTests
{
    private enum TestEnum
    {
        None,
        Value
    }

    [Fact]
    public void Void_pointer_matches_rbfx_null_and_serialization_contracts()
    {
        var pointer = new IntPtr(0x1234);
        var variant = new Variant(pointer);

        Assert.Equal(VariantType.VoidPtr, variant.Type);
        Assert.Equal(pointer, variant.GetVoidPtr());
        Assert.Equal(pointer, variant.Get<IntPtr>());
        Assert.Equal(pointer, variant.GetObject());
        Assert.True(variant.GetBool());
        Assert.False(variant.IsZero());
        Assert.Equal(string.Empty, variant.ToString());
        Assert.Equal(variant, Variant.FromObject(pointer));
        Assert.Equal(VariantType.VoidPtr, VariantTypeUtility.GetVariantType(pointer));

        var parsed = Variant.FromString(VariantType.VoidPtr, "0x1234");
        Assert.Equal(IntPtr.Zero, parsed.GetVoidPtr());
        Assert.False(parsed.GetBool());
        Assert.True(parsed.IsZero());
    }

    [Fact]
    public void Lerp_matches_rbfx_supported_type_contracts()
    {
        Assert.Equal(2, new Variant(1).Lerp(new Variant(4), 0.5f).GetInt());
        Assert.Equal(2L, new Variant(1L).Lerp(new Variant(4L), 0.5f).GetInt64());
        Assert.Equal(2.5, new Variant(1.0).Lerp(new Variant(4.0), 0.5f).GetDouble());
        Assert.Equal(new Vector3f(2.0f, 3.0f, 4.0f), new Variant(new Vector3f(1.0f, 2.0f, 3.0f)).Lerp(new Variant(new Vector3f(3.0f, 4.0f, 5.0f)), 0.5f).GetVector3());
        Assert.Equal(new Color(0.5f, 0.5f, 0.5f), new Variant(Color.Black).Lerp(new Variant(Color.White), 0.5f).GetColor());
        Assert.Equal(new IntRect(2, 3, 4, 5), new Variant(new IntRect(1, 2, 3, 4)).Lerp(new Variant(new IntRect(3, 4, 5, 6)), 0.5f).GetIntRect());
        Assert.Equal(new Vector2i(2, 3), new Variant(new Vector2i(1, 2)).Lerp(new Variant(new Vector2i(3, 4)), 0.5f).GetIntVector2());

        var unsupported = new Variant("left");
        Assert.Equal(unsupported, unsupported.Lerp(new Variant("right"), 0.5f));
    }

    [Fact]
    public void Buffer_and_vector_buffer_match_rbfx_copy_contracts()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var bufferVariant = new Variant(bytes);
        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, bufferVariant.GetBuffer());

        var vectorBuffer = new VectorBuffer(new byte[] { 4, 5, 6 });
        var vectorBufferVariant = new Variant(vectorBuffer);
        vectorBuffer.GetBuffer()[0] = 9;

        Assert.Equal(VariantType.Buffer, vectorBufferVariant.Type);
        Assert.Equal(new byte[] { 4, 5, 6 }, vectorBufferVariant.GetBuffer());

        var returnedBuffer = vectorBufferVariant.GetVectorBuffer();
        returnedBuffer.GetBuffer()[0] = 9;
        Assert.Equal(new byte[] { 4, 5, 6 }, vectorBufferVariant.GetBuffer());
    }

    [Fact]
    public void Getters_match_rbfx_conversion_and_default_contracts()
    {
        Assert.Equal(3, new Variant(3.9f).GetInt());
        Assert.Equal(3L, new Variant(3.9).GetInt64());
        Assert.Equal(1u, new Variant(true).GetUInt());
        Assert.Equal(1ul, new Variant(true).GetUInt64());
        Assert.True(new Variant(1.0f).GetBool());
        Assert.Equal(0.0f, new Variant("value").GetFloat());
        Assert.Equal(Vector3f.ZERO, new Variant("value").GetVector3());
        Assert.Equal(Quaternion.IDENTITY, new Variant("value").GetQuaternion());
        Assert.Equal(Color.White, new Variant("value").GetColor());
        Assert.Equal(new Color(1.0f, 2.0f, 3.0f, 4.0f), new Variant(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f)).GetColor());
        Assert.Equal(string.Empty, new Variant(1).GetString());
        Assert.Empty(new Variant(1).GetBuffer());
        Assert.Equal(ResourceRef.Empty, new Variant(1).GetResourceRef());
        Assert.Empty(new Variant(1).GetVariantVector());
        Assert.Equal(Matrix3.IDENTITY, new Variant(1).GetMatrix3());
        Assert.Equal(3, new Variant(3.9f).Get<int>());
        Assert.Equal(TestEnum.Value, new Variant(1).Get<TestEnum>());
        Assert.Equal(Color.White, new Variant("value").Get<Color>());
    }

    [Fact]
    public void Resource_reference_string_conversion_requires_context()
    {
        var value = new Variant(new ResourceRef("Material", "Materials/Test"));
        Context.SetInstance(null);
        try
        {
            Assert.Equal(string.Empty, value.ToString());

            Context.SetInstance(new Context());
            Assert.Equal("Material;Materials/Test", value.ToString());
        }
        finally
        {
            Context.SetInstance(null);
        }
    }

    [Fact]
    public void Convert_matches_rbfx_scalar_and_string_contracts()
    {
        Assert.Equal(3, new Variant(3.9f).Convert(VariantType.Int).GetInt());
        Assert.True(new Variant(1).Convert(VariantType.Bool).GetBool());
        Assert.Equal("3", new Variant(3).Convert(VariantType.String).GetString());
        Assert.True(Variant.FromString(VariantType.Bool, " yes").GetBool());
        Assert.Equal(42, Variant.FromString(VariantType.Int, "42").GetInt());
        Assert.Equal(0.5f, Variant.FromString(VariantType.Float, "0.5").GetFloat());
        Assert.Equal(new byte[] { 1, 2, 3 }, Variant.FromString(VariantType.Buffer, "1 2 3").GetBuffer());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), Variant.FromString(VariantType.Vector3, "1 2 3").GetVector3());
        Assert.Equal(Vector3f.ZERO, Variant.FromString(VariantType.Vector3, "1 2").GetVector3());
        Assert.Equal(new Color(1.0f, 2.0f, 3.0f), Variant.FromString(VariantType.Color, "1 2 3").GetColor());
        Assert.Equal(Quaternion.FromEulerAngles(1.0f, 2.0f, 3.0f), Variant.FromString(VariantType.Quaternion, "1 2 3").GetQuaternion());
        Assert.Equal(new IntRect(1, 2, 3, 4), Variant.FromString(VariantType.IntRect, "1 2 3 4").GetIntRect());
        Assert.Equal(new Matrix3(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f), Variant.FromString(VariantType.Matrix3, "1 2 3 4 5 6 7 8 9").GetMatrix3());
        Assert.Equal(default(Matrix3), Variant.FromString(VariantType.Matrix3, "1 2 3 4 5 6 7 8").GetMatrix3());
        Assert.Equal(new Matrix4(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f), Variant.FromString(VariantType.Matrix4, "1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16").GetMatrix4());
        Assert.Equal(VariantType.None, new Variant(1).Convert(VariantType.Vector3).Type);
    }

    [Fact]
    public void CString_extension_preserves_cstring_value_contract()
    {
        using var value = new CString("Native text");
        var variant = new Variant(value);

        Assert.Equal(VariantType.CString, variant.Type);
        Assert.Same(value, variant.GetCString());
        Assert.Same(value, variant.GetObject());
        Assert.Equal("Native text", variant.ToString());
        Assert.Equal(variant, Variant.FromObject(value));
        Assert.Equal(VariantType.CString, VariantTypeUtility.GetVariantType(value));
    }

    [Fact]
    public void CString_extension_uses_string_json_contract()
    {
        using var value = new CString("Native text");
        var variant = new Variant(value);

        var json = VariantTypeUtility.Serialize(variant);
        using var roundTrip = Assert.IsType<CString>(VariantTypeUtility.Deserialize(json, VariantType.CString));
        var roundTripVariant = VariantTypeUtility.DeserializeVariant(json, VariantType.CString);

        Assert.Equal("\"Native text\"", json);
        Assert.Equal("Native text", roundTrip.ConvertToString());
        Assert.Equal(VariantType.CString, roundTripVariant.Type);
        Assert.Equal("Native text", roundTripVariant.GetCString().ConvertToString());
    }
}
