namespace RedBolt.Tests;

public sealed class VariantTests
{
    private enum TestEnum
    {
        None,
        Value
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
