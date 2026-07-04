using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

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
            ApplicationName = "RedBolt.BindingSmoke.Tests",
            Headless = true
        };
        Engine.Initialize(parameters);
    }

    public Engine Engine { get; }

    public ResourceCache Resources => Engine.Resources;

    public Input Input => Engine.Input;

    public Renderer Renderer => Engine.Renderer;

    public Graphics Graphics => Engine.Graphics;

    public void Dispose()
    {
        Engine.Shutdown();
    }
}

[CollectionDefinition(nameof(HeadlessEngineCollection))]
public sealed class HeadlessEngineCollection : ICollectionFixture<HeadlessEngineFixture>
{
}
