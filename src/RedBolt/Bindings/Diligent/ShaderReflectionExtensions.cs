namespace RedBolt.Bindings.Diligent;

public partial class ShaderCodeBufferDesc
{
    public ShaderCodeVariableDesc GetVariableAt(Uint32 index)
    {
        return GetPVariablesAt(index);
    }
}
