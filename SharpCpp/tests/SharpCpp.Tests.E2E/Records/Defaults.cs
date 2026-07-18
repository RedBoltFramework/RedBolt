using System.Reflection;

using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Records;

public sealed class DefaultsTests
{
    [Fact]
    public void Managed_signatures_expose_literal_defaults()
    {
        var defaultScore = typeof(E2EBindings).GetMethod(nameof(E2EBindings.DefaultScore))!;
        var defaultScoreParameters = defaultScore.GetParameters();

        Assert.False(defaultScoreParameters[0].HasDefaultValue);
        Assert.Equal(typeof(int), defaultScoreParameters[1].ParameterType);
        Assert.Equal(2, defaultScoreParameters[1].DefaultValue);
        Assert.Equal(typeof(bool), defaultScoreParameters[2].ParameterType);
        Assert.Equal(true, defaultScoreParameters[2].DefaultValue);

        var constructor = typeof(DefaultBox).GetConstructor([typeof(int), typeof(int), typeof(DefaultMode)])!;
        var constructorParameters = constructor.GetParameters();

        Assert.False(constructorParameters[0].HasDefaultValue);
        Assert.Equal(2, constructorParameters[1].DefaultValue);
        Assert.Equal(DefaultMode.Active, constructorParameters[2].DefaultValue);

        var add = typeof(DefaultBox).GetMethod(nameof(DefaultBox.Add), BindingFlags.Public | BindingFlags.Static)!;
        var addParameters = add.GetParameters();

        Assert.False(addParameters[0].HasDefaultValue);
        Assert.Equal(23, addParameters[1].DefaultValue);

        var combine = typeof(DefaultBox).GetMethod(nameof(DefaultBox.Combine))!;
        var combineParameters = combine.GetParameters();

        Assert.False(combineParameters[0].HasDefaultValue);
        Assert.Equal(3, combineParameters[1].DefaultValue);
        Assert.Equal(DefaultMode.Active, combineParameters[2].DefaultValue);

        var create = typeof(DefaultBox).GetMethod(nameof(DefaultBox.Create), BindingFlags.Public | BindingFlags.Static)!;
        var createParameters = create.GetParameters();

        Assert.False(createParameters[0].HasDefaultValue);
        Assert.Equal(2, createParameters[1].DefaultValue);
        Assert.Equal(DefaultMode.Active, createParameters[2].DefaultValue);
    }

    [Fact]
    public void Free_function_uses_builtin_defaults()
    {
        Assert.Equal(221, E2EBindings.DefaultScore(2));
        Assert.Equal(241, E2EBindings.DefaultScore(2, 4));
        Assert.Equal(242, E2EBindings.DefaultScore(2, 4, false));
    }

    [Fact]
    public void Constructor_uses_builtin_and_enum_defaults()
    {
        var box = new DefaultBox(3);

        Assert.Equal(6, box.GetValue());
        Assert.Equal(DefaultMode.Active, box.GetMode());
    }

    [Fact]
    public void Static_method_uses_builtin_defaults()
    {
        Assert.Equal(42, DefaultBox.Add(19));
        Assert.Equal(24, DefaultBox.Add(1));
    }

    [Fact]
    public void Instance_method_uses_builtin_and_enum_defaults()
    {
        var box = new DefaultBox(3);

        Assert.Equal(146, box.Combine(10));
        Assert.Equal(227, box.Combine(1, 2, DefaultMode.Idle));
    }

    [Fact]
    public void Static_factory_uses_builtin_and_enum_defaults()
    {
        var box = DefaultBox.Create(4);

        Assert.Equal(8, box.GetValue());
        Assert.Equal(DefaultMode.Active, box.GetMode());
    }

    [Fact]
    public void Named_arguments_can_skip_to_later_defaulted_parameters()
    {
        Assert.Equal(121, E2EBindings.DefaultScore(seed: 1, enabled: true));
        Assert.Equal(24, DefaultBox.Add(lhs: 1, rhs: 23));
        Assert.Equal(237, new DefaultBox(3).Combine(amount: 1, mode: DefaultMode.Idle));
    }
}
