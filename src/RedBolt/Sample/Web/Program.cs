namespace RedBolt.Sample.Web;

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;
using RedBolt.Platform;
using RedBolt.Sample.AggregatedInput;
using RedBolt.Sample.AnimatingScene;
using RedBolt.Sample.BakedLighting;
using RedBolt.Sample.Billboards;
using RedBolt.Sample.CameraOperator;
using RedBolt.Sample.CameraShake;
using RedBolt.Sample.Decals;
using RedBolt.Sample.DynamicGeometry;
using RedBolt.Sample.HelloGUI;
using RedBolt.Sample.HelloWorld;
using RedBolt.Sample.HugeObjectCount;
using RedBolt.Sample.InputLogger;
using RedBolt.Sample.LightAnimation;
using RedBolt.Sample.MaterialAnimation;
using RedBolt.Sample.MultipleViewports;
using RedBolt.Sample.PointerAdapter;
using RedBolt.Sample.RenderToTexture;
using RedBolt.Sample.SkeletalAnimation;
using RedBolt.Sample.SoundEffects;
using RedBolt.Sample.SoundSynthesis;
using RedBolt.Sample.StaticScene;
using RedBolt.Sample.TextureFormats;
using RedBolt.Sample.VirtualFileSystem;
using RedBolt.Sample.WindowSettings;

internal static class WebProgram
{
    private static void Main()
    {
        WebPlatform.Initialize();

        var lz4Version = Lz4NativeBindings.Lz4VersionNumber();
        var sdlRevision = Encoding.UTF8.GetString(SdlNativeBindings.SdlGetRevision());
        var diligentFactory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        var resourcesReady = System.IO.File.Exists("CoreData.pak")
            && System.IO.File.Exists("Data.pak");
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
