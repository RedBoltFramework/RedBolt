// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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

    // char** parameters that SharpCpp's default shape (an output string) gets wrong.
    private static readonly HashSet<string> StringArrayFunctions =
        ["IMG_ReadXPMFromArray", "IMG_ReadXPMFromArrayToRGB888"];

    // char* returns that SDL allocates and the caller must SDL_free.
    private static readonly HashSet<string> OwnedStringReturnFunctions =
    [
        "SDL_GetClipboardText",
        "SDL_GetBasePath",
        "SDL_GetPrefPath",
        "SDL_iconv_string",
        "SDL_GameControllerMapping",
        "SDL_GameControllerMappingForDeviceIndex",
        "SDL_GameControllerMappingForGUID",
        "SDL_GameControllerMappingForIndex",
    ];

    private static readonly Dictionary<string, string> UnsupportedFunctions = new()
    {
        ["SDL_iconv"] = "inbuf/outbuf are in/out cursors into caller buffers; neither a string array nor an output string",
        ["SDL_strtokr"] = "saveptr is an in/out tokenizer cursor; neither a string array nor an output string",
        ["SDL_strtol"] = "endp points into the input string; use .NET number parsing instead",
        ["SDL_strtoll"] = "endp points into the input string; use .NET number parsing instead",
        ["SDL_strtoul"] = "endp points into the input string; use .NET number parsing instead",
        ["SDL_strtoull"] = "endp points into the input string; use .NET number parsing instead",
        ["SDL_strtod"] = "endp points into the input string; use .NET number parsing instead",
    };

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        foreach (var function in compilation.Functions)
        {
            if (UnsupportedFunctions.TryGetValue(function.NativeName, out var reason))
            {
                function.SetIgnoreReason(reason);
                continue;
            }

            if (OwnedStringReturnFunctions.Contains(function.NativeName))
                function.SetMetadata(MetadataKeys.FreedBy, "SDL_free");

            if (function.NativeName == "SDL_GetDefaultAudioInfo")
            {
                // The name out string is SDL-allocated and the caller must SDL_free it.
                foreach (var parameter in function.Parameters)
                {
                    if (parameter.NativeName == "name")
                        parameter.SetMetadata(MetadataKeys.FreedBy, "SDL_free");
                }
            }

            if (!StringArrayFunctions.Contains(function.NativeName))
                continue;

            foreach (var parameter in function.Parameters)
            {
                if (parameter.PointerPointer == PointerPointerKind.Chars)
                    parameter.SetMetadata(MetadataKeys.PointerPointerShape, PointerPointerShapes.Array);
            }
        }
    }

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
