using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class GenericTests
{
    [Fact]
    public void Generic_free_function_is_exposed_as_generic_method_definition()
    {
        var method = typeof(E2EBindings).GetMethod(nameof(E2EBindings.GenericAdd));

        Assert.NotNull(method);
        Assert.True(method!.IsGenericMethodDefinition);
    }

    [Fact]
    public void Generic_free_function_dispatches_to_supported_instantiations()
    {
        Assert.Equal(7, E2EBindings.GenericAdd(3, 4));
        Assert.Equal(7.5f, E2EBindings.GenericAdd(3.0f, 4.5f));
        Assert.Equal(9.0, E2EBindings.GenericAdd(4.5, 4.5));
    }

    [Fact]
    public void Unsupported_generic_free_function_instantiation_throws()
    {
        var exception = Assert.Throws<NotSupportedException>(() => E2EBindings.GenericAdd(true, false));

        Assert.Contains("generic argument combination", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Free_functions_can_return_concrete_generic_wrappers()
    {
        var intBox = E2EBindings.CreateIntBox(10);
        var floatBox = E2EBindings.CreateFloatBox(1.5f);

        Assert.Equal(10, intBox.GetValue());
        Assert.Equal(1.5f, floatBox.GetValue());
    }

    [Fact]
    public void Generic_record_constructor_and_instance_methods_dispatch_to_supported_instantiations()
    {
        var box = new BoxOf<int>(12);

        Assert.Equal(12, box.GetValue());

        box.SetValue(27);

        Assert.Equal(27, box.GetValue());
    }

    [Fact]
    public void Generic_record_static_methods_dispatch_to_supported_instantiations()
    {
        var box = BoxOf<float>.Create(2.5f);

        Assert.Equal(2.5f, box.GetValue());
    }

    [Fact]
    public void Generic_record_can_use_handle_typed_instantiation()
    {
        var payload = new GenericPayload(7);
        var replacement = new GenericPayload(9);
        var constructed = new HandleBoxOf<GenericPayload>(payload);
        var created = E2EBindings.CreateGenericPayloadBox(payload);

        Assert.Same(payload, constructed.GetValue());
        Assert.Same(payload, created.GetValue());

        constructed.SetValue(replacement);

        Assert.Same(replacement, constructed.GetValue());
        Assert.Equal(9, constructed.GetValue().GetValue());

        created.Dispose();
        constructed.Dispose();
        replacement.Dispose();
        payload.Dispose();
    }

    [Fact]
    public void Explicit_instantiation_metadata_can_expand_generic_record_surface()
    {
        var constructed = new BoxOf<double>(2.5);
        var created = BoxOf<double>.Create(4.5);

        Assert.Equal(2.5, constructed.GetValue());
        Assert.Equal(4.5, created.GetValue());
    }

    [Fact]
    public void Unsupported_generic_instantiation_throws_at_runtime()
    {
        var exception = Assert.Throws<NotSupportedException>(() => BoxOf<long>.Create(1L));

        Assert.Contains("generic argument combination", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generic_method_inside_generic_record_is_exposed_as_generic_method_definition()
    {
        var method = typeof(BoxOf<int>).GetMethod(nameof(BoxOf<int>.Project));

        Assert.NotNull(method);
        Assert.True(method!.IsGenericMethodDefinition);
    }

    [Fact]
    public void Generic_method_inside_generic_record_dispatches_to_concrete_instantiation()
    {
        var box = new BoxOf<int>(12);

        var result = box.Project(2);

        Assert.Equal(24, result);
    }

    [Fact]
    public void Unsupported_generic_method_instantiation_throws()
    {
        var box = new BoxOf<int>(12);

        var exception = Assert.Throws<NotSupportedException>(() => box.Project(2.5f));

        Assert.Contains("generic argument combination", exception.Message, StringComparison.Ordinal);
    }
}
