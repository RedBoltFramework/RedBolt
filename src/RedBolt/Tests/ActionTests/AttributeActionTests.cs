namespace RedBolt.Tests;

using RedBolt.Actions;

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
}
