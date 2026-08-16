// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#include <d3dcompiler.h>
#include <windows.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static wchar_t* GetWindowsPath(const char* path)
{
    const int pathLength = MultiByteToWideChar(CP_UTF8, 0, path, -1, NULL, 0);
    const int prefixLength = path[0] == '/' ? 2 : 0;
    wchar_t* windowsPath = calloc((size_t)prefixLength + pathLength, sizeof(*windowsPath));
    if (windowsPath == NULL)
        return NULL;

    if (prefixLength != 0)
    {
        windowsPath[0] = L'Z';
        windowsPath[1] = L':';
    }
    MultiByteToWideChar(CP_UTF8, 0, path, -1, windowsPath + prefixLength, pathLength);
    for (wchar_t* character = windowsPath; *character != L'\0'; ++character)
    {
        if (*character == L'/')
            *character = L'\\';
    }
    return windowsPath;
}

int main(int argc, char** argv)
{
    if (argc != 3)
    {
        fprintf(stderr, "Usage: %s <input.hlsl> <output.h>\n", argv[0]);
        return 1;
    }

    wchar_t* inputPath = GetWindowsPath(argv[1]);
    wchar_t* outputPath = GetWindowsPath(argv[2]);
    if (inputPath == NULL || outputPath == NULL)
        return 1;

    ID3DBlob* bytecode = NULL;
    ID3DBlob* errors = NULL;
    const HRESULT result = D3DCompileFromFile(inputPath, NULL, D3D_COMPILE_STANDARD_FILE_INCLUDE,
        "main", "cs_5_0", 0, 0, &bytecode, &errors);
    if (errors != NULL)
    {
        fwrite(errors->lpVtbl->GetBufferPointer(errors), 1,
            errors->lpVtbl->GetBufferSize(errors), stderr);
        errors->lpVtbl->Release(errors);
    }
    free(inputPath);
    if (FAILED(result))
    {
        free(outputPath);
        return 1;
    }

    FILE* output = _wfopen(outputPath, L"wb");
    free(outputPath);
    if (output == NULL)
    {
        bytecode->lpVtbl->Release(bytecode);
        return 1;
    }

    const char* shaderName = strrchr(argv[1], '/');
    shaderName = shaderName != NULL ? shaderName + 1 : argv[1];
    const char* extension = strrchr(shaderName, '.');
    const int shaderNameLength = extension != NULL ? (int)(extension - shaderName) : (int)strlen(shaderName);
    fprintf(output, "#pragma once\n\nconst unsigned char g_p%.*s[] = {", shaderNameLength, shaderName);

    const unsigned char* data = bytecode->lpVtbl->GetBufferPointer(bytecode);
    const size_t size = bytecode->lpVtbl->GetBufferSize(bytecode);
    for (size_t index = 0; index < size; ++index)
        fprintf(output, "%s0x%02x%s", index % 12 == 0 ? "\n    " : " ", data[index], index + 1 == size ? "" : ",");
    fprintf(output, "\n};\n");

    fclose(output);
    bytecode->lpVtbl->Release(bytecode);
    return 0;
}
