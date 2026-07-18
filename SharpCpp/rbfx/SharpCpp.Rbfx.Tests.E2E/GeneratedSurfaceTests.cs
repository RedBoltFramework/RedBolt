using Xunit;

namespace SharpCpp.Rbfx.Tests.E2E;

public sealed class GeneratedSurfaceTests
{
    [Fact]
    public void EventHandler_keeps_supported_surface_and_instantiates_subscribe_templates()
    {
        var managedContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Core", "SharpCpp.Rbfx.Object.g.cs"));
        var nativeContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Core", "SharpCpp.Rbfx.Object.g.cpp"));

        Assert.Contains("template <class T> EventHandler(Object *receiver, T handler) // excluded", managedContent, StringComparison.Ordinal);
        Assert.Contains("public global::Urho3D.Math.StringHash EventType", managedContent, StringComparison.Ordinal);
        Assert.Contains("public void SubscribeToEvent(global::Urho3D.Math.StringHash eventType, global::System.Action<global::Urho3D.Core.Object, global::Urho3D.Math.StringHash, VariantMap> handler)", managedContent, StringComparison.Ordinal);
        Assert.Contains("public void SubscribeToEvent(global::Urho3D.Core.Object sender, global::Urho3D.Math.StringHash eventType, global::System.Action<global::Urho3D.Core.Object, global::Urho3D.Math.StringHash, VariantMap> handler)", managedContent, StringComparison.Ordinal);
        Assert.Contains("&Urho3D::Object::SubscribeToEvent<eastl::function<void (Urho3D::Object*, Urho3D::StringHash, eastl::hash_map<Urho3D::StringHash, Urho3D::Variant>&)>>", nativeContent, StringComparison.Ordinal);
    }

    [Fact]
    public void EventHandler_callback_can_read_variant_map_event_data()
    {
        using var context = new global::Urho3D.Core.Context();
        using var receiver = new global::Urho3D.Core.WorkQueue(context);
        global::Urho3D.Math.StringHash eventType = "ManagedTestEvent";
        global::Urho3D.Math.StringHash key = "TimeStep";
        var wasCalled = false;

        using var handler = new global::Urho3D.Core.EventHandler(receiver, (_, _, eventData) =>
        {
            wasCalled = true;
            Assert.True(eventData.TryGetValue(key, out var value));
            Assert.Equal(0.25f, value.Float);
        });

        using var eventData = new global::Ea.UnorderedMap<global::Urho3D.Math.StringHash, global::Urho3D.Core.Variant>
        {
            [key] = 0.25f
        };
        Assert.True(eventData.TryGetValue(key, out var directValue));
        Assert.Equal(0.25f, directValue.Float);

        handler.SetSenderAndEventType(receiver, eventType);
        handler.Invoke(eventData);

        Assert.True(wasCalled);
    }

    [Fact]
    public void Generated_SubscribeToEvent_transfers_event_handler_to_native_owner()
    {
        using var context = new global::Urho3D.Core.Context();
        using var receiver = new global::Urho3D.Core.WorkQueue(context);
        global::Urho3D.Math.StringHash eventType = "ManagedOwnedEventHandler";
        global::Urho3D.Math.StringHash key = "TimeStep";
        var wasCalled = false;

        receiver.SubscribeToEvent(eventType, (_, _, eventData) =>
        {
            wasCalled = true;
            Assert.True(eventData.TryGetValue(key, out var value));
            Assert.Equal(0.5f, value.Float);
        });

        using var eventData = new global::Ea.UnorderedMap<global::Urho3D.Math.StringHash, global::Urho3D.Core.Variant>
        {
            [key] = 0.5f
        };

        receiver.SendEvent(eventType, eventData);

        Assert.True(wasCalled);
    }

    [Fact]
    public void Spline_keeps_current_supported_surface()
    {
        var content = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Core", "SharpCpp.Rbfx.Spline.g.cs"));

        Assert.Contains("public global::Urho3D.Core.Variant GetPoint(float f)", content, StringComparison.Ordinal);
        Assert.Contains("public global::Urho3D.Core.InterpolationMode InterpolationMode", content, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.SetInterpolationMode(this, value);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Context_generates_default_constructor_surface()
    {
        var managedContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Core", "SharpCpp.Rbfx.Context.g.cs"));
        var nativeContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Core", "SharpCpp.Rbfx.Context.g.cpp"));

        Assert.Contains("public Context()", managedContent, StringComparison.Ordinal);
        Assert.Contains("__PInvoke.Context()", managedContent, StringComparison.Ordinal);
        Assert.Contains("MarshallerInvoker::construct<Urho3D::Context>()", nativeContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Macro_headers_generate_engine_constants_and_events()
    {
        var defsContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Engine", "SharpCpp.Rbfx.EngineDefs.g.cs"));
        var eventsContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "Engine", "SharpCpp.Rbfx.EngineEvents.g.cs"));

        Assert.Contains("public static partial class EngineDefs", defsContent, StringComparison.Ordinal);
        Assert.Contains("public const string FullScreen = \"FullScreen\";", defsContent, StringComparison.Ordinal);
        Assert.Contains("public const string WindowTitle = \"WindowTitle\";", defsContent, StringComparison.Ordinal);
        Assert.Equal("FullScreen", global::Urho3D.Engine.EngineDefs.FullScreen);
        Assert.Equal("WindowTitle", global::Urho3D.Engine.EngineDefs.WindowTitle);

        Assert.Contains("public static partial class EngineEvents", eventsContent, StringComparison.Ordinal);
        Assert.Contains("public static readonly Detail.ConsoleCommandEvent ConsoleCommand = new Detail.ConsoleCommandEvent();", eventsContent, StringComparison.Ordinal);
        Assert.Contains("public sealed class ConsoleCommandEvent : global::Urho3D.Detail.EventDefinition", eventsContent, StringComparison.Ordinal);
        Assert.Contains("public readonly global::Urho3D.Math.StringHash Command = \"Command\";", eventsContent, StringComparison.Ordinal);
        Assert.IsType<global::Urho3D.Engine.EngineEvents.Detail.ConsoleCommandEvent>(global::Urho3D.Engine.EngineEvents.ConsoleCommand);
    }

    [Fact]
    public void Enabled_RmlUI_bindings_generate_rml_namespace_surface()
    {
        if (!GeneratorLogContains("RmlUI bindings: enabled"))
        {
            return;
        }

        var documentContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "ThirdParty", "RmlUi", "SharpCpp.Rbfx.Core.ElementDocument.g.cs"));
        var contextContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "ThirdParty", "RmlUi", "SharpCpp.Rbfx.Core.Context.g.cs"));
        var aliasContent = File.ReadAllText(Path.Combine(GetGeneratedDir(), "SharpCpp.Rbfx.Bindings.Aliases.g.cs"));

        Assert.Contains("namespace Rml", documentContent, StringComparison.Ordinal);
        Assert.Contains("public partial class ElementDocument", documentContent, StringComparison.Ordinal);
        Assert.Contains("public void Show(global::Rml.ModalFlag modal_flag = global::Rml.ModalFlag.None, global::Rml.FocusFlag focus_flag = global::Rml.FocusFlag.Auto)", documentContent, StringComparison.Ordinal);
        Assert.Contains("public enum ModalFlag", documentContent, StringComparison.Ordinal);
        Assert.Contains("public enum FocusFlag", documentContent, StringComparison.Ordinal);
        Assert.Contains("public global::Rml.DataModelConstructor CreateDataModel(global::Ea.String name, global::Rml.DataTypeRegister? data_type_register = default)", contextContent, StringComparison.Ordinal);
        Assert.Contains("private static void Invoke(nint state, nint arg0, nint arg1, nint arg2)", aliasContent, StringComparison.Ordinal);
        Assert.Contains("callback(global::Rml.DataModelHandle.CreateManagedWrapper(arg0, ownsHandle: false)", aliasContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_ImGui_bindings_generate_imguinet_surface()
    {
        if (!GeneratorLogContains("Dear ImGui bindings: enabled"))
        {
            return;
        }

        var content = File.ReadAllText(Path.Combine(GetGeneratedDir(), "ThirdParty", "ImGui", "SharpCpp.Rbfx.imgui.g.cs"));

        Assert.Contains("namespace ImGuiNet", content, StringComparison.Ordinal);
        Assert.Contains("public static partial class ImGui", content, StringComparison.Ordinal);
        Assert.Contains("public enum ImGuiCond", content, StringComparison.Ordinal);
        Assert.Contains("public static void SetNextWindowPos(in global::Urho3D.Math.Vector2 pos, ImGuiCond cond, in global::Urho3D.Math.Vector2? pivot = default)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public static void SetNextWindowPos(in global::Urho3D.Math.Vector2 pos, ImGuiCond cond)\n", content, StringComparison.Ordinal);
        Assert.Contains("public static void PushTextWrapPos", content, StringComparison.Ordinal);
        Assert.Contains("public static bool Button(global::System.ReadOnlySpan<byte> label, in global::Urho3D.Math.Vector2? size = default)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public static bool Button(global::System.ReadOnlySpan<byte> label)\n", content, StringComparison.Ordinal);
        Assert.Contains("public static bool BeginItemTooltip", content, StringComparison.Ordinal);
        Assert.Contains("public static void EndTooltip", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Variant_helpers_expose_v1_style_type_mapping()
    {
        Assert.Equal(global::Urho3D.Core.VariantType.VarInt, global::Urho3D.Core.Variant.GetVariantType(typeof(int)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarBool, global::Urho3D.Core.Variant.GetVariantType(typeof(bool)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarString, global::Urho3D.Core.Variant.GetVariantType(typeof(string)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarVector3, global::Urho3D.Core.Variant.GetVariantType(typeof(global::Urho3D.Math.Vector3)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarResourceref, global::Urho3D.Core.Variant.GetVariantType(typeof(global::Urho3D.Core.ResourceRef)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarVariantvector, global::Urho3D.Core.Variant.GetVariantType(typeof(global::Ea.Vector<global::Urho3D.Core.Variant>)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarStringvariantmap, global::Urho3D.Core.Variant.GetVariantType(typeof(global::Ea.UnorderedMap<global::Ea.String, global::Urho3D.Core.Variant>)));
        Assert.Equal(global::Urho3D.Core.VariantType.VarNone, global::Urho3D.Core.Variant.GetVariantType(typeof(GeneratedSurfaceTests)));
    }

    [Fact]
    public void Handwritten_helpers_keep_ported_core_surface_available()
    {
        Assert.NotNull(typeof(global::Ea.String).GetConstructor([typeof(string)]));
        Assert.NotNull(typeof(global::Urho3D.Core.Context).GetProperty(nameof(global::Urho3D.Core.Context.Instance)));
        Assert.NotNull(typeof(global::Urho3D.Core.Context).GetMethod(nameof(global::Urho3D.Core.Context.SetRuntimeApi), [typeof(global::Urho3D.Script.ScriptRuntimeApi)]));
        Assert.NotNull(typeof(global::Urho3D.Core.Context).GetMethod(nameof(global::Urho3D.Core.Context.GetSubsystem), Type.EmptyTypes));
        Assert.NotNull(typeof(global::Urho3D.Core.Object).GetMethod(nameof(global::Urho3D.Core.Object.GetSubsystem), Type.EmptyTypes));
        Assert.NotNull(typeof(global::Urho3D.Core.WorkQueue).GetMethod(
            nameof(global::Urho3D.Core.WorkQueue.PostTaskForMainThread),
            [typeof(Action<uint, global::Urho3D.Core.WorkQueue>), typeof(global::Urho3D.Core.TaskPriority)]));
    }

    private static string GetGeneratedDir()
    {
        var dir = Environment.GetEnvironmentVariable("SharpCppGeneratedDir");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "..",
                "build", "native", "dev", "generated", "rbfx");
        }

        return Path.GetFullPath(dir);
    }

    private static bool GeneratorLogContains(string value)
    {
        var logPath = Path.Combine(GetGeneratedDir(), "generator.log");
        return File.Exists(logPath) && File.ReadAllText(logPath).Contains(value, StringComparison.Ordinal);
    }
}
