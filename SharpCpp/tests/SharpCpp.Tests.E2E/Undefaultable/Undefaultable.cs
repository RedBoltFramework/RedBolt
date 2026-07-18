using System.Reflection;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Undefaultable;

public sealed class UndefaultableTests
{
    [Fact]
    public void Free_function_with_undefaultable_generates_two_overloads()
    {
        var methods = typeof(E2EBindings).GetMethods()
            .Where(m => string.Equals(m.Name, nameof(E2EBindings.DescribeLevel), StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, methods.Length);

        // Find overload by parameter count
        var overloadA = methods.First(m => m.GetParameters().Length == 2);
        var overloadB = methods.First(m => m.GetParameters().Length == 3);

        // Overload A: (int value, int threshold = 5)
        var overloadAParams = overloadA.GetParameters();
        Assert.Equal(typeof(int), overloadAParams[0].ParameterType);
        Assert.False(overloadAParams[0].HasDefaultValue);
        Assert.Equal(typeof(int), overloadAParams[1].ParameterType);
        Assert.Equal(5, overloadAParams[1].DefaultValue);

        // Overload B: (int value, int threshold, ReadOnlySpan<byte> label)
        var overloadBParams = overloadB.GetParameters();
        Assert.Equal(typeof(int), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
        Assert.Equal(typeof(int), overloadBParams[1].ParameterType);
        Assert.False(overloadBParams[1].HasDefaultValue);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[2].ParameterType);
        Assert.False(overloadBParams[2].HasDefaultValue);
    }

    [Fact]
    public void Free_function_all_undefaultable_generates_overload_with_no_params()
    {
        // Overload A: () — no params
        var overloadA = typeof(E2EBindings).GetMethod(
            nameof(E2EBindings.CheckUndefaultableOnly),
            Type.EmptyTypes)!;

        Assert.NotNull(overloadA);

        // Overload B: (ReadOnlySpan<byte>) — required
        var overloadB = typeof(E2EBindings).GetMethod(
            nameof(E2EBindings.CheckUndefaultableOnly),
            [typeof(ReadOnlySpan<byte>)])!;

        var overloadBParams = overloadB.GetParameters();
        Assert.Single(overloadBParams);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
    }

    [Fact]
    public void Void_free_function_with_undefaultable_generates_overloads()
    {
        // Overload A: () — no params
        var overloadA = typeof(E2EBindings).GetMethod(
            nameof(E2EBindings.ApplyConfig),
            Type.EmptyTypes)!;

        Assert.NotNull(overloadA);

        // Overload B: (ReadOnlySpan<byte>, int) — undefaultable required, expressible keeps default
        var overloadB = typeof(E2EBindings).GetMethod(
            nameof(E2EBindings.ApplyConfig),
            [typeof(ReadOnlySpan<byte>), typeof(int)])!;

        var overloadBParams = overloadB.GetParameters();
        Assert.Equal(2, overloadBParams.Length);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
        Assert.Equal(typeof(int), overloadBParams[1].ParameterType);
        Assert.Equal(3, overloadBParams[1].DefaultValue);
    }

    [Fact]
    public void Mixed_expressible_and_undefaultable_generates_correct_overloads()
    {
        var methods = typeof(E2EBindings).GetMethods()
            .Where(m => string.Equals(m.Name, nameof(E2EBindings.MixedDefaults), StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, methods.Length);

        // Overload A: (int a = 1) — 1 param
        var overloadA = methods.First(m => m.GetParameters().Length == 1);
        var overloadAParams = overloadA.GetParameters();
        Assert.Equal(typeof(int), overloadAParams[0].ParameterType);
        Assert.Equal(1, overloadAParams[0].DefaultValue);

        // Overload B: (int a, ReadOnlySpan<byte> b, int c = 3) — 3 params
        var overloadB = methods.First(m => m.GetParameters().Length == 3);
        var overloadBParams = overloadB.GetParameters();
        Assert.Equal(typeof(int), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[1].ParameterType);
        Assert.False(overloadBParams[1].HasDefaultValue);
        Assert.Equal(typeof(int), overloadBParams[2].ParameterType);
        Assert.Equal(3, overloadBParams[2].DefaultValue);
    }

    [Fact]
    public void Constructor_with_undefaultable_generates_overloads()
    {
        // Overload A: (int id) — before first undefaultable
        var ctorA = typeof(UndefaultableHandler).GetConstructor([typeof(int)])!;
        Assert.NotNull(ctorA);

        var ctorAParams = ctorA.GetParameters();
        Assert.Single(ctorAParams);
        Assert.Equal(typeof(int), ctorAParams[0].ParameterType);
        Assert.False(ctorAParams[0].HasDefaultValue);

        // Overload B: (int id, ReadOnlySpan<byte> name) — undefaultable required
        var ctorB = typeof(UndefaultableHandler).GetConstructor(
            [typeof(int), typeof(ReadOnlySpan<byte>)])!;

        var ctorBParams = ctorB.GetParameters();
        Assert.Equal(2, ctorBParams.Length);
        Assert.Equal(typeof(ReadOnlySpan<byte>), ctorBParams[1].ParameterType);
        Assert.False(ctorBParams[1].HasDefaultValue);
    }

    [Fact]
    public void Instance_method_with_undefaultable_generates_overloads()
    {
        // Overload A: (int amount) — before first undefaultable
        var overloadA = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.Process),
            [typeof(int)])!;

        var overloadAParams = overloadA.GetParameters();
        Assert.Single(overloadAParams);
        Assert.Equal(typeof(int), overloadAParams[0].ParameterType);
        Assert.False(overloadAParams[0].HasDefaultValue);

        // Overload B: (int amount, ReadOnlySpan<byte> tag, int scale = 2)
        var overloadB = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.Process),
            [typeof(int), typeof(ReadOnlySpan<byte>), typeof(int)])!;

        var overloadBParams = overloadB.GetParameters();
        Assert.Equal(3, overloadBParams.Length);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[1].ParameterType);
        Assert.False(overloadBParams[1].HasDefaultValue);
        Assert.Equal(typeof(int), overloadBParams[2].ParameterType);
        Assert.Equal(2, overloadBParams[2].DefaultValue);
    }

    [Fact]
    public void Static_method_with_undefaultable_generates_overloads()
    {
        // Overload A: () — no params (first undefaultable is at index 0)
        var overloadA = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.Lookup),
            Type.EmptyTypes)!;

        Assert.NotNull(overloadA);

        // Overload B: (ReadOnlySpan<byte> key, int fallback = -1) — undefaultable required
        var overloadB = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.Lookup),
            [typeof(ReadOnlySpan<byte>), typeof(int)])!;

        var overloadBParams = overloadB.GetParameters();
        Assert.Equal(2, overloadBParams.Length);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
        Assert.Equal(typeof(int), overloadBParams[1].ParameterType);
        Assert.Equal(-1, overloadBParams[1].DefaultValue);
    }

    [Fact]
    public void Void_static_method_with_undefaultable_generates_overloads()
    {
        // Overload A: () — no params
        var overloadA = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.ClearCache),
            Type.EmptyTypes)!;

        Assert.NotNull(overloadA);

        // Overload B: (ReadOnlySpan<byte> reason) — required
        var overloadB = typeof(UndefaultableHandler).GetMethod(
            nameof(UndefaultableHandler.ClearCache),
            [typeof(ReadOnlySpan<byte>)])!;

        var overloadBParams = overloadB.GetParameters();
        Assert.Single(overloadBParams);
        Assert.Equal(typeof(ReadOnlySpan<byte>), overloadBParams[0].ParameterType);
        Assert.False(overloadBParams[0].HasDefaultValue);
    }
}
