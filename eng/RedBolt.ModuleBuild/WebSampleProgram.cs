// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Web;

using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Lz4;
using RedBolt.Bindings.Sdl;
using RedBolt.Engine;

internal static class WebProgram
{
    private static void Main()
    {
        var lz4Version = Lz4.Lz4VersionNumber();
        var sdlRevision = Encoding.UTF8.GetString(Sdl.SdlGetRevision());
        var diligentFactory = Diligent.LoadAndGetEngineFactoryOpenGL();
        var resourcesReady = ResourcesExist();
        // The browser frame loop continues after Run returns, so the page owns this application.
        var application = SampleCatalog.Create(BrowserDom.GetSelectedApplicationType());

        int exitCode;
        try
        {
            exitCode = application.Run(["--resource-paths", SampleCatalog.ResourcePaths]);
        }
        catch (Exception exception)
        {
            application.Dispose();
            var failureMessage = $"RedBolt WebAssembly sample failed with {exception.GetType().FullName}: {exception.Message}";
            Console.Error.WriteLine(exception);
            BrowserDom.SetResult(failureMessage, false);
            throw;
        }

        var success = exitCode == 0 && lz4Version > 0 && !string.IsNullOrEmpty(sdlRevision)
            && !diligentFactory.IsNull && resourcesReady;
        var message = exitCode == 0
            ? $"RedBolt WebAssembly engine initialized: LZ4 {lz4Version}, SDL {sdlRevision}, Diligent OpenGL factory {(!diligentFactory.IsNull ? "ready" : "missing")}, resources {(resourcesReady ? "ready" : "missing")}"
            : $"RedBolt WebAssembly engine failed: {application.ErrorMessage}";

        Console.WriteLine(message);
        BrowserDom.SetResult(message, success);
        if (!success)
            throw new InvalidOperationException(message);
    }

    private static bool ResourcesExist()
    {
        foreach (var resourcePath in SampleCatalog.ResourcePaths.Split(';'))
        {
            if (!File.Exists($"{resourcePath}.pak"))
                return false;
        }

        return true;
    }
}

internal static partial class BrowserDom
{
    [JSImport("dom.getSelectedApplicationType", "main.js")]
    internal static partial string GetSelectedApplicationType();

    [JSImport("dom.setResult", "main.js")]
    internal static partial void SetResult(string message, bool success);
}
