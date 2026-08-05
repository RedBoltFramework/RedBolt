namespace RedBolt.Generator.Dependencies;

using System;
using System.IO;

using SharpCpp.Model.Logging;

internal static class Program
{
    private static int Main(string[] args)
    {
        StreamWriter? logWriter = null;

        try
        {
            var options = GeneratorOptions.Parse(args);
            var generator = CreateGenerator(options.Surface, options.NativeLibraryName);
            Directory.CreateDirectory(options.OutputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(options.LogPath)!);
            logWriter = new StreamWriter(options.LogPath, false);
            SharpCppLog.Configure(fileWriter: logWriter);

            var summary = generator.Generate(options);
            Console.WriteLine(summary);
            logWriter.WriteLine(summary);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            logWriter?.WriteLine(ex);
            PrintUsage();
            return 1;
        }
        finally
        {
            SharpCppLog.Configure();
            logWriter?.Dispose();
        }
    }

    private static BindingTargetGenerator CreateGenerator(string surface, string nativeLibraryName)
    {
        return surface.ToLowerInvariant() switch
        {
            "sdl" => new SdlGenerator(nativeLibraryName),
            "diligent" => new DiligentGenerator(nativeLibraryName),
            "dearimgui" => new DearImGuiGenerator(nativeLibraryName),
            "stb" => new StbGenerator(nativeLibraryName),
            "tinygltf" => new TinyGltfGenerator(nativeLibraryName),
            "lz4" => new Lz4Generator(nativeLibraryName),
            "harfbuzz" => new HarfBuzzGenerator(nativeLibraryName),
            "rmlui" => new RmlUiGenerator(nativeLibraryName),
            "webp" => new WebPGenerator(nativeLibraryName),
            "xatlas" => new XAtlasGenerator(nativeLibraryName),
            "embree" => new EmbreeGenerator(nativeLibraryName),
            "glslang" => new GlslangGenerator(nativeLibraryName),
            "spirvcross" => new SpirVCrossGenerator(nativeLibraryName),
            "spirvtools" => new SpirVToolsGenerator(nativeLibraryName),
            "shadercompiler" => new ShaderCompilerGenerator(nativeLibraryName),
            "jolt" => new JoltGenerator(nativeLibraryName),
            _ => throw new ArgumentException($"Unknown binding surface '{surface}'.", nameof(surface))
        };
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: RedBolt.Generator.Dependencies --surface <sdl|diligent|dearimgui|stb|tinygltf|lz4|harfbuzz|rmlui|webp|xatlas|embree|glslang|spirvcross|spirvtools|shadercompiler|jolt> --header <path> --include-name <include> --output-dir <dir> --file-stem <stem> --metadata <path> --log <path> --sharpcpp-root <path> --redbolt-root <path> --native-library-name <name> --compiler <path> [--include <dir>]... [--define <name[=value]>]... [--owned-header <path>]...");
    }
}
