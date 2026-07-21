namespace RedBolt.Tests;

public sealed class VariantTests
{
    private static Variant ParseVariant(VariantType type, string value)
    {
        var result = default(Variant);
        result.FromString(type, value);
        return result;
    }

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

        var parsed = ParseVariant(VariantType.VoidPtr, "0x1234");
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
    public void Variant_copy_boundaries_deep_copy_owned_payloads()
    {
        var sourceVector = new VariantVector { new(new byte[] { 1, 2, 3 }) };
        var variant = new Variant(sourceVector);
        sourceVector[0].GetBuffer()[0] = 9;

        Assert.Equal(1, variant.GetVariantVector()[0].GetBuffer()[0]);

        var returnedVector = variant.Get<VariantVector>();
        returnedVector[0].GetBuffer()[0] = 8;
        Assert.Equal(1, variant.GetVariantVector()[0].GetBuffer()[0]);

        var converted = variant.Convert(VariantType.VariantVector);
        converted.GetVariantVector()[0].GetBuffer()[0] = 7;
        Assert.Equal(1, variant.GetVariantVector()[0].GetBuffer()[0]);

        var interpolated = variant.Lerp(default, 0.5f);
        interpolated.GetVariantVector()[0].GetBuffer()[0] = 6;
        Assert.Equal(1, variant.GetVariantVector()[0].GetBuffer()[0]);

        var objectVector = Assert.IsType<VariantVector>(variant.GetObject());
        objectVector[0].GetBuffer()[0] = 5;
        Assert.Equal(1, variant.GetVariantVector()[0].GetBuffer()[0]);
    }

    [Fact]
    public void Variant_vector_copies_inserted_values_and_destroys_replaced_or_erased_values()
    {
        using var source = Variant.MakeCustomValue(new CloneableCustomValue(1));
        using var replacement = Variant.MakeCustomValue(new CloneableCustomValue(2));
        using var values = new VariantVector();
        IList<Variant> valuesInterface = values;

        valuesInterface.Add(source);
        var stored = values[0].GetCustom<CloneableCustomValue>();
        Assert.NotSame(source.GetCustom<CloneableCustomValue>(), stored);

        valuesInterface[0] = replacement;
        Assert.True(stored.IsDisposed);

        var replacementStored = values[0].GetCustom<CloneableCustomValue>();
        valuesInterface.RemoveAt(0);
        Assert.True(replacementStored.IsDisposed);
        Assert.False(replacement.GetCustom<CloneableCustomValue>().IsDisposed);
    }

    [Fact]
    public void Variant_maps_copy_inserted_values_and_destroy_replaced_or_erased_values()
    {
        using var source = Variant.MakeCustomValue(new CloneableCustomValue(1));
        using var replacement = Variant.MakeCustomValue(new CloneableCustomValue(2));
        using var variantMap = new VariantMap();
        using var stringVariantMap = new StringVariantMap();

        IDictionary<StringHash, Variant> mapInterface = variantMap;
        mapInterface.Add(new StringHash("Value"), source);
        var stored = variantMap[new StringHash("Value")].GetCustom<CloneableCustomValue>();
        Assert.NotSame(source.GetCustom<CloneableCustomValue>(), stored);

        mapInterface[new StringHash("Value")] = replacement;
        Assert.True(stored.IsDisposed);
        var replacementStored = variantMap[new StringHash("Value")].GetCustom<CloneableCustomValue>();
        Assert.True(mapInterface.Remove(new StringHash("Value")));
        Assert.True(replacementStored.IsDisposed);

        IDictionary<string, Variant> stringMapInterface = stringVariantMap;
        stringMapInterface["Value"] = source;
        var stringStored = stringVariantMap["Value"].GetCustom<CloneableCustomValue>();
        stringMapInterface.Clear();
        Assert.True(stringStored.IsDisposed);
        Assert.False(source.GetCustom<CloneableCustomValue>().IsDisposed);
    }

    [Fact]
    public void Variant_uses_explicit_union_and_single_reference_storage()
    {
        var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var nestedType = typeof(Variant).GetNestedType("VariantValue", System.Reflection.BindingFlags.NonPublic);
        var referenceField = typeof(Variant).GetField("_referenceValue", bindingFlags);
        var scalar = new Variant(42);
        var text = "value";
        var reference = new Variant(text);

        Assert.NotNull(nestedType);
        Assert.Equal(System.Runtime.InteropServices.LayoutKind.Explicit, nestedType.StructLayoutAttribute!.Value);
        Assert.Equal(4 * IntPtr.Size, System.Runtime.InteropServices.Marshal.SizeOf(nestedType));
        Assert.Equal(4 * IntPtr.Size + 2 * IntPtr.Size, System.Runtime.CompilerServices.Unsafe.SizeOf<Variant>());
        Assert.NotNull(referenceField);
        Assert.Null(referenceField.GetValue(scalar));
        Assert.Same(text, referenceField.GetValue(reference));
    }

    [Fact]
    public void Mutating_methods_match_rbfx_variant_contracts()
    {
        var variant = new Variant(1);
        variant.Clear();
        Assert.True(variant.IsEmpty);

        variant.SetBuffer(new byte[] { 1, 2, 3 }, 2);
        Assert.Equal(new byte[] { 1, 2 }, variant.GetBuffer());

        variant.SetCustom(42);
        Assert.Equal(42, variant.GetCustom<int>());

        var parsed = default(Variant);
        parsed.FromString("Int", "7");
        Assert.Equal(7, parsed.GetInt());
        Assert.Equal(unchecked((uint)parsed.GetHashCode()), parsed.ToHash());
    }

    [Theory]
    [InlineData(VariantType.Vector4, "Vector4")]
    [InlineData(VariantType.Vector2i, "IntVector2")]
    [InlineData(VariantType.Vector3i, "IntVector3")]
    public void Type_names_use_native_archive_identifiers(VariantType type, string typeName)
    {
        Assert.Equal(typeName, Variant.GetTypeName(type));
        Assert.Equal(type, Variant.GetTypeFromName(typeName));
    }

    [Fact]
    public void Mutating_methods_destroy_the_previous_owned_payload()
    {
        var variant = Variant.MakeCustomValue(new CloneableCustomValue(7));
        var firstValue = variant.GetCustom<CloneableCustomValue>();
        variant.SetBuffer(new byte[] { 1, 2, 3 }, 3);
        Assert.True(firstValue.IsDisposed);

        variant.SetCustom(new CloneableCustomValue(8));
        var secondValue = variant.GetCustom<CloneableCustomValue>();
        variant.FromString(VariantType.Int, "42");
        Assert.True(secondValue.IsDisposed);

        variant.SetCustom(new CloneableCustomValue(9));
        var thirdValue = variant.GetCustom<CloneableCustomValue>();
        variant.Clear();
        Assert.True(thirdValue.IsDisposed);
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
        Assert.Equal(Vector3f.Zero, new Variant("value").GetVector3());
        Assert.Equal(Quaternion.IDENTITY, new Variant("value").GetQuaternion());
        Assert.Equal(Color.White, new Variant("value").GetColor());
        Assert.Equal(new Color(1.0f, 2.0f, 3.0f, 4.0f), new Variant(new Vector4f(1.0f, 2.0f, 3.0f, 4.0f)).GetColor());
        Assert.Equal(string.Empty, new Variant(1).GetString());
        Assert.Empty(new Variant(1).GetBuffer());
        Assert.Equal(ResourceRef.Empty, new Variant(1).GetResourceRef());
        Assert.Empty(new Variant(1).GetVariantVector());
        Assert.Equal(Matrix3.Identity, new Variant(1).GetMatrix3());
        Assert.Equal(3, new Variant(3.9f).Get<int>());
        Assert.Equal(TestEnum.Value, new Variant(1).Get<TestEnum>());
        Assert.Equal(Color.White, new Variant("value").Get<Color>());
    }

    [Fact]
    public void Resource_reference_string_conversion_requires_context()
    {
        var value = new Variant(new ResourceRef("Material", "Materials/Test"));
        Context.SetInstance(new Context());
        try
        {
            Assert.Equal(";Materials/Test", value.ToString());

            var context = new Context();
            context.Reflect<Material>();
            Context.SetInstance(context);
            Assert.Equal("Material;Materials/Test", value.ToString());
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }

    [Fact]
    public void Convert_matches_rbfx_scalar_and_string_contracts()
    {
        Assert.Equal(3, new Variant(3.9f).Convert(VariantType.Int).GetInt());
        Assert.True(new Variant(1).Convert(VariantType.Bool).GetBool());
        Assert.Equal("3", new Variant(3).Convert(VariantType.String).GetString());
        Assert.True(ParseVariant(VariantType.Bool, " yes").GetBool());
        Assert.Equal(42, ParseVariant(VariantType.Int, "42").GetInt());
        Assert.Equal(0.5f, ParseVariant(VariantType.Float, "0.5").GetFloat());
        Assert.Equal(new byte[] { 1, 2, 3 }, ParseVariant(VariantType.Buffer, "1 2 3").GetBuffer());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), ParseVariant(VariantType.Vector3, "1 2 3").GetVector3());
        Assert.Equal(Vector3f.Zero, ParseVariant(VariantType.Vector3, "1 2").GetVector3());
        Assert.Equal(new Color(1.0f, 2.0f, 3.0f), ParseVariant(VariantType.Color, "1 2 3").GetColor());
        Assert.Equal(new Quaternion(1.0f, 2.0f, 3.0f), ParseVariant(VariantType.Quaternion, "1 2 3").GetQuaternion());
        Assert.Equal(new IntRect(1, 2, 3, 4), ParseVariant(VariantType.IntRect, "1 2 3 4").GetIntRect());
        Assert.Equal(new Matrix3(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f), ParseVariant(VariantType.Matrix3, "1 2 3 4 5 6 7 8 9").GetMatrix3());
        Assert.Equal(default(Matrix3), ParseVariant(VariantType.Matrix3, "1 2 3 4 5 6 7 8").GetMatrix3());
        Assert.Equal(new Matrix4(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f), ParseVariant(VariantType.Matrix4, "1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16").GetMatrix4());
        Assert.Equal(VariantType.None, new Variant(1).Convert(VariantType.Vector3).Type);
    }

    [Fact]
    public void CString_extension_preserves_cstring_value_contract()
    {
        using var value = new CString("Native text");
        var variant = new Variant(value);

        Assert.Equal(VariantType.CString, variant.Type);
        Assert.NotSame(value, variant.GetCString());
        Assert.Equal(value, variant.GetCString());
        Assert.NotSame(value, variant.GetObject());
        Assert.Equal("Native text", variant.ToString());
        Assert.Equal(variant, Variant.FromObject(value));
        Assert.Equal(VariantType.CString, VariantTypeUtility.GetVariantType(value));

        variant.Dispose();
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

    [Fact]
    public void Custom_values_preserve_type_equality_zero_and_string_contracts()
    {
        var value = Variant.MakeCustomValue(new CustomValue { Value = 7 });
        var equalValue = Variant.MakeCustomValue(new CustomValue { Value = 7 });
        var zeroValue = Variant.MakeCustomValue(default(CustomValue));

        Assert.Equal(VariantType.Custom, value.Type);
        Assert.True(value.IsCustomType<CustomValue>());
        Assert.False(value.IsCustomType<int>());
        Assert.True(value.TryGetCustom<CustomValue>(out var retrievedValue));
        Assert.Equal(7, retrievedValue.Value);
        Assert.Equal(0, value.GetCustom<int>());
        Assert.Equal(value, equalValue);
        Assert.False(value.IsZero());
        Assert.True(zeroValue.IsZero());
        Assert.Equal("7", value.ToString());
        Assert.Equal(value, Variant.FromObject(new CustomVariantValueImpl<CustomValue>(new CustomValue { Value = 7 })));
    }

    [Fact]
    public void Custom_values_clone_as_their_concrete_managed_type()
    {
        var original = new CustomVariantValueImpl<CustomValue>(new CustomValue { Value = 7 });

        var clone = Assert.IsType<CustomVariantValueImpl<CustomValue>>(((ICloneable)original).Clone());

        Assert.Equal(original, clone);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Custom_values_copy_and_destroy_owned_value_objects()
    {
        var source = new CloneableCustomValue(7);
        var original = new CustomVariantValueImpl<CloneableCustomValue>(source);
        var clone = Assert.IsType<CustomVariantValueImpl<CloneableCustomValue>>(original.Clone());

        source.Value = 8;

        Assert.Equal(7, original.TypedValue.Value);
        Assert.NotSame(source, original.TypedValue);
        Assert.NotSame(original.TypedValue, clone.TypedValue);

        var originalValue = original.TypedValue;
        var cloneValue = clone.TypedValue;
        original.Dispose();
        clone.Dispose();

        Assert.True(originalValue.IsDisposed);
        Assert.True(cloneValue.IsDisposed);
    }

    [Fact]
    public void Optional_retrieval_matches_empty_and_value_contracts()
    {
        var empty = default(Variant).GetOptional<int>();
        var value = new Variant(42).GetOptional<int>();

        Assert.Null(empty);
        Assert.Equal(42, value);
    }

    private readonly record struct CustomValue(int Value)
    {
        public override string ToString() => Value.ToString();
    }

    private sealed class CloneableCustomValue(int value) : ICloneable, IDisposable
    {
        public int Value = value;
        public bool IsDisposed;

        public object Clone() => new CloneableCustomValue(Value);

        public void Dispose() => IsDisposed = true;
    }
}
