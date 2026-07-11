using RedBolt;

namespace RedBolt.Tests;

/// <summary>
/// Fixture that boots a headless RedBolt engine once per test collection.
/// Tests that need an initialized Engine should use [Collection(nameof(HeadlessEngineCollection))].
/// </summary>
public sealed class HeadlessEngineFixture : IDisposable
{
    public HeadlessEngineFixture()
    {
        Engine = new Engine();
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests",
            Headless = true
        };
        Engine.Initialize(parameters);
    }

    public Engine Engine { get; }

    public Context Context => Engine.Context;

    public Time Time => Engine.GetSubsystem<Time>()!;

    public ResourceCache Resources => Engine.GetSubsystem<ResourceCache>()!;

    public Input Input => Engine.GetSubsystem<Input>()!;

    public Renderer Renderer => Engine.GetSubsystem<Renderer>()!;

    public Graphics Graphics => Engine.GetSubsystem<Graphics>()!;

    public RenderDevice RenderDevice => Engine.GetSubsystem<RenderDevice>()!;

    public RenderContext RenderContext => RenderDevice.GetRenderContext();

    public PipelineStateCache PipelineStateCache => Engine.GetSubsystem<PipelineStateCache>()!;

    public void Dispose()
    {
        Engine.Shutdown();
    }

}

[CollectionDefinition(nameof(HeadlessEngineCollection))]
public sealed class HeadlessEngineCollection : ICollectionFixture<HeadlessEngineFixture>
{
}
