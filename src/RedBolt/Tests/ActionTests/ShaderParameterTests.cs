namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Graphics;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShaderParameterTests
{
    [Fact]
    public void Shader_parameter_action_serializes_name()
    {
        using var action = new ShaderParameterAction();

        Assert.Equal(string.Empty, action.Name);
        action.SetDuration(2.0f);
        action.SetName("MatDiffColor");

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new ShaderParameterAction();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        Assert.Equal(2.0f, restored.Duration);
        Assert.Equal("MatDiffColor", restored.Name);
    }

    [Fact]
    public void Shader_parameter_to_serializes_destination()
    {
        using var destination = new Variant(4.0f);
        using var action = new ShaderParameterTo();
        action.SetDuration(2.0f);
        action.SetName("Value");
        action.SetTo(destination);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new ShaderParameterTo();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        Assert.Equal(2.0f, restored.Duration);
        Assert.Equal("Value", restored.Name);
        Assert.Equal(4.0f, restored.To.GetFloat());
    }

    [Fact]
    public void Shader_parameter_to_animates_from_current_value()
    {
        using var manager = new ActionManager(false);
        using var material = new Material();
        material.SetShaderParameter("Value", 2.0f);
        using var destination = new Variant(6.0f);
        using var action = new ShaderParameterTo();
        action.SetDuration(1.0f);
        action.SetName("Value");
        action.SetTo(destination);

        Assert.NotNull(manager.AddAction(action, material));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.Equal(4.0f, Assert.IsType<float>(material.GetShaderParameter("Value")));
    }

    [Fact]
    public void Shader_parameter_to_uses_destination_when_types_differ()
    {
        using var manager = new ActionManager(false);
        using var material = new Material();
        material.SetShaderParameter("Value", 2);
        using var destination = new Variant(6.0f);
        using var action = new ShaderParameterTo();
        action.SetDuration(1.0f);
        action.SetName("Value");
        action.SetTo(destination);

        Assert.NotNull(manager.AddAction(action, material));
        manager.Update(0.0f);

        Assert.Equal(6.0f, Assert.IsType<float>(material.GetShaderParameter("Value")));
    }
}
