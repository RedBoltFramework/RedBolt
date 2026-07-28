namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

public class AttributeActionTests
{
    [Fact]
    public void Constructors_and_attribute_name_accessors_match_source()
    {
        using var emptyAction = new AttributeAction();
        Assert.Equal(string.Empty, emptyAction.AttributeName);

        using var action = new AttributeAction(AttributeActionConstants.PositionAttribute);
        Assert.Equal("Position", action.AttributeName);

        action.SetAttributeName(AttributeActionConstants.RotationAttribute);
        Assert.Equal("Rotation", action.AttributeName);
    }

    [Fact]
    public void Set_attribute_state_sets_value_once()
    {
        using var target = new AttributeTarget { Value = 2.0f };
        using var action = new FiniteTimeAction();
        using var attribute = new AttributeInfo(
            "Value", typeof(float), accessor: new ValueAccessor());
        using var value = new Variant(4.0f);
        using var state = new SetAttributeState(action, target, attribute, value);

        state.Update(0.0f);
        Assert.Equal(4.0f, target.Value);

        target.Value = 6.0f;
        state.Update(1.0f);
        Assert.Equal(6.0f, target.Value);
    }

    [Fact]
    public void Blink_state_restores_original_value_on_stop()
    {
        using var target = new AttributeTarget { Value = 2.0f };
        using var action = new FiniteTimeAction();
        using var attribute = new AttributeInfo(
            "Value", typeof(float), accessor: new ValueAccessor());
        using var from = new Variant(4.0f);
        using var to = new Variant(8.0f);
        using var state = new AttributeBlinkState(action, target, attribute, from, to, 2);

        state.Update(0.0f);
        Assert.Equal(8.0f, target.Value);

        state.Update(0.3f);
        Assert.Equal(4.0f, target.Value);

        state.Stop();
        Assert.Equal(2.0f, target.Value);
    }

    private sealed class AttributeTarget : Serializable
    {
        public float Value { get; set; }
    }

    private sealed class ValueAccessor : AttributeAccessor
    {
        public override void Get(Serializable ptr, ref Variant dest)
        {
            dest = new Variant(((AttributeTarget)ptr).Value);
        }

        public override void Set(Serializable ptr, Variant src)
        {
            ((AttributeTarget)ptr).Value = src.GetFloat();
        }
    }
}
