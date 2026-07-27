using SystemMath = global::System.Math;

using RedBolt;
using RedBolt.Core;
using RedBolt.IO;

namespace RedBolt.Tests;

public sealed class DeserializerTests
{
    [Fact]
    public void Class_kind_and_construction_match_rbfx()
    {
        Assert.True(typeof(Deserializer).IsClass);
        Assert.True(typeof(Deserializer).IsAbstract);
        Assert.False(typeof(Deserializer).IsValueType);
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(Deserializer)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Deserializer)));

        var empty = new ProbeDeserializer();
        Assert.Equal(0u, empty.Position);
        Assert.Equal(0u, empty.Size);

        var sized = new ProbeDeserializer([1, 2, 3]);
        Assert.Equal(0u, sized.Position);
        Assert.Equal(3u, sized.Size);
    }

    [Fact]
    public void Seek_relative_preserves_unsigned_native_arithmetic()
    {
        var deserializer = new ProbeDeserializer();

        deserializer.SeekRelative(-1);

        Assert.Equal(uint.MaxValue, deserializer.LastSeekPosition);
    }

    [Fact]
    public void Compound_values_are_read_with_one_bulk_read()
    {
        var vectorBuffer = new VectorBuffer();
        vectorBuffer.WriteVector3(new Vector3f(1.0f, 2.0f, 3.0f));
        var deserializer = new ProbeDeserializer(vectorBuffer.GetBuffer());

        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), deserializer.ReadVector3());
        Assert.Equal(1, deserializer.ReadCount);

        vectorBuffer = new VectorBuffer();
        vectorBuffer.WriteVariantData(new Variant(new Vector3d(4.0, 5.0, 6.0)));
        deserializer = new ProbeDeserializer(vectorBuffer.GetBuffer());

        Assert.Equal(new Vector3d(4.0, 5.0, 6.0),
            deserializer.ReadVariant(VariantType.DoubleVector3).GetDoubleVector3());
        Assert.Equal(1, deserializer.ReadCount);
    }

    [Fact]
    public void Pointer_variant_consumes_payload_and_returns_null_void_pointer()
    {
        var deserializer = new ProbeDeserializer([1, 2, 3, 4]);

        var value = deserializer.ReadVariant(VariantType.Ptr);

        Assert.Equal(4u, deserializer.Position);
        Assert.Equal(VariantType.VoidPtr, value.Type);
        Assert.Equal(IntPtr.Zero, value.GetVoidPtr());
    }

    [Fact]
    public void Dispose_dispatches_through_deserializer_reference()
    {
        Deserializer deserializer = new ProbeDeserializer();

        deserializer.Dispose();

        Assert.True(((ProbeDeserializer)deserializer).IsDisposed);
    }

    private sealed class ProbeDeserializer : Deserializer
    {
        private readonly byte[] _data;

        public ProbeDeserializer()
        {
            _data = [];
        }

        public ProbeDeserializer(byte[] data)
            : base((uint)data.Length)
        {
            _data = data;
        }

        public int ReadCount { get; private set; }

        public uint LastSeekPosition { get; private set; }

        public bool IsDisposed { get; private set; }

        public override uint Read(Span<byte> destination)
        {
            ++ReadCount;
            var size = SystemMath.Min(destination.Length, _data.Length - (int)_position);
            _data.AsSpan((int)_position, size).CopyTo(destination);
            _position += (uint)size;
            return (uint)size;
        }

        public override uint Seek(uint position)
        {
            LastSeekPosition = position;
            _position = SystemMath.Min(position, _size);
            return _position;
        }

        public override uint Write(ReadOnlySpan<byte> data) => 0;

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
    }
}
