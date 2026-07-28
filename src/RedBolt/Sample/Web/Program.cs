using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;

internal static class WebProgram
{
    private static void Main()
    {
        WebPlatform.Initialize();

        var lz4Version = Lz4NativeBindings.Lz4VersionNumber();
        var sdlRevision = Encoding.UTF8.GetString(SdlNativeBindings.SdlGetRevision());
        var diligentFactory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        var resourcesReady = System.IO.File.Exists("CoreData/Models/Box.mdl")
            && System.IO.File.Exists("Data/Models/Mushroom.mdl")
            && System.IO.File.Exists("Data/Materials/Mushroom.xml");
        var sampleName = BrowserDom.GetSelectedSample();
        Application application = sampleName switch
        {
            "aggregated-input" => new AggregatedInputApplication(),
            "animating-scene" => new AnimatingScene(),
            "baked-lighting" => new BakedLighting(),
            "billboards" => new BillboardsApplication(),
            "camera-operator" => new CameraOperatorSampleApplication(),
            "camera-shake" => new CameraShakeApplication(),
            "decals" => new DecalsApplication(),
            "dynamic-geometry" => new DynamicGeometry(),
            "hello-gui" => new HelloGUI(),
            "hello-world" => new HelloWorld(),
            "huge-object-count" => new HugeObjectCountApplication(),
            "input-logger" => new InputLoggerApplication(),
            "light-animation" => new LightAnimationApplication(),
            "material-animation" => new MaterialAnimationApplication(),
            "multiple-viewports" => new MultipleViewports(),
            "pointer-adapter" => new PointerAdapterApplication(),
            "render-to-texture" => new RenderToTexture(),
            "skeletal-animation" => new SkeletalAnimationApplication(),
            "sound-effects" => new SoundEffects(),
            "sound-synthesis" => new SoundSynthesis(),
            "static-scene" => new StaticScene(),
            "texture-formats" => new TextureFormatsSample(),
            "virtual-file-system" => new VirtualFileSystemApplication(),
            "window-settings" => new WindowSettingsDemo(),
            _ => throw new InvalidOperationException($"Unknown RedBolt sample: {sampleName}")
        };
        int exitCode;
        try
        {
            exitCode = application.Run();
        }
        catch (Exception exception)
        {
            var failureMessage = $"RedBolt WebAssembly sample failed with {exception.GetType().FullName}: {exception.Message}";
            Console.Error.WriteLine(exception);
            BrowserDom.SetResult(failureMessage, false);
            throw;
        }
        var success = exitCode == 0
            && lz4Version > 0
            && !string.IsNullOrEmpty(sdlRevision)
            && !diligentFactory.IsNull
            && resourcesReady;
        var message = exitCode == 0
            ? $"RedBolt WebAssembly engine initialized: LZ4 {lz4Version}, SDL {sdlRevision}, Diligent OpenGL factory {(!diligentFactory.IsNull ? "ready" : "missing")}, resources {(resourcesReady ? "ready" : "missing")}"
            : $"RedBolt WebAssembly engine failed: {application.ErrorMessage}";

        Console.WriteLine(message);
        BrowserDom.SetResult(message, success);

        if (!success)
            throw new InvalidOperationException(message);
    }
}

internal static partial class BrowserDom
{
    [JSImport("dom.getSelectedSample", "main.js")]
    internal static partial string GetSelectedSample();

    [JSImport("dom.setResult", "main.js")]
    internal static partial void SetResult(string message, bool success);
}
