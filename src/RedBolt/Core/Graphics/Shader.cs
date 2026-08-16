// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;
using System.Text;
using System.Text.RegularExpressions;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

public enum ShaderStage
{
    Vertex,
    Pixel
}

[Reflected]
public sealed partial class Shader : ResourceType
{
    private const string UniversalShaderRoot = "Shaders/GLSL/";
    private const string ShaderRoot = "Shaders/GLSL/v2/";
    private static readonly Regex VertexInputRegex = new(
        @"VERTEX_INPUT\((?<decl>[^)]*?\s(?<name>[A-Za-z_][A-Za-z0-9_]*))\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<(ShaderType Type, StringHash Defines), ShaderVariation> _variations = new();
    private static readonly Dictionary<string, uint> FileToIndexMapping = new(StringComparer.Ordinal);
    private string _sourceCode = string.Empty;
    private DateTime _timeStamp;
    private uint _numVariations;

    /// <summary>Signals that shader source code was reloaded.</summary>
    public event Action? OnReloaded;

    public Shader()
    {
        RefreshMemoryUse();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        GetSubsystem<ResourceCache>()?.ResetDependencies(this);

        var disposedVariations = new HashSet<ShaderVariation>();
        foreach (var variation in _variations.Values)
        {
            if (disposedVariations.Add(variation))
                variation.Dispose();
        }
        _variations.Clear();
        _numVariations = 0;
        _timeStamp = default;
        _sourceCode = string.Empty;
        OnReloaded = null;
        base.Dispose();
    }


    /// <summary>Return either vertex or pixel shader source code.</summary>
    public string SourceCode => _sourceCode;

    /// <summary>Return the latest timestamp of the shader code and its includes.</summary>
    public DateTime TimeStamp => _timeStamp;

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var resourceName = GetResourceName(Name);
        var builder = new StringBuilder();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        _timeStamp = DateTime.MinValue;
        ProcessSource(resourceName, builder, visited, source);

        _sourceCode = builder.ToString();
        if (GetSubsystem<Graphics>()!.GetSettings().ValidateShaders)
        {
            static bool IsAllowedCharacter(char character)
            {
                if (character > 127 || char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
                    return character <= 127;

                return "_.+-/*%<>[](){}^|&~=!:,?#;".IndexOf(character) >= 0;
            }

            var invalidIndex = -1;
            for (var i = 0; i < _sourceCode.Length; ++i)
            {
                if (!IsAllowedCharacter(_sourceCode[i]))
                {
                    invalidIndex = i;
                    break;
                }
            }
            if (invalidIndex >= 0)
            {
                var snippetStart = _sourceCode.LastIndexOf('\n', SystemMath.Max(0, invalidIndex - 1));
                var snippetEnd = _sourceCode.IndexOf('\n', invalidIndex);
                if (snippetStart < 0)
                    snippetStart = 0;
                if (snippetEnd < 0)
                    snippetEnd = _sourceCode.Length;

                Log.GetLogger().LogWarning(
                    "Unexpected character #{Code} '{Character}' in shader code:\n{Snippet}",
                    (uint)_sourceCode[invalidIndex], _sourceCode[invalidIndex],
                    _sourceCode[snippetStart..snippetEnd]);
            }
        }

        RefreshMemoryUse();
        return true;
    }

    /// <summary>Finish resource loading. Always called from the main thread. Return true if successful.</summary>
    public override bool EndLoad()
    {
        OnReloaded?.Invoke();
        return true;
    }

    /// <summary>Return a variation with defines. Separate multiple defines with spaces.</summary>
    public ShaderVariation GetVariation(ShaderType type, string defines)
    {
        ArgumentNullException.ThrowIfNull(defines);

        var key = (type, new StringHash(defines));
        if (_variations.TryGetValue(key, out var variation))
            return variation;

        var normalizedDefines = string.Join(' ', NormalizeDefines(defines));
        var normalizedKey = (type, new StringHash(normalizedDefines));
        if (_variations.TryGetValue(normalizedKey, out variation))
        {
            _variations[key] = variation;
            return variation;
        }

        variation = new ShaderVariation(this, type, normalizedDefines);
        _variations[key] = variation;
        ++_numVariations;
        RefreshMemoryUse();
        return variation;
    }

    public string GetShaderName()
    {
        var resourceName = GetResourceName(Name).Replace('\\', '/');
        const string prefix = "Shaders/GLSL/v2";
        if (!resourceName.StartsWith(prefix, StringComparison.Ordinal))
            return resourceName;

        var shaderName = resourceName[prefix.Length..].TrimStart('/');
        return shaderName.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase)
            ? shaderName[..^".glsl".Length]
            : shaderName;
    }

    /// <summary>Return global list of shader files.</summary>
    public static string GetShaderFileList()
    {
        var fileList = FileToIndexMapping
            .OrderBy(static item => item.Value)
            .ToArray();

        var result = new StringBuilder("Shader Files:\n");
        foreach (var item in fileList)
            result.Append(item.Value).Append(": ").Append(item.Key).Append('\n');
        result.Append('\n');
        return result.ToString();
    }

    public string PrepareSource(ShaderStage stage, string defines, RenderBackend backend = RenderBackend.OpenGL,
        string extraCode = "")
    {
        var (body, extensionDirectives) = ExtractExtensionDirectives(_sourceCode);
        var versionRange = FindVersionDirective(body);

        var builder = new StringBuilder();
        if (versionRange.HasValue)
            builder.Append(body, versionRange.Value.Start, versionRange.Value.Length).AppendLine();
        else
            builder.AppendLine(GetDefaultVersionDirective(backend));

        foreach (var extensionDirective in extensionDirectives)
            builder.AppendLine(extensionDirective);

        builder.AppendLine(stage == ShaderStage.Vertex ? "#define COMPILEVS" : "#define COMPILEPS");
        builder.AppendLine($"#define {GetBackendDefine(backend)}");

        foreach (var define in NormalizeDefines(defines))
        {
            var normalizedDefine = define.Replace('=', ' ');
            builder.Append("#define ");
            builder.Append(normalizedDefine);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(extraCode))
            builder.AppendLine(extraCode);

        if (!versionRange.HasValue)
        {
            builder.Append(body);
        }
        else
        {
            builder.Append(body.AsSpan(0, versionRange.Value.Start));
            builder.Append("//");
            builder.Append(body.AsSpan(versionRange.Value.Start));
        }

        var source = builder.ToString();
        return stage == ShaderStage.Vertex ? ApplyVertexInputLocations(source) : source;
    }

    private static string ApplyVertexInputLocations(string source)
    {
        return VertexInputRegex.Replace(source, match =>
        {
            var name = match.Groups["name"].Value;
            if (!TryGetVertexInputLocation(name, out var location))
                return match.Value;

            var declaration = match.Groups["decl"].Value;
            return $"layout(location = {location}) in {declaration};";
        });
    }

    private static bool TryGetVertexInputLocation(string name, out uint location)
    {
        // Keep these locations in sync with Graphics.BuildLayoutElements.
        location = name switch
        {
            "iPos" => 0,
            "iBlendWeights" => 1,
            "iBlendIndices" => 2,
            "iTexCoord" => 3,
            "iTexCoord1" => 4,
            "iColor" => 5,
            "iNormal" => 6,
            "iTangent" => 7,
            _ => uint.MaxValue
        };

        return location != uint.MaxValue;
    }

    public static string GetResourceName(string shaderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);

        var normalized = shaderName.Trim().Replace('\\', '/');
        var shaderRoot = normalized.StartsWith("v2/", StringComparison.Ordinal)
            ? UniversalShaderRoot
            : ShaderRoot;
        if (normalized.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.StartsWith(UniversalShaderRoot, StringComparison.Ordinal)
                ? normalized
                : shaderRoot + normalized.TrimStart('/');
        }

        if (normalized.StartsWith(UniversalShaderRoot, StringComparison.Ordinal))
            return normalized + ".glsl";

        return shaderRoot + normalized.TrimStart('/') + ".glsl";
    }

    private void ProcessSource(
        string resourceName, StringBuilder builder, HashSet<string> visited, Deserializer? sourceStream = null)
    {
        var visitedKey = resourceName.Replace('\\', '/');
        if (!visited.Add(visitedKey))
            return;

        var cache = GetSubsystem<ResourceCache>();
        var sourceTimeStamp = cache?.GetLastModifiedTime(resourceName) ?? DateTime.MinValue;
        _timeStamp = sourceTimeStamp > _timeStamp ? sourceTimeStamp : _timeStamp;

        var fileIndex = FileToIndexMapping.GetValueOrDefault(resourceName);
        if (fileIndex == 0)
        {
            fileIndex = (uint)(FileToIndexMapping.Count + 1);
            FileToIndexMapping[resourceName] = fileIndex;
        }

        var sourceText = sourceStream is not null
            ? ReadSourceStream(sourceStream)
            : ReadSourceFile(resourceName);
        if (sourceText is null)
            return;

        using var reader = new StringReader(sourceText);
        var currentLine = 1u;
        var numNewLines = 0u;
        builder.Append(FormatLineDirective(true, resourceName, fileIndex, currentLine));
        string? sourceLine;
        while ((sourceLine = reader.ReadLine()) is not null)
        {
            var line = sourceLine;
            if (line.StartsWith("#include", StringComparison.Ordinal))
            {
                var includeName = line[9..].Replace("\"", string.Empty, StringComparison.Ordinal).Trim();
                var includedResourceName = CombineResourcePath(resourceName, includeName);
                if (cache?.Exists(includedResourceName) == true)
                {
                    cache.StoreResourceDependency(this, includedResourceName);
                    ProcessSource(includedResourceName, builder, visited);
                }
                else
                    builder.Append($"#error Missing include file <{includedResourceName}>\n");

                builder.Append(FormatLineDirective(true, resourceName, fileIndex, currentLine));
            }
            else
            {
                var isLineContinuation = line.Length >= 1 && line[^1] == '\\';
                if (isLineContinuation)
                    line = line[..^1];

                var validateShaders = GetSubsystem<Graphics>()!.GetSettings().ValidateShaders;
                if (!validateShaders || !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    builder.Append(line);
                ++numNewLines;
                if (!isLineContinuation)
                {
                    for (var i = 0u; i < numNewLines; ++i)
                        builder.Append('\n');
                    numNewLines = 0;
                }
            }

            ++currentLine;
        }

        builder.AppendLine();
    }

    private static string ReadSourceStream(Deserializer source)
    {
        var data = new byte[checked((int)(source.Size - source.Position))];
        source.Read(data);
        return Encoding.UTF8.GetString(data);
    }

    private string? ReadSourceFile(string resourceName)
    {
        var file = GetSubsystem<ResourceCache>()?.GetFile(resourceName);
        if (file is null)
            return null;

        try
        {
            return ReadSourceStream(file);
        }
        finally
        {
            file.Dispose();
        }
    }

    private static string FormatLineDirective(bool isGLSL, string fileName, uint fileIndex, uint line)
    {
        return isGLSL
            ? $"/// #include {fileName}\n#line {line} {fileIndex}\n"
            : $"#line {line} \"{fileName}\"\n";
    }

    private void RefreshMemoryUse()
    {
        SetMemoryUse(checked((int)(Encoding.UTF8.GetByteCount(_sourceCode) + _numVariations)));
    }

    private static IEnumerable<string> NormalizeDefines(string defines)
    {
        if (string.IsNullOrWhiteSpace(defines))
            return Array.Empty<string>();

        return defines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static define => define.ToUpperInvariant())
            .OrderBy(static define => define, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetBackendDefine(RenderBackend backend)
    {
        return backend switch
        {
            RenderBackend.OpenGL => "URHO3D_OPENGL",
            _ => $"URHO3D_{backend.ToString().ToUpperInvariant()}"
        };
    }

    private static bool TryParseInclude(string line, out string includeName)
    {
        includeName = string.Empty;
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("#include", StringComparison.Ordinal))
            return false;

        var start = trimmed.IndexOf('"');
        var end = trimmed.LastIndexOf('"');
        if (start < 0 || end <= start)
            return false;

        includeName = trimmed[(start + 1)..end];
        return !string.IsNullOrWhiteSpace(includeName);
    }

    private static string CombineResourcePath(string resourceName, string includeName)
    {
        var normalizedResource = resourceName.Replace('\\', '/');
        var slashIndex = normalizedResource.LastIndexOf('/');
        var directory = slashIndex >= 0 ? normalizedResource[..(slashIndex + 1)] : string.Empty;
        return (directory + includeName).Replace('\\', '/');
    }

    private static string GetDefaultVersionDirective(RenderBackend backend)
    {
        return backend switch
        {
            RenderBackend.OpenGL => "#version 410",
            _ => "#version 410"
        };
    }

    private static (int Start, int Length)? FindVersionDirective(string source)
    {
        var start = source.IndexOf("#version", StringComparison.Ordinal);
        if (start < 0)
            return null;

        var end = source.IndexOf('\n', start);
        return end < 0 ? (start, source.Length - start) : (start, end - start);
    }

    private static (string Body, IReadOnlyList<string> ExtensionDirectives) ExtractExtensionDirectives(string source)
    {
        var lines = source.Split('\n');
        var builder = new StringBuilder();
        var extensionDirectives = new List<string>();
        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith("#extension", StringComparison.Ordinal))
                extensionDirectives.Add(trimmedLine.TrimEnd('\r'));
            else
                builder.AppendLine(line.TrimEnd('\r'));
        }

        return (builder.ToString(), extensionDirectives);
    }
}
