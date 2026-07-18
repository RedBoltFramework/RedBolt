using System;
using System.Text;

using SharpCpp.Support;
using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.String;

public sealed class StringTests
{
    [Fact]
    public void StringLength_returns_correct_length()
    {
        ReadOnlySpan<byte> value = "hello"u8;
        Assert.Equal(5, E2EBindings.StringLength(value));
    }

    [Fact]
    public void StringLength_returns_correct_length_for_short_string()
    {
        Assert.Equal(1, E2EBindings.StringLength("a"u8));
    }

    [Fact]
    public void StringEquals_returns_true_for_equal_strings()
    {
        Assert.True(E2EBindings.StringEquals("hello"u8, "hello"u8));
    }

    [Fact]
    public void StringEquals_returns_false_for_different_strings()
    {
        Assert.False(E2EBindings.StringEquals("hello"u8, "world"u8));
    }

    [Fact]
    public void StringEquals_returns_false_for_different_length_strings()
    {
        Assert.False(E2EBindings.StringEquals("hi"u8, "hello"u8));
    }

    [Fact]
    public void GetName_returns_SharpCpp_string()
    {
        E2EBindings.GetName(out var value);
        Assert.Equal("SharpCpp", value);
    }

    [Fact]
    public void ReplaceName_replaces_string_value()
    {
        var value = "original";
        E2EBindings.ReplaceName(ref value);
        Assert.Equal("replaced", value);
    }

    [Fact]
    public void GetMessage_returns_SharpCpp_string()
    {
        Assert.True(E2EBindings.GetMessage().SequenceEqual("Hello from SharpCpp"u8));
    }

    [Fact]
    public void StringViewLength_returns_correct_length()
    {
        Assert.Equal(5, E2EBindings.StringViewLength("hello"u8));
    }

    [Fact]
    public void StringViewEquals_compares_span_contents()
    {
        Assert.True(E2EBindings.StringViewEquals("hello"u8, "hello"u8));
        Assert.False(E2EBindings.StringViewEquals("hello"u8, "world"u8));
    }

    [Fact]
    public void StringViewLength_preserves_embedded_nulls()
    {
        Assert.Equal(4, E2EBindings.StringViewEmbeddedNullLength("hi\0x"u8));
    }

    [Fact]
    public void GetStringViewMessage_returns_span()
    {
        Assert.True(E2EBindings.GetStringViewMessage().SequenceEqual("Hello from string_view"u8));
    }

    [Fact]
    public void GetStringViewWithEmbeddedNull_returns_full_span()
    {
        Assert.True(E2EBindings.GetStringViewWithEmbeddedNull().SequenceEqual("a\0b"u8));
    }

    [Fact]
    public void EaStringViewLength_returns_correct_length()
    {
        Assert.Equal(5, E2EBindings.EaStringViewLength("hello"u8));
    }

    [Fact]
    public void EaStringViewLength_preserves_embedded_nulls()
    {
        Assert.Equal(4, E2EBindings.EaStringViewEmbeddedNullLength("hi\0x"u8));
    }

    [Fact]
    public void GetEaStringViewMessage_returns_span()
    {
        Assert.True(E2EBindings.GetEaStringViewMessage().SequenceEqual("Hello from ea::string_view"u8));
    }

    [Fact]
    public void String_type_exists_and_inherits_StringBase()
    {
        Assert.True(typeof(Std.String).IsSubclassOf(typeof(StringBase)));
    }

    [Fact]
    public void EaString_type_exists_and_inherits_StringBase()
    {
        Assert.True(typeof(E2EBindings).Assembly.GetType("Ea.String")!.IsSubclassOf(typeof(StringBase)));
    }

    [Fact]
    public void EaString_round_trips_and_remains_distinct_from_std_string()
    {
        var eaType = typeof(E2EBindings).Assembly.GetType("Ea.String", throwOnError: true)!;
        var value = Activator.CreateInstance(eaType)!;
        eaType.GetMethod("Assign", [typeof(string)])!.Invoke(value, ["hello"]);

        var method = typeof(E2EBindings).GetMethod("MeasureEaString")!;
        Assert.Equal(5, method.Invoke(null, [value]));
        Assert.Equal(eaType, method.GetParameters()[0].ParameterType);
        Assert.NotEqual(typeof(Std.String), eaType);
    }
}
