using SharpCpp.Support;

using System.Globalization;

using Ea;
using Urho3D.UI;

using Xunit;

namespace SharpCpp.Rbfx.Tests.E2E;

public sealed class StringBindingTests
{
    [Fact]
    public void Rbfx_ea_string_wrapper_is_generated_with_ui_name_surface()
    {
        var eaStringType = typeof(global::Ea.String);
        var uiElementType = typeof(UIElement);

        Assert.True(eaStringType.IsSubclassOf(typeof(StringBase)));

        var nameProperty = uiElementType.GetProperty("Name")!;
        Assert.Equal(eaStringType, nameProperty.PropertyType);
        Assert.True(nameProperty.CanRead);
        Assert.True(nameProperty.CanWrite);
    }

    [Fact]
    public void Ea_string_format_writes_interpolated_utf8_text()
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("lt-LT");

            using var value = new global::Ea.String(string.Empty);

            value.Format($"Elapsed: {1.25f:F1} s | Events: {7,3}");

            Assert.Equal("Elapsed: 1.2 s | Events:   7", value.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Ea_string_format_accepts_explicit_culture()
    {
        using var value = new global::Ea.String(string.Empty);

        value.Format(CultureInfo.GetCultureInfo("lt-LT"), $"Elapsed: {1.25f:F1} s");

        Assert.Equal("Elapsed: 1,2 s", value.ToString());
    }

    [Fact]
    public void Ea_string_converts_to_utf8_span()
    {
        using var value = new global::Ea.String("hello");

        ReadOnlySpan<byte> span = value;

        Assert.True(span.SequenceEqual("hello"u8));
    }
}
