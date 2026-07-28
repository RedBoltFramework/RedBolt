namespace RedBolt.Tests;

using RedBolt.Actions;

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
}
