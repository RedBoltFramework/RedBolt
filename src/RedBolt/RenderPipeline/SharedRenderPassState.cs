namespace RedBolt;

/// <summary>
/// State of render pipeline that can be accessed by render path and render passes.
/// </summary>
// C++: SharedRenderPassState is a struct inheriting NonCopyable, not a class.
// C++ has constructor and destructor (empty).
// C++ renderBufferManager_ is SharedPtr<RenderBufferManager>.
// C++ renderBuffers_ is ea::fixed_hash_map<StringHash, SharedPtr<RenderBuffer>, MaxRenderBuffers>.
public sealed class SharedRenderPassState
{
    public const uint MaxRenderBuffers = 128;

    public static readonly StringHash AlbedoBufferId = "GeometryBuffer.Albedo";
    public static readonly StringHash SpecularBufferId = "GeometryBuffer.Specular";
    public static readonly StringHash NormalBufferId = "GeometryBuffer.Normal";

    public IRenderPipelineInterface? RenderPipelineInterface { get; set; }

    public Camera? RenderCamera { get; set; }

    public RenderBufferManager? RenderBufferManager { get; set; }

    public Graphics? Graphics { get; set; }

    public IDictionary<StringHash, RenderBuffer> RenderBuffers { get; } = new Dictionary<StringHash, RenderBuffer>((int)MaxRenderBuffers);
}
