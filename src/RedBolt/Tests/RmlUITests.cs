namespace RedBolt.Tests;

using RedBolt.Bindings.RmlUi;
using RedBolt.Container;
using RedBolt.Core;
using CoreContext = RedBolt.Core.Context;
using CoreVariant = RedBolt.Core.Variant;
using RedBolt.Input;
using RedBolt.Math;
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
        using var input = new Input();
        context.RegisterSubsystem(input);
        context.RegisterSubsystem(new Localization());
        RmlUI.RegisterRmlUILibrary(context);

        try
        {
            using var ui = new RmlUI("managed-rmlui");
            using var markup = new Std.String();
            using var source = new Std.String();
            using var modelName = new Std.String();
            using var valueName = new Std.String();
            using var applyName = new Std.String();
            using var setButtonId = new Std.String();
            using var applyButtonId = new Std.String();
            modelName.Assign("managed");
            valueName.Assign("value");
            applyName.Assign("apply");
            setButtonId.Assign("set");
            applyButtonId.Assign("apply");

            var value = "initial";
            var getCount = 0;
            var setCount = 0;
            var eventCount = 0;
            using var model = ui.GetRmlContext().CreateDataModel(modelName);
            using var modelHandle = model.GetModelHandle();
            Assert.True(model.BindFunc(
                valueName,
                output =>
                {
                    using var nativeValue = new Std.String();
                    nativeValue.Assign(value);
                    output.Assign(nativeValue);
                    ++getCount;
                },
                input =>
                {
                    using var nativeValue = input.Get<Std.String>();
                    value = nativeValue.ToString();
                    ++setCount;
                }));
            Assert.True(model.BindEventCallback(
                applyName,
                (handle, _, arguments) =>
                {
                    using var nativeValue = arguments[0].Get<Std.String>();
                    value = nativeValue.ToString();
                    handle.DirtyVariable(valueName);
                    ++eventCount;
                }));

            markup.Assign(
                "<rml><head></head><body><div data-model=\"managed\">" +
                "<span>{{value}}</span>" +
                "<button id=\"set\" data-event-click=\"value = 'changed'\">Set</button>" +
                "<button id=\"apply\" data-event-click=\"apply('applied')\" onclick=\"event:test\">Apply</button>" +
                "<div style=\"position: absolute; left: 0px; top: 0px; width: 100px; height: 100px;\" ondropfile=\"event:dropped\"></div>" +
                "</div></body></rml>");
            source.Assign("memory://RedBolt.Tests/RmlUI.rml");

            using var contextName = ui.GetRmlContext().GetName();
            Assert.Equal("managed-rmlui", contextName.ToString());
            Assert.Equal(new Vector2i(512, 512), ui.GetRmlContext().GetDimensions());
            Assert.IsType<RmlHarfBuzzFontEngine>(RmlUiNativeBindings.GetFontEngineInterface());
            Assert.NotNull(context.GetReflection(typeof(RmlUI).GetClassName()));

            using var nativeVariant = new RedBolt.Bindings.RmlUi.Variant();
            using var coreVariant = new CoreVariant("round-trip");
            Assert.True(RmlUI.ToRmlUi(coreVariant, nativeVariant));
            Assert.Equal(RedBolt.Bindings.RmlUi.VariantType.String, nativeVariant.GetVariantType());
            Assert.True(RmlUI.FromRmlUi(nativeVariant, out var convertedVariant));
            using (convertedVariant)
            {
                Assert.Equal("round-trip", convertedVariant.GetString());
            }
            using var vectorVariant = new CoreVariant(new Vector3f(1.0f, 2.0f, 3.0f));
            Assert.True(RmlUI.ToRmlUi(vectorVariant, nativeVariant));
            Assert.Equal(RedBolt.Bindings.RmlUi.VariantType.Vector3, nativeVariant.GetVariantType());
            Assert.True(RmlUI.FromRmlUi(nativeVariant, out convertedVariant));
            using (convertedVariant)
            {
                Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), convertedVariant.GetVector3());
            }
            using var colorVariant = new CoreVariant(new Color(0.25f, 0.5f, 0.75f, 1.0f));
            Assert.True(RmlUI.ToRmlUi(colorVariant, nativeVariant));
            Assert.Equal(RedBolt.Bindings.RmlUi.VariantType.Colourf, nativeVariant.GetVariantType());
            Assert.True(RmlUI.FromRmlUi(nativeVariant, out convertedVariant));
            using (convertedVariant)
            {
                Assert.Equal(new Color(0.25f, 0.5f, 0.75f, 1.0f), convertedVariant.GetColor());
            }

            ElementDocument? unloadedDocument = null;
            var dropEventCount = 0;
            ui.OnDocumentClosedEvent += document => unloadedDocument = document;
            ui.SubscribeToEvent<RmlCustomEvent>((ref RmlCustomEvent eventData) =>
            {
                if (eventData.EventType == new StringHash("dropped"))
                    ++dropEventCount;
            });
            var document = ui.GetRmlContext().LoadDocumentFromMemory(markup, source);

            Assert.NotNull(document);
            document.Show();
            ui.GetRmlContext().Update();
            Assert.True(getCount > 0);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var setButton = document.GetElementById(setButtonId);
            Assert.NotNull(setButton);
            setButton.Click();
            Assert.Equal("changed", value);
            Assert.Equal(1, setCount);

            var applyButton = document.GetElementById(applyButtonId);
            Assert.NotNull(applyButton);
            applyButton.Click();
            Assert.Equal("applied", value);
            Assert.Equal(1, eventCount);
            Assert.True(modelHandle.IsVariableDirty(valueName));

            input.SetMouseVisible(true);
            ui.GetRmlContext().ProcessMouseMove(10, 10, 0);
            var dropFile = new InputEvents.DropFileEvent { FileName = "/tmp/dropped.txt" };
            context.SendEvent(ref dropFile);
            Assert.Equal(1, dropEventCount);

            document.Close();
            Assert.Same(document, unloadedDocument);
            Assert.True(ui.GetRmlContext().RemoveDataModel(modelName));
        }
        finally
        {
            CoreContext.SetInstance(previousContext);
        }
    }
}
