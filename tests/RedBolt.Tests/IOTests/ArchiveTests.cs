using RedBolt;
using System.Runtime.InteropServices;

namespace RedBolt.Tests;

public sealed class ArchiveTests
{
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
    public void Archive_serializes_cstring_variant_as_string()
    {
        using var cstring = new CString("Native text");
        var value = new Variant(cstring);
        var archive = new TestArchive();

        ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, VariantType.CString);

        Assert.Equal(("value", "Native text"), Assert.Single(archive.StringValues));
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
        var buffer = new VectorBuffer();
        var vector = new Variant(new Vector3f(1.0f, 2.0f, 3.0f));
        using var cstring = new CString("Native text");
        var nativeString = new Variant(cstring);

        var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
        {
            ArchiveSerialization.SerializeVariantInBlock(output, ref vector);
            ArchiveSerialization.SerializeVariantInBlock(output, ref nativeString);
        }
        output.Flush();

        buffer.Seek(0);
        var input = new BinaryInputArchive(context, buffer);
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
        var buffer = new VectorBuffer();
        var curve = new VariantCurve { Name = "Curve", Interpolation = KeyFrameInterpolation.Linear };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 3.0f });
        curve.Commit();
        var value = new Variant(curve);

        var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeVariantInBlock(output, ref value);
        output.Flush();

        buffer.Seek(0);
        var input = new BinaryInputArchive(context, buffer);
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
        var buffer = new VectorBuffer();
        var mesh = new TetrahedralMesh();
        mesh.Vertices.Add(new Vector3f(1.0f, 2.0f, 3.0f));
        mesh.Vertices.Add(new Vector3f(4.0f, 5.0f, 6.0f));
        mesh.HullNormals.Add(new Vector3f(0.0f, 1.0f, 0.0f));
        var tetrahedron = new Tetrahedron
        {
            Indices = [0, 1, 2, 3],
            Neighbors = [4, 5, 6, 7],
            Matrix = new Matrix3x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)
        };
        mesh.Tetrahedrons.Add(tetrahedron);
        mesh.NumInnerTetrahedrons = 1;

        var output = new BinaryOutputArchive(context, buffer);
        using (output.OpenUnorderedBlock("Root"))
            ArchiveSerialization.SerializeValue(output, "mesh", ref mesh);
        output.Flush();

        buffer.Seek(0);
        var loadedMesh = new TetrahedralMesh();
        var input = new BinaryInputArchive(context, buffer);
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

    private sealed class TestArchive : Archive
    {
        private readonly Context _context = new();

        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }
        public ArchiveBlockType LastBlockType { get; private set; }
        public uint LastSizeHint { get; private set; }
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
