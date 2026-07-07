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

    public EngineTime Time => GetRequiredSubsystem<EngineTime>();

    public ResourceCache Resources => GetRequiredSubsystem<ResourceCache>();

    public Input Input => GetRequiredSubsystem<Input>();

    public Renderer Renderer => GetRequiredSubsystem<Renderer>();

    public Graphics Graphics => GetRequiredSubsystem<Graphics>();

    public RenderDevice RenderDevice => GetRequiredSubsystem<RenderDevice>();

    public RenderContext RenderContext => RenderDevice.GetRenderContext();

    public PipelineStateCache PipelineStateCache => GetRequiredSubsystem<PipelineStateCache>();

    public void Dispose()
    {
        Engine.Shutdown();
    }

    private T GetRequiredSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>()
            ?? throw new InvalidOperationException($"Required subsystem {typeof(T).Name} is not registered in Context.");
    }
}

[CollectionDefinition(nameof(HeadlessEngineCollection))]
public sealed class HeadlessEngineCollection : ICollectionFixture<HeadlessEngineFixture>
{
}
