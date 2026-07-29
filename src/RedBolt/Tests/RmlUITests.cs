namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.Core;
using CoreContext = RedBolt.Core.Context;
using RedBolt.Input;
using RedBolt.Resource;
using RedBolt.RmlUI;

using RedBoltNative;

[Collection(nameof(RmlUiBindingsCollection))]
public sealed class RmlUITests
{
    [Fact]
    public void Subsystem_manages_context_and_document_lifecycle()
    {
        var previousContext = CoreContext.Instance;
        using var context = new CoreContext();
        CoreContext.SetInstance(context);
        context.RegisterSubsystem(new Input());
        context.RegisterSubsystem(new Localization());
        RmlUI.RegisterRmlUILibrary(context);

        try
        {
            using var ui = new RmlUI("managed-rmlui");
            using var markup = new Std.String();
            using var source = new Std.String();
            markup.Assign("<rml><head></head><body><button onclick=\"event:test\">Test</button></body></rml>");
            source.Assign("memory://RedBolt.Tests/RmlUI.rml");

            using var contextName = ui.GetRmlContext().GetName();
            Assert.Equal("managed-rmlui", contextName.ToString());
            Assert.Equal(new RmlUiLegacyVector2i { X = 512, Y = 512 }, ui.GetRmlContext().GetDimensions());
            Assert.IsType<RmlHarfBuzzFontEngine>(RmlUiNativeBindings.GetFontEngineInterface());
            Assert.NotNull(context.GetReflection(typeof(RmlUI).GetClassName()));

            ElementDocument? unloadedDocument = null;
            ui.OnDocumentClosedEvent += document => unloadedDocument = document;
            var document = ui.GetRmlContext().LoadDocumentFromMemory(markup, source);

            Assert.NotNull(document);
            document.Close();
            Assert.Same(document, unloadedDocument);
        }
        finally
        {
            CoreContext.SetInstance(previousContext);
        }
    }
}
