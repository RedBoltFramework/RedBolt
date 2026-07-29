namespace RedBolt.Sample.Web;

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;
using RedBolt.Platform;

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
        var applicationTypeName = BrowserDom.GetSelectedApplicationType();
        var applicationType = typeof(WebProgram).Assembly.GetType(applicationTypeName, true)!;
        if (!typeof(Application).IsAssignableFrom(applicationType))
            throw new InvalidOperationException($"Unknown RedBolt sample application: {applicationTypeName}");
        var application = (Application?)Activator.CreateInstance(applicationType, true)
            ?? throw new InvalidOperationException($"Cannot create RedBolt sample application: {applicationTypeName}");
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
    [JSImport("dom.getSelectedApplicationType", "main.js")]
    internal static partial string GetSelectedApplicationType();

    [JSImport("dom.setResult", "main.js")]
    internal static partial void SetResult(string message, bool success);
}
