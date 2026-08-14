namespace RedBolt.Generator.Dependencies;

using SharpCpp.Model.Ast;

internal sealed class SdlGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "sdl",
        "RedBolt.Bindings.Sdl",
        "RedBolt.Bindings.Sdl",
        "Sdl",
        nativeLibraryName,
        "redbolt_sdl",
        "-xc",
        "-std=c17",
        true)
{
    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";

    protected override bool IncludeCallbackReleaseRegistration => true;

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            if (compilation.Members[index] is not FunctionBinding { NativeName: "SDL_GetRevisionNumber" })
                continue;

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }
        compilation.InvalidateCaches();
        return prunedCount;
    }
}
