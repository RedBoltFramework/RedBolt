using System;
using System.Runtime.InteropServices.JavaScript;
using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;

var lz4Version = Lz4NativeBindings.Lz4VersionNumber();
var sdlRevision = SdlNativeBindings.SdlGetRevisionNumber();
var diligentFactory = DiligentNativeBindings.GetEngineFactoryOpenGL();
var resourcesReady = System.IO.File.Exists("CoreData/Models/Box.mdl")
    && System.IO.File.Exists("Data/Models/Mushroom.mdl")
    && System.IO.File.Exists("Data/Materials/Mushroom.xml");
var application = new MultipleViewportsApplication();
var exitCode = application.Run();
var success = exitCode == 0
    && lz4Version > 0
    && sdlRevision >= 0
    && !diligentFactory.IsNull
    && resourcesReady;
var message = exitCode == 0
    ? $"RedBolt WebAssembly engine initialized: LZ4 {lz4Version}, SDL {sdlRevision}, Diligent OpenGL factory {(!diligentFactory.IsNull ? "ready" : "missing")}, resources {(resourcesReady ? "ready" : "missing")}"
    : $"RedBolt WebAssembly engine failed: {application.ErrorMessage}";

Console.WriteLine(message);
BrowserDom.SetResult(message, success);

if (!success)
    throw new InvalidOperationException(message);

internal static partial class BrowserDom
{
    [JSImport("dom.setResult", "main.js")]
    internal static partial void SetResult(string message, bool success);
}
