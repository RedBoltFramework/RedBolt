namespace RedBolt.Tests;

public sealed class VariantTests
{
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
