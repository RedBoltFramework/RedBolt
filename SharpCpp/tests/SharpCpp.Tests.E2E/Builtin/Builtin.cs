using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Builtin;

public sealed class BuiltinTests
{
    [Fact]
    public void AddNumbers_roundtrips_int32_arguments()
    {
        Assert.Equal(42, E2EBindings.AddNumbers(19, 23));
    }

    [Fact]
    public void Bool_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.True(E2EBindings.RoundtripBool(true));

        var inValue = false;
        Assert.False(E2EBindings.EchoInBool(in inValue));

        var refValue = false;
        E2EBindings.ToggleRefBool(ref refValue);
        Assert.True(refValue);

        E2EBindings.WriteOutBool(out var outValue);
        Assert.True(outValue);
    }

    [Fact]
    public void Bool_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        var inValue = false;
        Assert.False(E2EBindings.EchoMarkedInBool(in inValue));

        var refValue = false;
        E2EBindings.ReplaceMarkedRefBool(ref refValue);
        Assert.True(refValue);

        E2EBindings.WriteMarkedOutBool(out var outValue);
        Assert.True(outValue);
    }

    [Fact]
    public void Char_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((byte)201, E2EBindings.RoundtripChar((byte)201));

        byte inValue = 17;
        Assert.Equal((byte)17, E2EBindings.EchoInChar(in inValue));

        byte refValue = 41;
        E2EBindings.IncrementRefChar(ref refValue);
        Assert.Equal((byte)42, refValue);

        E2EBindings.WriteOutChar(out var outValue);
        Assert.Equal((byte)90, outValue);
    }

    [Fact]
    public void Char_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        byte inValue = 17;
        Assert.Equal((byte)17, E2EBindings.EchoMarkedInChar(in inValue));

        byte refValue = 41;
        E2EBindings.ReplaceMarkedRefChar(ref refValue);
        Assert.Equal((byte)42, refValue);

        E2EBindings.WriteMarkedOutChar(out var outValue);
        Assert.Equal((byte)90, outValue);
    }

    [Fact]
    public void Int8_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((sbyte)-11, E2EBindings.RoundtripInt8((sbyte)-11));

        sbyte inValue = 7;
        Assert.Equal((sbyte)7, E2EBindings.EchoInInt8(in inValue));

        sbyte refValue = 41;
        E2EBindings.IncrementRefInt8(ref refValue);
        Assert.Equal((sbyte)42, refValue);

        E2EBindings.WriteOutInt8(out var outValue);
        Assert.Equal((sbyte)-12, outValue);
    }

    [Fact]
    public void Int8_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        sbyte inValue = 7;
        Assert.Equal((sbyte)7, E2EBindings.EchoMarkedInInt8(in inValue));

        sbyte refValue = 41;
        E2EBindings.ReplaceMarkedRefInt8(ref refValue);
        Assert.Equal((sbyte)42, refValue);

        E2EBindings.WriteMarkedOutInt8(out var outValue);
        Assert.Equal((sbyte)-12, outValue);
    }

    [Fact]
    public void UInt8_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((byte)201, E2EBindings.RoundtripUint8((byte)201));

        byte inValue = 17;
        Assert.Equal((byte)17, E2EBindings.EchoInUint8(in inValue));

        byte refValue = 41;
        E2EBindings.IncrementRefUint8(ref refValue);
        Assert.Equal((byte)42, refValue);

        E2EBindings.WriteOutUint8(out var outValue);
        Assert.Equal((byte)240, outValue);
    }

    [Fact]
    public void UInt8_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        byte inValue = 17;
        Assert.Equal((byte)17, E2EBindings.EchoMarkedInUint8(in inValue));

        byte refValue = 41;
        E2EBindings.ReplaceMarkedRefUint8(ref refValue);
        Assert.Equal((byte)42, refValue);

        E2EBindings.WriteMarkedOutUint8(out var outValue);
        Assert.Equal((byte)240, outValue);
    }

    [Fact]
    public void Int16_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((short)-3210, E2EBindings.RoundtripInt16((short)-3210));

        short inValue = 1234;
        Assert.Equal((short)1234, E2EBindings.EchoInInt16(in inValue));

        short refValue = 41;
        E2EBindings.IncrementRefInt16(ref refValue);
        Assert.Equal((short)42, refValue);

        E2EBindings.WriteOutInt16(out var outValue);
        Assert.Equal((short)-1234, outValue);
    }

    [Fact]
    public void Int16_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        short inValue = 1234;
        Assert.Equal((short)1234, E2EBindings.EchoMarkedInInt16(in inValue));

        short refValue = 41;
        E2EBindings.ReplaceMarkedRefInt16(ref refValue);
        Assert.Equal((short)42, refValue);

        E2EBindings.WriteMarkedOutInt16(out var outValue);
        Assert.Equal((short)-1234, outValue);
    }

    [Fact]
    public void UInt16_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((ushort)54321, E2EBindings.RoundtripUint16((ushort)54321));

        ushort inValue = 2345;
        Assert.Equal((ushort)2345, E2EBindings.EchoInUint16(in inValue));

        ushort refValue = 41;
        E2EBindings.IncrementRefUint16(ref refValue);
        Assert.Equal((ushort)42, refValue);

        E2EBindings.WriteOutUint16(out var outValue);
        Assert.Equal((ushort)45678, outValue);
    }

    [Fact]
    public void UInt16_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        ushort inValue = 2345;
        Assert.Equal((ushort)2345, E2EBindings.EchoMarkedInUint16(in inValue));

        ushort refValue = 41;
        E2EBindings.ReplaceMarkedRefUint16(ref refValue);
        Assert.Equal((ushort)42, refValue);

        E2EBindings.WriteMarkedOutUint16(out var outValue);
        Assert.Equal((ushort)45678, outValue);
    }

    [Fact]
    public void Int32_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(-1234567, E2EBindings.RoundtripInt32(-1234567));

        var inValue = 7654321;
        Assert.Equal(7654321, E2EBindings.EchoInInt32(in inValue));

        var refValue = 41;
        E2EBindings.IncrementRefInt32(ref refValue);
        Assert.Equal(42, refValue);

        E2EBindings.WriteOutInt32(out var outValue);
        Assert.Equal(-123456789, outValue);
    }

    [Fact]
    public void Int32_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        var inValue = 7654321;
        Assert.Equal(7654321, E2EBindings.EchoMarkedInInt32(in inValue));

        var refValue = 41;
        E2EBindings.ReplaceMarkedRefInt32(ref refValue);
        Assert.Equal(42, refValue);

        E2EBindings.WriteMarkedOutInt32(out var outValue);
        Assert.Equal(-123456789, outValue);
    }

    [Fact]
    public void UInt32_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(3456789012u, E2EBindings.RoundtripUint32(3456789012u));

        uint inValue = 1234567890u;
        Assert.Equal(1234567890u, E2EBindings.EchoInUint32(in inValue));

        uint refValue = 41;
        E2EBindings.IncrementRefUint32(ref refValue);
        Assert.Equal(42u, refValue);

        E2EBindings.WriteOutUint32(out var outValue);
        Assert.Equal(3456789012u, outValue);
    }

    [Fact]
    public void UInt32_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        uint inValue = 1234567890u;
        Assert.Equal(1234567890u, E2EBindings.EchoMarkedInUint32(in inValue));

        uint refValue = 41;
        E2EBindings.ReplaceMarkedRefUint32(ref refValue);
        Assert.Equal(42u, refValue);

        E2EBindings.WriteMarkedOutUint32(out var outValue);
        Assert.Equal(3456789012u, outValue);
    }

    [Fact]
    public void Int64_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(-1234567890123456789L, E2EBindings.RoundtripInt64(-1234567890123456789L));

        long inValue = 1234567890123456789L;
        Assert.Equal(1234567890123456789L, E2EBindings.EchoInInt64(in inValue));

        long refValue = 41;
        E2EBindings.IncrementRefInt64(ref refValue);
        Assert.Equal(42L, refValue);

        E2EBindings.WriteOutInt64(out var outValue);
        Assert.Equal(-1234567890123456789L, outValue);
    }

    [Fact]
    public void Int64_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        long inValue = 1234567890123456789L;
        Assert.Equal(1234567890123456789L, E2EBindings.EchoMarkedInInt64(in inValue));

        long refValue = 41;
        E2EBindings.ReplaceMarkedRefInt64(ref refValue);
        Assert.Equal(42L, refValue);

        E2EBindings.WriteMarkedOutInt64(out var outValue);
        Assert.Equal(-1234567890123456789L, outValue);
    }

    [Fact]
    public void UInt64_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(12345678901234567890UL, E2EBindings.RoundtripUint64(12345678901234567890UL));

        ulong inValue = 9876543210123456789UL;
        Assert.Equal(9876543210123456789UL, E2EBindings.EchoInUint64(in inValue));

        ulong refValue = 41;
        E2EBindings.IncrementRefUint64(ref refValue);
        Assert.Equal(42UL, refValue);

        E2EBindings.WriteOutUint64(out var outValue);
        Assert.Equal(12345678901234567890UL, outValue);
    }

    [Fact]
    public void UInt64_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        ulong inValue = 9876543210123456789UL;
        Assert.Equal(9876543210123456789UL, E2EBindings.EchoMarkedInUint64(in inValue));

        ulong refValue = 41;
        E2EBindings.ReplaceMarkedRefUint64(ref refValue);
        Assert.Equal(42UL, refValue);

        E2EBindings.WriteMarkedOutUint64(out var outValue);
        Assert.Equal(12345678901234567890UL, outValue);
    }

    [Fact]
    public void NativeInt_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((nint)24681012, E2EBindings.RoundtripNativeInt((nint)24681012));

        nint inValue = 7654321;
        Assert.Equal((nint)7654321, E2EBindings.EchoInNativeInt(in inValue));

        nint refValue = 41;
        E2EBindings.IncrementRefNativeInt(ref refValue);
        Assert.Equal((nint)42, refValue);

        E2EBindings.WriteOutNativeInt(out var outValue);
        Assert.Equal((nint)24681012, outValue);
    }

    [Fact]
    public void NativeInt_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        nint inValue = 7654321;
        Assert.Equal((nint)7654321, E2EBindings.EchoMarkedInNativeInt(in inValue));

        nint refValue = 41;
        E2EBindings.ReplaceMarkedRefNativeInt(ref refValue);
        Assert.Equal((nint)42, refValue);

        E2EBindings.WriteMarkedOutNativeInt(out var outValue);
        Assert.Equal((nint)24681012, outValue);
    }

    [Fact]
    public void NativeUint_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal((nuint)1357911, E2EBindings.RoundtripNativeUint((nuint)1357911));

        nuint inValue = 24681012;
        Assert.Equal((nuint)24681012, E2EBindings.EchoInNativeUint(in inValue));

        nuint refValue = 41;
        E2EBindings.IncrementRefNativeUint(ref refValue);
        Assert.Equal((nuint)42, refValue);

        E2EBindings.WriteOutNativeUint(out var outValue);
        Assert.Equal((nuint)1357911, outValue);
    }

    [Fact]
    public void NativeUint_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        nuint inValue = 24681012;
        Assert.Equal((nuint)24681012, E2EBindings.EchoMarkedInNativeUint(in inValue));

        nuint refValue = 41;
        E2EBindings.ReplaceMarkedRefNativeUint(ref refValue);
        Assert.Equal((nuint)42, refValue);

        E2EBindings.WriteMarkedOutNativeUint(out var outValue);
        Assert.Equal((nuint)1357911, outValue);
    }

    [Fact]
    public void Single_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(3.25f, E2EBindings.RoundtripSingle(3.25f));

        float inValue = 1.5f;
        Assert.Equal(1.5f, E2EBindings.EchoInSingle(in inValue));

        float refValue = 40.75f;
        E2EBindings.IncrementRefSingle(ref refValue);
        Assert.Equal(41.75f, refValue);

        E2EBindings.WriteOutSingle(out var outValue);
        Assert.Equal(6.5f, outValue);
    }

    [Fact]
    public void Single_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        float inValue = 1.5f;
        Assert.Equal(1.5f, E2EBindings.EchoMarkedInSingle(in inValue));

        float refValue = 40.75f;
        E2EBindings.ReplaceMarkedRefSingle(ref refValue);
        Assert.Equal(41.75f, refValue);

        E2EBindings.WriteMarkedOutSingle(out var outValue);
        Assert.Equal(6.5f, outValue);
    }

    [Fact]
    public void Double_value_in_ref_and_out_bindings_roundtrip()
    {
        Assert.Equal(7.75, E2EBindings.RoundtripDouble(7.75));

        double inValue = 4.5;
        Assert.Equal(4.5, E2EBindings.EchoInDouble(in inValue));

        double refValue = 39.5;
        E2EBindings.IncrementRefDouble(ref refValue);
        Assert.Equal(40.5, refValue);

        E2EBindings.WriteOutDouble(out var outValue);
        Assert.Equal(9.25, outValue);
    }

    [Fact]
    public void Double_marked_pointer_in_ref_and_out_bindings_roundtrip()
    {
        double inValue = 4.5;
        Assert.Equal(4.5, E2EBindings.EchoMarkedInDouble(in inValue));

        double refValue = 39.5;
        E2EBindings.ReplaceMarkedRefDouble(ref refValue);
        Assert.Equal(40.5, refValue);

        E2EBindings.WriteMarkedOutDouble(out var outValue);
        Assert.Equal(9.25, outValue);
    }

    [Fact]
    public void VoidPtr_value_in_ref_and_out_bindings_roundtrip()
    {
        var roundtripValue = new IntPtr(0x1234);
        Assert.Equal(roundtripValue, E2EBindings.RoundtripVoidPtr(roundtripValue));

        var inValue = new IntPtr(0x5678);
        Assert.Equal(inValue, E2EBindings.EchoInVoidPtr(inValue));

        var expectedToken = E2EBindings.OpaqueToken();
        var refValue = IntPtr.Zero;
        E2EBindings.ReplaceRefVoidPtr(ref refValue);
        Assert.Equal(expectedToken, refValue);

        var typedefRefValue = IntPtr.Zero;
        E2EBindings.ReplaceRefTypedefVoidPtr(ref typedefRefValue);
        Assert.Equal(expectedToken, typedefRefValue);

        E2EBindings.WriteOutVoidPtr(out var outValue);
        Assert.Equal(expectedToken, outValue);
    }

    [Fact]
    public void VoidPtr_marked_pointer_in_and_out_bindings_roundtrip()
    {
        var inValue = new IntPtr(0x5678);
        Assert.Equal(inValue, E2EBindings.EchoMarkedInVoidPtr(inValue));

        var expectedToken = E2EBindings.OpaqueToken();
        E2EBindings.WriteMarkedOutVoidPtr(out var outValue);
        Assert.Equal(expectedToken, outValue);
    }

    [Fact]
    public void SignedCharPtr_surfaces_are_IntPtr_tokens()
    {
        var roundtripValue = new IntPtr(0x1234);
        Assert.Equal(roundtripValue, E2EBindings.RoundtripSignedCharPtr(roundtripValue));

        var inValue = new IntPtr(0x5678);
        Assert.Equal(inValue, E2EBindings.EchoInConstSignedCharPtr(inValue));

        var expectedToken = E2EBindings.OpaqueToken();
        var refValue = IntPtr.Zero;
        E2EBindings.ReplaceRefSignedCharPtr(ref refValue);
        Assert.Equal(expectedToken, refValue);

        E2EBindings.WriteOutSignedCharPtr(out var outValue);
        Assert.Equal(expectedToken, outValue);
    }

    [Fact]
    public void UnsignedCharPtr_surfaces_are_IntPtr_tokens()
    {
        var roundtripValue = new IntPtr(0x1234);
        Assert.Equal(roundtripValue, E2EBindings.RoundtripUnsignedCharPtr(roundtripValue));

        var inValue = new IntPtr(0x5678);
        Assert.Equal(inValue, E2EBindings.EchoInConstUnsignedCharPtr(inValue));

        var expectedToken = E2EBindings.OpaqueToken();
        var refValue = IntPtr.Zero;
        E2EBindings.ReplaceRefUnsignedCharPtr(ref refValue);
        Assert.Equal(expectedToken, refValue);

        E2EBindings.WriteOutUnsignedCharPtr(out var outValue);
        Assert.Equal(expectedToken, outValue);
    }

    [Fact]
    public void Simple_handle_return_creates_non_null_wrapper()
    {
        var context = E2EBindings.CreateContext();

        Assert.NotNull(context);
        Assert.False(context.IsNull);
        Assert.NotEqual((nint)0, context.Handle);
    }
}
