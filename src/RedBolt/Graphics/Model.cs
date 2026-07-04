namespace RedBolt;

public sealed class Model : Resource
{
    internal Model(string name, uint vertexCount)
        : base(name)
    {
        VertexCount = vertexCount;
    }

    public uint VertexCount { get; }
}
