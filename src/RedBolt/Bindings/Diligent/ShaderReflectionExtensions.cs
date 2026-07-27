namespace RedBolt.Bindings.Diligent;

public partial class ShaderCodeBufferDesc
{
    public ShaderCodeVariableDesc GetVariableAt(Uint32 index)
    {
        var variable = DiligentNativeBindings.GetShaderCodeBufferVariableAt(this, index);
        global::System.GC.KeepAlive(this);
        return variable;
    }
}
