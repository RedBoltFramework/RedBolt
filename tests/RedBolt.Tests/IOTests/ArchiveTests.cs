using RedBolt;
using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class ArchiveTests
{
    private enum UnsignedLongEnum : ulong
    {
        Zero
    }

    private static AttributePrefab CreateAttribute(string name, Variant value)
    {
        var result = new AttributePrefab(name);
        result.SetValue(ref value);
        return result;
    }

    [Theory]
    [InlineData("Root", true)]
    [InlineData("_private.Item:1", true)]
    [InlineData("", false)]
    [InlineData("1invalid", false)]
    [InlineData("invalid-name", false)]
    public void Archive_validates_block_and_element_names(string name, bool expected)
    {
        Assert.Equal(expected, Archive.ValidateName(name));
    }

    [Fact]
    public void Archive_block_closes_once_when_disposed()
    {
        var archive = new TestArchive();

        var block = archive.OpenArrayBlock("Root", 3);
        Assert.True(block.IsValid);
        Assert.Equal(3u, block.SizeHint);
        block.Dispose();
        block.Dispose();

        Assert.Equal(1, archive.BeginCount);
        Assert.Equal(1, archive.EndCount);
        Assert.Equal(ArchiveBlockType.Array, archive.LastBlockType);
        Assert.Equal(3u, archive.LastSizeHint);
    }

    [Fact]
    public void Archive_block_is_extensible_noncopyable_reference_class()
    {
        Assert.True(typeof(ArchiveBlock).IsClass);
        Assert.False(typeof(ArchiveBlock).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(ArchiveBlock)));
    }

    [Fact]
    public void Archive_block_move_construction_transfers_ownership()
    {
        var archive = new TestArchive();
        var source = archive.OpenArrayBlock("Root", 3);

        using var moved = new ArchiveBlock(source);

        Assert.False(source.IsValid);
        Assert.True(moved.IsValid);
        Assert.Equal(3u, moved.SizeHint);
        source.Dispose();
        Assert.Equal(0, archive.EndCount);
    }

    [Fact]
    public void Archive_block_swap_exchanges_ownership_and_size_hints()
    {
        var archive = new TestArchive();
        using var first = archive.OpenArrayBlock("First", 1);
        using var second = archive.OpenArrayBlock("Second", 2);

        first.Swap(second);

        Assert.Equal(2u, first.SizeHint);
        Assert.Equal(1u, second.SizeHint);
    }

    [Fact]
    public void Archive_block_move_assignment_swaps_ownership_and_size_hints()
    {
        var archive = new TestArchive();
        using var first = archive.OpenArrayBlock("First", 1);
        using var second = archive.OpenArrayBlock("Second", 2);

        Assert.Same(first, first.MoveAssign(second));

        Assert.Equal(2u, first.SizeHint);
        Assert.Equal(1u, second.SizeHint);
    }

    [Fact]
    public void Archive_virtual_dispose_dispatches_through_base_reference()
    {
        Archive archive = new TestArchive();

        archive.Dispose();

        Assert.True(((TestArchive)archive).IsDisposed);
    }

    [Fact]
    public void Human_readable_archive_rejects_invalid_hex_without_mutating_destination()
    {
        var output = new TestJSONOutputArchive();
        var source = new byte[] { 1, 2, 3 };
        using (output.OpenUnorderedBlock("Root"))
            output.SerializeBytes("data", source);

        var input = new TestJSONInputArchive(output.ToJsonString().Replace("010203", "invalid"));
        var destination = new byte[] { 4, 5, 6 };
        using (input.OpenUnorderedBlock("Root"))
            Assert.Throws<ArchiveException>(() => input.SerializeBytes("data", destination));

        Assert.Equal(new byte[] { 4, 5, 6 }, destination);
    }

    [Fact]
    public void Archive_serializes_cstring_variant_as_string()
    {
        using var cstring = new CString("Native text");
        var value = new Variant(cstring);
        var archive = new TestArchive();

        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, VariantType.CString);

        Assert.Equal(("value", "Native text"), Assert.Single(archive.StringValues));
    }

    [Fact]
    public void Human_readable_archive_round_trips_resource_references_as_strings()
    {
        var output = new TestJSONOutputArchive();
        var resourceRef = new ResourceRef(StringHash.Empty, "Texture.xml");
        var resourceRefList = new ResourceRefList(StringHash.Empty, ["One.xml", "Two.xml"]);
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeValue(output, "resource", ref resourceRef);
            ArchiveSerialization.SerializeValue(output, "resources", ref resourceRefList);
        }

        var input = new TestJSONInputArchive(output.ToJsonString());
        var loadedResourceRef = ResourceRef.Empty;
        var loadedResourceRefList = ResourceRefList.Empty;
        using (input.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeValue(input, "resource", ref loadedResourceRef);
            ArchiveSerialization.SerializeValue(input, "resources", ref loadedResourceRefList);
        }

        Assert.Equal(resourceRef, loadedResourceRef);
        Assert.Equal(resourceRefList, loadedResourceRefList);
    }

    [Fact]
    public void Binary_archive_round_trips_resource_reference()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var source = new ResourceRef("BinaryFile", "Data/dependency.bin");
        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(output, "resource", ref source);
        output.Flush();

        buffer.Seek(0);
        var destination = ResourceRef.Empty;
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(input, "resource", ref destination);
        input.Flush();

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Binary_archive_round_trips_resource_reference_variant()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var source = new Variant(new ResourceRef("BinaryFile", "Data/dependency.bin"));
        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantInBlock(output, ref source);
        output.Flush();

        buffer.Seek(0);
        var destination = default(Variant);
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantInBlock(input, ref destination);
        input.Flush();

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Binary_archive_round_trips_resource_reference_attribute()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var source = CreateAttribute(
            "Dependency", new Variant(new ResourceRef("BinaryFile", "Data/dependency.bin")));
        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            source.SerializeInBlock(output);
        output.Flush();

        buffer.Seek(0);
        var destination = new AttributePrefab();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            destination.SerializeInBlock(input);
        input.Flush();

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Binary_archive_round_trips_nested_resource_reference_attribute()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var source = new NodePrefab();
        var child = new NodePrefab();
        var component = new SerializablePrefab();
        component.SetType("Node");
        component.Attributes.Add(CreateAttribute(
            "Dependency", new Variant(new ResourceRef("BinaryFile", "Data/dependency.bin"))));
        child.Components.Add(component);
        source.Children.Add(child);
        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            source.SerializeInBlock(output);
        output.Flush();

        buffer.Seek(0);
        var destination = new NodePrefab();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            destination.SerializeInBlock(input);
        input.Flush();

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Binary_file_round_trips_nested_resource_reference_attribute()
    {
        var source = new NodePrefab();
        var child = new NodePrefab();
        var component = new SerializablePrefab();
        component.SetType("Node");
        component.Attributes.Add(CreateAttribute(
            "Dependency", new Variant(new ResourceRef("BinaryFile", "Data/dependency.bin"))));
        child.Components.Add(component);
        source.Children.Add(child);
        var file = new BinaryFile();
        Assert.True(file.SaveObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("Root");
            source.SerializeInBlock(archive);
        }));

        var destination = new NodePrefab();
        Assert.True(file.LoadObjectCallback(archive =>
        {
            using var block = archive.OpenUnorderedBlock("Root");
            destination.SerializeInBlock(archive);
        }));

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Binary_file_preserves_native_extensible_class_kind()
    {
        Assert.False(typeof(BinaryFile).IsSealed);
    }

    [Fact]
    public void Human_readable_archive_preserves_unknown_enum_as_number()
    {
        var output = new TestJSONOutputArchive();
        var value = (VariantType)123;
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeEnum(output, "type", ref value, Variant.GetTypeNameList());

        var input = new TestJSONInputArchive(output.ToJsonString());
        var loadedValue = VariantType.None;
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeEnum(input, "type", ref loadedValue, Variant.GetTypeNameList());

        Assert.Equal((VariantType)123, loadedValue);
    }

    [Fact]
    public void Human_readable_archive_preserves_unsigned_long_enum_as_number()
    {
        var output = new TestJSONOutputArchive();
        var value = (UnsignedLongEnum)(ulong.MaxValue - 1);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeEnum(output, "type", ref value, ["Zero"]);

        var input = new TestJSONInputArchive(output.ToJsonString());
        var loadedValue = UnsignedLongEnum.Zero;
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeEnum(input, "type", ref loadedValue, ["Zero"]);

        Assert.Equal(value, loadedValue);
    }

    [Fact]
    public void Variant_type_uses_native_unsigned_char_storage()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(VariantType)));
    }

    [Fact]
    public void Human_readable_variant_input_default_constructs_compound_value()
    {
        var output = new TestJSONOutputArchive();
        var source = new Variant(Matrix3.Identity);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantAsType(output, "value", ref source, VariantType.Matrix3);

        var input = new TestJSONInputArchive(output.ToJsonString().Replace("1 0 0 0 1 0 0 0 1", "9"));
        var destination = new Variant(42);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantAsType(input, "value", ref destination, VariantType.Matrix3);

        Assert.Equal(new Matrix3(9, 0, 0, 0, 1, 0, 0, 0, 1), destination.GetMatrix3());
    }

    [Fact]
    public void Human_readable_archive_round_trips_direct_compound_values()
    {
        var vector2 = new Vector2f(1.0f, 2.0f);
        var doubleVector2 = new Vector2d(3.0, 4.0);
        var doubleVector3 = new Vector3d(5.0, 6.0, 7.0);
        var matrix3 = new Matrix3(1, 2, 3, 4, 5, 6, 7, 8, 9);
        var matrix4 = Matrix4.Identity;
        var rect = new Rect(1.0f, 2.0f, 3.0f, 4.0f);
        var quaternion = new Quaternion(10.0f, 20.0f, 30.0f);
        var color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        var intVector2 = new Vector2i(11, 12);
        var intVector3 = new Vector3i(13, 14, 15);
        var intRect = new IntRect(16, 17, 18, 19);

        var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeValue(output, "vector2", ref vector2);
            ArchiveSerialization.SerializeValue(output, "doubleVector2", ref doubleVector2);
            ArchiveSerialization.SerializeValue(output, "doubleVector3", ref doubleVector3);
            ArchiveSerialization.SerializeValue(output, "matrix3", ref matrix3);
            ArchiveSerialization.SerializeValue(output, "matrix4", ref matrix4);
            ArchiveSerialization.SerializeValue(output, "rect", ref rect);
            ArchiveSerialization.SerializeValue(output, "quaternion", ref quaternion);
            ArchiveSerialization.SerializeValue(output, "color", ref color);
            ArchiveSerialization.SerializeValue(output, "intVector2", ref intVector2);
            ArchiveSerialization.SerializeValue(output, "intVector3", ref intVector3);
            ArchiveSerialization.SerializeValue(output, "intRect", ref intRect);
        }

        var loadedVector2 = default(Vector2f);
        var loadedDoubleVector2 = default(Vector2d);
        var loadedDoubleVector3 = default(Vector3d);
        var loadedMatrix3 = default(Matrix3);
        var loadedMatrix4 = default(Matrix4);
        var loadedRect = default(Rect);
        var loadedQuaternion = default(Quaternion);
        var loadedColor = default(Color);
        var loadedIntVector2 = default(Vector2i);
        var loadedIntVector3 = default(Vector3i);
        var loadedIntRect = default(IntRect);
        var input = new TestJSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeValue(input, "vector2", ref loadedVector2);
            ArchiveSerialization.SerializeValue(input, "doubleVector2", ref loadedDoubleVector2);
            ArchiveSerialization.SerializeValue(input, "doubleVector3", ref loadedDoubleVector3);
            ArchiveSerialization.SerializeValue(input, "matrix3", ref loadedMatrix3);
            ArchiveSerialization.SerializeValue(input, "matrix4", ref loadedMatrix4);
            ArchiveSerialization.SerializeValue(input, "rect", ref loadedRect);
            ArchiveSerialization.SerializeValue(input, "quaternion", ref loadedQuaternion);
            ArchiveSerialization.SerializeValue(input, "color", ref loadedColor);
            ArchiveSerialization.SerializeValue(input, "intVector2", ref loadedIntVector2);
            ArchiveSerialization.SerializeValue(input, "intVector3", ref loadedIntVector3);
            ArchiveSerialization.SerializeValue(input, "intRect", ref loadedIntRect);
        }

        Assert.Equal(vector2, loadedVector2);
        Assert.Equal(doubleVector2, loadedDoubleVector2);
        Assert.Equal(doubleVector3, loadedDoubleVector3);
        Assert.Equal(matrix3, loadedMatrix3);
        Assert.Equal(matrix4, loadedMatrix4);
        Assert.Equal(rect, loadedRect);
        Assert.True(quaternion.ApproximatelyEquivalent(loadedQuaternion, 0.00001f));
        Assert.Equal(color, loadedColor);
        Assert.Equal(intVector2, loadedIntVector2);
        Assert.Equal(intVector3, loadedIntVector3);
        Assert.Equal(intRect, loadedIntRect);
    }

    [Fact]
    public void Consume_archive_exception_returns_serialization_status()
    {
        Assert.True(ArchiveSerialization.ConsumeArchiveException(() => { }));
        Assert.False(ArchiveSerialization.ConsumeArchiveException(
            () => throw new ArchiveException("serialization failure"), false));
        Assert.Throws<InvalidOperationException>(() => ArchiveSerialization.ConsumeArchiveException(
            () => throw new InvalidOperationException("unrelated failure"), false));
        Assert.Throws<NotSupportedException>(() => ArchiveSerialization.ConsumeArchiveException(
            () => throw new NotSupportedException("unrelated failure"), false));
    }

    [Fact]
    public void Serialize_value_opens_variant_block()
    {
        var archive = new TestArchive();
        var value = new Variant(42);

        ArchiveSerialization.SerializeValue(archive, "variant", ref value);

        Assert.Equal(1, archive.BeginCount);
        Assert.Equal(1, archive.EndCount);
        Assert.Equal(ArchiveBlockType.Unordered, archive.LastBlockType);
    }

    [Fact]
    public void Binary_archive_serializes_compound_variant_as_raw_bytes()
    {
        var vector = new Vector3f(1.0f, 2.0f, 3.0f);
        var value = new Variant(vector);
        var archive = new TestArchive();

        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, VariantType.Vector3);

        var serializedValue = Assert.Single(archive.ByteValues);
        Assert.Equal("value", serializedValue.Name);
        Assert.Equal(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref vector, 1)).ToArray(), serializedValue.Value);
    }

    [Fact]
    public void Archive_serializes_buffer_variant_as_sized_data_block()
    {
        var value = new Variant(new byte[] { 1, 2, 3 });
        var archive = new TestArchive();

        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, VariantType.Buffer);

        var serializedValue = Assert.Single(archive.ByteValues);
        Assert.Equal("data", serializedValue.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, serializedValue.Value);
        Assert.Equal(("size", 3u), Assert.Single(archive.VleValues));
    }

    [Fact]
    public void Archive_serializes_custom_variant_through_archive_serializable_contract()
    {
        var customValue = new CustomArchiveValue();
        var value = Variant.MakeCustomValue(customValue);
        var archive = new TestArchive();

        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, VariantType.Custom);

        Assert.True(customValue.WasSerialized);
    }

    [Fact]
    public void Binary_archive_round_trips_compound_and_cstring_variants()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var vector = new Variant(new Vector3f(1.0f, 2.0f, 3.0f));
        using var cstring = new CString("Native text");
        var nativeString = new Variant(cstring);

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeVariantInBlock(output, ref vector);
            ArchiveSerialization.SerializeVariantInBlock(output, ref nativeString);
        }
        output.Flush();

        buffer.Seek(0);
        using var input = new BinaryInputArchive(context, buffer);
        var loadedVector = default(Variant);
        var loadedNativeString = default(Variant);
        using (input.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeVariantInBlock(input, ref loadedVector);
            ArchiveSerialization.SerializeVariantInBlock(input, ref loadedNativeString);
        }
        input.Flush();

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), loadedVector.GetVector3());
        Assert.Equal("Native text", loadedNativeString.GetCString().ConvertToString());
    }

    [Fact]
    public void Binary_archive_round_trips_variant_curve()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var curve = new VariantCurve { Name = "Curve", Interpolation = KeyFrameInterpolation.Linear };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 3.0f });
        curve.Commit();
        var value = new Variant(curve);

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantInBlock(output, ref value);
        output.Flush();

        buffer.Seek(0);
        using var input = new BinaryInputArchive(context, buffer);
        var loadedValue = default(Variant);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantInBlock(input, ref loadedValue);
        input.Flush();

        var loadedCurve = loadedValue.GetVariantCurve();
        Assert.Equal("Curve", loadedCurve.Name);
        Assert.Equal(VariantType.Float, loadedCurve.Type);
        Assert.Equal(2u, loadedCurve.NumKeyFrames);
        Assert.Equal(2.0f, loadedCurve.Sample(0.5f).GetFloat());
    }

    [Fact]
    public void Binary_archive_round_trips_tetrahedral_mesh()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var mesh = new TetrahedralMesh();
        mesh.Vertices.Add(new Vector3f(1.0f, 2.0f, 3.0f));
        mesh.Vertices.Add(new Vector3f(4.0f, 5.0f, 6.0f));
        mesh.HullNormals.Add(new Vector3f(0.0f, 1.0f, 0.0f));
        var tetrahedron = new Tetrahedron
        {
            Matrix = new Matrix3x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)
        };
        tetrahedron.Indices[0] = 0;
        tetrahedron.Indices[1] = 1;
        tetrahedron.Indices[2] = 2;
        tetrahedron.Indices[3] = 3;
        tetrahedron.Neighbors[0] = 4;
        tetrahedron.Neighbors[1] = 5;
        tetrahedron.Neighbors[2] = 6;
        tetrahedron.Neighbors[3] = 7;
        mesh.Tetrahedrons.Add(tetrahedron);
        mesh.NumInnerTetrahedrons = 1;

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(output, "mesh", ref mesh);
        output.Flush();

        buffer.Seek(0);
        var loadedMesh = new TetrahedralMesh();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(input, "mesh", ref loadedMesh);
        input.Flush();

        Assert.Equal(mesh.Vertices, loadedMesh.Vertices);
        Assert.Equal(mesh.HullNormals, loadedMesh.HullNormals);
        Assert.Equal(mesh.NumInnerTetrahedrons, loadedMesh.NumInnerTetrahedrons);
        Assert.Equal(mesh.Tetrahedrons.Count, loadedMesh.Tetrahedrons.Count);
        Assert.Equal(mesh.Tetrahedrons[0].Indices, loadedMesh.Tetrahedrons[0].Indices);
        Assert.Equal(mesh.Tetrahedrons[0].Neighbors, loadedMesh.Tetrahedrons[0].Neighbors);
        Assert.Equal(mesh.Tetrahedrons[0].Matrix, loadedMesh.Tetrahedrons[0].Matrix);
    }

    [Fact]
    public void Binary_archive_serializes_tetrahedrons_as_pod_bytes()
    {
        var archive = new TestArchive();
        var mesh = new TetrahedralMesh();
        mesh.Tetrahedrons.Add(new Tetrahedron());

        ArchiveSerialization.SerializeValue(archive, "mesh", ref mesh);

        Assert.Contains(archive.ByteValues, item => item.Value.Length == 80);
    }

    [Fact]
    public void Human_readable_primitive_array_input_preserves_unspecified_components()
    {
        var output = new TestJSONOutputArchive();
        var source = new Vector3f(1.0f, 2.0f, 3.0f);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(output, "value", ref source);

        var input = new TestJSONInputArchive(output.ToJsonString().Replace("1 2 3", "9"));
        var destination = new Vector3f(4.0f, 5.0f, 6.0f);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(input, "value", ref destination);

        Assert.Equal(new Vector3f(9.0f, 5.0f, 6.0f), destination);
    }

    [Fact]
    public void Binary_archive_round_trips_light_probe_baked_data()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        var data = new LightProbeCollectionBakedData();
        data.SphericalHarmonics.Add(new SphericalHarmonicsDot9(new Vector3f(1.0f, 2.0f, 3.0f)));

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(output, "data", ref data);
        output.Flush();

        buffer.Seek(0);
        var loadedData = new LightProbeCollectionBakedData();
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(input, "data", ref loadedData);
        input.Flush();

        Assert.Equal(data.SphericalHarmonics, loadedData.SphericalHarmonics);
        Assert.Single(loadedData.Ambient);
        Assert.Equal(data.SphericalHarmonics[0].GetDebugColor().ToVector3(), loadedData.Ambient[0]);
    }

    [Fact]
    public void Binary_archive_round_trips_optional_and_pair_helpers()
    {
        var context = new Context();
        using var buffer = new VectorBuffer();
        uint? strictlyOptional = 42;
        uint? optional = 7;
        var pair = (First: 11u, Second: "pair");

        using var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeStrictlyOptionalValue(output, "strict", ref strictlyOptional, null,
                SerializeNullableUInt);
            ArchiveSerialization.SerializeValue(output, "optional", ref optional, SerializeUInt);
            ArchiveSerialization.SerializeValue(output, "pair", ref pair, SerializeUInt, SerializeString);
        }
        output.Flush();

        buffer.Seek(0);
        strictlyOptional = null;
        optional = null;
        pair = default;
        using var input = new BinaryInputArchive(context, buffer);
        using (input.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeStrictlyOptionalValue(input, "strict", ref strictlyOptional, null,
                SerializeNullableUInt);
            ArchiveSerialization.SerializeValue(input, "optional", ref optional, SerializeUInt);
            ArchiveSerialization.SerializeValue(input, "pair", ref pair, SerializeUInt, SerializeString);
        }
        input.Flush();

        Assert.Equal((uint?)42, strictlyOptional);
        Assert.Equal((uint?)7, optional);
        Assert.Equal((11u, "pair"), pair);
        Assert.True(pair.GetType().IsValueType);
    }

    [Fact]
    public void Unordered_archive_omits_and_restores_default_optional_values()
    {
        var output = new TestJSONOutputArchive();
        var value = 0u;
        uint? strictlyOptional = null;
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeOptionalValue(output, "value", ref value, 0u, SerializeUInt);
            ArchiveSerialization.SerializeStrictlyOptionalValue(output, "strict", ref strictlyOptional, null,
                SerializeNullableUInt);
        }

        var json = output.ToJsonString();
        Assert.DoesNotContain("value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("strict", json, StringComparison.Ordinal);

        value = 9;
        strictlyOptional = 10;
        var input = new TestJSONInputArchive(json);
        using (input.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeOptionalValue(input, "value", ref value, 0u, SerializeUInt);
            ArchiveSerialization.SerializeStrictlyOptionalValue(input, "strict", ref strictlyOptional, null,
                SerializeNullableUInt);
        }

        Assert.Equal(0u, value);
        Assert.Null(strictlyOptional);
    }

    private static void SerializeUInt(Archive archive, string name, ref uint value)
    {
        ArchiveSerialization.SerializeValue(archive, name, ref value);
    }

    private static void SerializeString(Archive archive, string name, ref string value)
    {
        ArchiveSerialization.SerializeValue(archive, name, ref value);
    }

    private static void SerializeNullableUInt(Archive archive, string name, ref uint? value)
    {
        if (!value.HasValue)
            value = 0;
        var serializedValue = value.Value;
        ArchiveSerialization.SerializeValue(archive, name, ref serializedValue);
        value = serializedValue;
    }

    private sealed class TestArchive : Archive
    {
        private readonly Context _context = new();

        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }
        public ArchiveBlockType LastBlockType { get; private set; }
        public uint LastSizeHint { get; private set; }
        public bool IsDisposed { get; private set; }
        public List<(string Name, string Value)> StringValues { get; } = [];
        public List<(string Name, byte[] Value)> ByteValues { get; } = [];
        public List<(string Name, uint Value)> VleValues { get; } = [];

        public override Context Context => _context;
        public override string Name => string.Empty;
        public override uint Checksum => 0;
        public override bool IsInput => false;
        public override bool IsHumanReadable => false;
        public override bool IsUnorderedAccessSupportedInCurrentBlock => false;
        public override bool IsEof => false;
        public override string CurrentBlockPath => string.Empty;

        public override bool HasElementOrBlock(string name) => false;

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public override void BeginBlock(string name, ref uint sizeHint, bool safe, ArchiveBlockType type)
        {
            ++BeginCount;
            LastBlockType = type;
            LastSizeHint = sizeHint;
        }

        public override void EndBlock() => ++EndCount;
        public override void Flush() { }
        public override void Serialize(string name, ref bool value) { }
        public override void Serialize(string name, ref sbyte value) { }
        public override void Serialize(string name, ref byte value) { }
        public override void Serialize(string name, ref short value) { }
        public override void Serialize(string name, ref ushort value) { }
        public override void Serialize(string name, ref int value) { }
        public override void Serialize(string name, ref uint value) { }
        public override void Serialize(string name, ref long value) { }
        public override void Serialize(string name, ref ulong value) { }
        public override void Serialize(string name, ref float value) { }
        public override void Serialize(string name, ref double value) { }
        public override void Serialize(string name, ref string value) => StringValues.Add((name, value));
        public override void SerializeBytes(string name, Span<byte> bytes) => ByteValues.Add((name, bytes.ToArray()));
        public override void SerializeVle(string name, ref uint value) => VleValues.Add((name, value));
        public override uint SerializeVersion(uint version) => version;
    }

    private sealed class CustomArchiveValue : IArchiveSerializable
    {
        public bool WasSerialized { get; private set; }

        public void SerializeInBlock(Archive archive)
        {
            WasSerialized = true;
        }
    }
}
