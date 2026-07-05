namespace RedBolt;

public sealed class SharedRenderPassState
{
    public static readonly StringHash AlbedoBufferId = "GeometryBuffer.Albedo";
    public static readonly StringHash SpecularBufferId = "GeometryBuffer.Specular";
    public static readonly StringHash NormalBufferId = "GeometryBuffer.Normal";

    public IRenderPipelineInterface? RenderPipelineInterface { get; set; }

    public Camera? RenderCamera { get; set; }

    public object? RenderBufferManager { get; set; }

    public IDictionary<StringHash, RenderBuffer> RenderBuffers { get; } = new Dictionary<StringHash, RenderBuffer>();
}
