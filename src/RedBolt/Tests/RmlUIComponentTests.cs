namespace RedBolt.Tests;

using System.IO;
using System.Runtime.CompilerServices;

using ElementDocument = RedBolt.Bindings.RmlUi.ElementDocument;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RmlUI;
using RedBolt.Scene;

[Collection(nameof(GraphicsEngineCollection))]
public sealed partial class RmlUIComponentTests
{
    [Reflected("Tests")]
    private sealed partial class InspectorTarget : Serializable
    {
        [Serialized("Enabled")]
        public bool Enabled { get; set; } = true;

        [Serialized("Count")]
        public int Count { get; set; } = 42;
    }

    private sealed class ComponentProbe : RmlUIComponent
    {
        public ComponentProbe(StrongBox<Variant> value, VariantVector values, VariantMap properties)
        {
            _value = value;
            _values = values;
            _properties = properties;
        }

        public bool DataModelInitialized { get; private set; }

        protected override void OnDataModelInitialized()
        {
            DataModelInitialized = true;
            Assert.True(BindDataModelProperty("value",
                (ref Variant value) => value = new Variant("bound"),
                (in Variant value) => { }));
            Assert.True(BindDataModelVariant("variant", _value));
            Assert.True(BindDataModelVariantVector("values", _values));
            Assert.True(BindDataModelVariantMap("properties", _properties));
        }

        private readonly StrongBox<Variant> _value;
        private readonly VariantVector _values;
        private readonly VariantMap _properties;
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
            "<rml><head></head><body data-model=\"{{__data_model_id}}\">" +
            "<span>{{value}}</span>" +
            "<span>{{variant}}</span>" +
            "<div data-for=\"item : values\">{{item}}</div>" +
            "<span>{{properties.number}}</span>" +
            "<button id=\"set-variant\" data-event-click=\"variant = 'changed'\">Set</button>" +
            "<button id=\"set-number\" data-event-click=\"properties.number = 99\">Set</button>" +
            "</body></rml>");

        RmlUI.RegisterRmlUILibrary(_fixture.Context);
        using var localization = new Localization();
        _fixture.Context.RegisterSubsystem(localization);
        using var ui = new RmlUI("component-test");
        _fixture.Context.RegisterSubsystem(ui);
        Assert.True(_fixture.Resources.AddResourceDir(resourceDirectory));
        var value = new StrongBox<Variant>(new Variant("initial"));

        try
        {
            using var values = new VariantVector { new("first"), new("second") };
            using var properties = new VariantMap { [new StringHash("number")] = new Variant(42) };
            using var node = new Node();
            using var component = new ComponentProbe(value, values, properties);
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

            ui.GetRmlContext().Update();
            using var setVariantId = new Std.String();
            using var buttonId = new Std.String();
            setVariantId.Assign("set-variant");
            buttonId.Assign("set-number");
            var setVariant = document.GetElementById(setVariantId);
            Assert.NotNull(setVariant);
            setVariant.Click();
            Assert.Equal("changed", value.Value.GetString());
            var setNumber = document.GetElementById(buttonId);
            Assert.NotNull(setNumber);
            setNumber.Click();
            Assert.Equal(99, properties[new StringHash("number")].GetInt());

            component.SetEnabled(false);
            Assert.Null(component.GetDocument());
            Assert.Null(RmlUIComponent.FromDocument(document));
            node.RemoveComponent(component);
        }
        finally
        {
            value.Value.Dispose();
            _fixture.Resources.RemoveResourceDir(resourceDirectory);
            _fixture.Context.RemoveSubsystem<RmlUI>();
            _fixture.Context.RemoveSubsystem<Localization>();
            Directory.Delete(resourceDirectory, recursive: true);
        }
    }

    [Fact]
    public void Serializable_inspector_edits_supported_attributes()
    {
        const string ResourceName = "RmlUI/SerializableInspectorTest.rml";
        var resourceDirectory = Path.Combine(Path.GetTempPath(), $"redbolt-rmlui-{Guid.NewGuid():N}");
        var resourcePath = Path.Combine(resourceDirectory, ResourceName);
        Directory.CreateDirectory(Path.GetDirectoryName(resourcePath)!);
        File.WriteAllText(resourcePath,
            "<rml><head></head><body data-model=\"{{__data_model_id}}\">" +
            "<div data-for=\"attribute : attributes\">" +
            "<input id=\"set-enabled\" type=\"checkbox\" value=\"value\" " +
            "data-if=\"attribute.type == 1\" data-checked=\"attribute.value\"/>" +
            "</div>" +
            "</body></rml>");

        RmlUI.RegisterRmlUILibrary(_fixture.Context);
        using var localization = new Localization();
        _fixture.Context.RegisterSubsystem(localization);
        using var ui = new RmlUI("inspector-test");
        _fixture.Context.RegisterSubsystem(ui);
        Assert.True(_fixture.Resources.AddResourceDir(resourceDirectory));

        try
        {
            using var target = new InspectorTarget();
            using var node = new Node();
            using var inspector = new RmlSerializableInspector();
            inspector.Connect(target);
            inspector.SetResource(ResourceName);
            node.AddComponent(inspector);

            var document = Assert.IsType<ElementDocument>(inspector.GetDocument());
            ui.GetRmlContext().Update();

            using var enabledId = new Std.String();
            enabledId.Assign("set-enabled");
            document.GetElementById(enabledId)!.Click();
            Assert.False(target.Enabled);

            Assert.NotNull(_fixture.Context.GetReflection(typeof(RmlSerializableInspector).GetClassName()));
            node.RemoveComponent(inspector);
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
