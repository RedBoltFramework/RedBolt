using System;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Callback;

public sealed class CallbackTests
{
    [Fact]
    public void Action_callback_is_invoked_by_native_code()
    {
        var observed = 0;

        E2EBindings.InvokeVoidCallback(value => observed = value, 42);

        Assert.Equal(42, observed);
    }

    [Fact]
    public void Func_callback_returns_value_to_native_code()
    {
        var result = E2EBindings.InvokeIntCallback((lhs, rhs) => lhs * rhs, 6, 7);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Native_callback_wrapper_can_be_copied_with_shared_lifetime()
    {
        var result = E2EBindings.InvokeCopiedCallback(value => value + 1, 41);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Object_pointer_callback_receives_borrowed_wrapper()
    {
        var result = E2EBindings.InvokeObjectPointerCallback(box => box.SetValue(42), 1);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Object_reference_callback_receives_borrowed_wrapper()
    {
        var result = E2EBindings.InvokeObjectReferenceCallback(box => box.SetValue(42), 1);

        Assert.Equal(42, result);
    }

    [Fact]
    public unsafe void Primitive_pointer_callback_receives_raw_pointer()
    {
        var result = E2EBindings.InvokePrimitivePointerCallback(pointer => *(int*)pointer.ToPointer() = 42, 1);

        Assert.Equal(42, result);
    }

    [Fact]
    public unsafe void Primitive_reference_callback_receives_raw_pointer()
    {
        var result = E2EBindings.InvokePrimitiveReferenceCallback(pointer => *(int*)pointer.ToPointer() = 42, 1);

        Assert.Equal(42, result);
    }

    [Fact]
    public void String_reference_callback_receives_string_wrapper()
    {
        var result = E2EBindings.InvokeStringReferenceCallback(value => value.IsInvalid ? 0 : 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Delegate_attribute_projects_callback_parameter_to_named_delegate()
    {
        var observed = 0;
        E2EBindings.ValueCallback callback = value => observed = value;

        E2EBindings.InvokeNamedDelegateCallback(callback, 42);

        Assert.Equal(42, observed);
    }
}
