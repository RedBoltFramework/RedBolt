using RedBolt;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;
namespace RedBolt.Tests;

public sealed class SerializerTests
{
    [Fact]
    public void Class_kind_and_destruction_match_cpp()
    {
        Assert.True(typeof(Serializer).IsClass);
        Assert.True(typeof(Serializer).IsAbstract);
        Assert.False(typeof(Serializer).IsSealed);
        Assert.False(typeof(Serializer).IsValueType);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(Serializer)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Serializer)));

        var dispose = typeof(Serializer).GetMethod(nameof(IDisposable.Dispose));
        Assert.NotNull(dispose);
        Assert.True(dispose.IsVirtual);
        Assert.False(dispose.IsFinal);
        Assert.Equal(typeof(Serializer), dispose.DeclaringType);

        var serializer = new ProbeSerializer();
        Serializer reference = serializer;
        reference.Dispose();
        Assert.True(serializer.IsDisposed);
    }

    [Fact]
    public void Pod_values_are_written_in_single_calls()
    {
        var serializer = new ProbeSerializer();

        Assert.True(serializer.WriteVector2(new Vector2f(1, 2)));
        Assert.Single(serializer.Writes);
        Assert.Equal(8, serializer.Writes[0].Length);

        serializer.Writes.Clear();
        Assert.True(serializer.WriteVector3(new Vector3f(1, 2, 3)));
        Assert.Single(serializer.Writes);
        Assert.Equal(12, serializer.Writes[0].Length);

        serializer.Writes.Clear();
        Assert.True(serializer.WriteMatrix3(Matrix3.Identity));
        Assert.Single(serializer.Writes);
        Assert.Equal(36, serializer.Writes[0].Length);
    }

    [Fact]
    public void Compound_writes_continue_after_failure()
    {
        var serializer = new ProbeSerializer(false, true, true);

        Assert.False(serializer.WriteBoundingBox(new BoundingBox(Vector3f.Zero, Vector3f.One)));
        Assert.Equal(2, serializer.Writes.Count);

        serializer.Writes.Clear();
        Assert.True(serializer.WriteLine("line"));
        Assert.Equal(3, serializer.Writes.Count);
    }

    [Fact]
    public void Strings_file_ids_and_vle_match_native_write_boundaries()
    {
        var serializer = new ProbeSerializer();

        Assert.True(serializer.WriteString("a\0b"));
        Assert.Equal(new byte[] { (byte)'a', 0 }, Assert.Single(serializer.Writes));

        serializer.Writes.Clear();
        Assert.True(serializer.WriteFileID("A"));
        Assert.Equal(4, serializer.Writes.Count);
        Assert.Equal(new byte[] { (byte)'A' }, serializer.Writes[0]);
        Assert.All(serializer.Writes.Skip(1), data => Assert.Equal(new byte[] { (byte)' ' }, data));

        serializer.Writes.Clear();
        Assert.True(serializer.WriteVLE(300));
        Assert.Equal(new byte[] { 0xac, 0x02 }, Assert.Single(serializer.Writes));
    }

    [Fact]
    public void Pointer_and_custom_variants_match_native_payloads()
    {
        var serializer = new ProbeSerializer();

        Assert.True(serializer.WriteVariantData(new Variant(IntPtr.Zero)));
        Assert.Equal(new byte[4], Assert.Single(serializer.Writes));

        serializer.Writes.Clear();
        Serializable serializable = new ProbeSerializable();
        Assert.True(serializer.WriteVariantData(Variant.MakeCustomValue(serializable)));
        Assert.Equal(2, serializer.Writes.Count);
        Assert.Equal(new byte[] { 42 }, serializer.Writes[1]);
    }

    private sealed class ProbeSerializer(params bool[] results) : Serializer
    {
        private int _resultIndex;

        public List<byte[]> Writes { get; } = [];
        public bool IsDisposed { get; private set; }

        public override uint Write(ReadOnlySpan<byte> data)
        {
            Writes.Add(data.ToArray());
            var result = _resultIndex >= results.Length || results[_resultIndex++];
            return result ? (uint)data.Length : 0;
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }

    private sealed class ProbeSerializable : Serializable
    {
        public override bool Save(Serializer destination)
        {
            return destination.WriteUByte(42);
        }
    }
}
