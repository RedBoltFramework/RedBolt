namespace RedBolt.Tests;

using System.IO;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RmlUI;
using RedBolt.Scene;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RmlUIComponentTests
{
    private sealed class ComponentProbe : RmlUIComponent
    {
        public bool DataModelInitialized { get; private set; }

        protected override void OnDataModelInitialized()
        {
            DataModelInitialized = true;
            Assert.True(BindDataModelProperty("value",
                (ref Variant value) => value = new Variant("bound"),
                (in Variant value) => { }));
        }
    }

    private readonly GraphicsEngineFixture _fixture;

    public RmlUIComponentTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Component_manages_document_and_data_model_lifecycle()
    {
        const string ResourceName = "RmlUI/ComponentTest.rml";
        var resourceDirectory = Path.Combine(Path.GetTempPath(), $"redbolt-rmlui-{Guid.NewGuid():N}");
        var resourcePath = Path.Combine(resourceDirectory, ResourceName);
        Directory.CreateDirectory(Path.GetDirectoryName(resourcePath)!);
        File.WriteAllText(resourcePath,
            "<rml><head></head><body data-model=\"{{__data_model_id}}\"><span>{{value}}</span></body></rml>");

        RmlUI.RegisterRmlUILibrary(_fixture.Context);
        using var localization = new Localization();
        _fixture.Context.RegisterSubsystem(localization);
        using var ui = new RmlUI("component-test");
        _fixture.Context.RegisterSubsystem(ui);
        Assert.True(_fixture.Resources.AddResourceDir(resourceDirectory));

        try
        {
            using var node = new Node();
            using var component = new ComponentProbe();
            component.SetAutoSize(false);
            component.SetPosition(new Vector2f(4.0f, 5.0f));
            component.SetSize(new Vector2f(30.0f, 20.0f));
            component.SetResource(ResourceName);

            node.AddComponent(component);

            var document = component.GetDocument();
            Assert.NotNull(document);
            Assert.Same(component, RmlUIComponent.FromDocument(document));
            Assert.True(component.DataModelInitialized);
            Assert.Equal(ResourceName, component.GetResource().Name);
            Assert.Equal(new Vector2f(30.0f, 20.0f), component.GetSize());
            Assert.NotNull(_fixture.Context.GetReflection(typeof(RmlUIComponent).GetClassName()));

            component.SetEnabled(false);
            Assert.Null(component.GetDocument());
            Assert.Null(RmlUIComponent.FromDocument(document));
            node.RemoveComponent(component);
        }
        finally
        {
            _fixture.Resources.RemoveResourceDir(resourceDirectory);
            _fixture.Context.RemoveSubsystem<RmlUI>();
            _fixture.Context.RemoveSubsystem<Localization>();
            Directory.Delete(resourceDirectory, recursive: true);
        }
    }
}
