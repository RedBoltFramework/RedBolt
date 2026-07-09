using System.Text;
using System.Text.RegularExpressions;

namespace RedBolt;

public enum ShaderStage
{
    Vertex,
    Pixel
}

public sealed class Shader : Resource
{
    private const string ShaderRoot = "Shaders/GLSL/v2/";
    private static readonly Regex VertexInputRegex = new(
        @"VERTEX_INPUT\((?<decl>[^)]*?\s(?<name>[A-Za-z_][A-Za-z0-9_]*))\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string _sourceCode = string.Empty;

    public Shader(string name)
        : base(name)
    {
    }

    public string SourceCode => _sourceCode;

    public DateTime LastWriteTimeUtc { get; private set; }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var resourceName = GetResourceName(Name);
        var fileName = !string.IsNullOrWhiteSpace(AbsoluteFileName)
            ? AbsoluteFileName
            : OwnerCache?.GetResourceFileName(resourceName) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var builder = new StringBuilder();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        LastWriteTimeUtc = DateTime.MinValue;
        if (!ProcessSource(resourceName, fileName, builder, visited))
            return false;

        _sourceCode = builder.ToString();
        MemoryUse = (uint)Encoding.UTF8.GetByteCount(_sourceCode);
        return true;
    }

    public string GetShaderName()
    {
        var resourceName = GetResourceName(Name).Replace('\\', '/');
        if (!resourceName.StartsWith(ShaderRoot, StringComparison.Ordinal))
            return resourceName;

        return resourceName[ShaderRoot.Length..^".glsl".Length];
    }

    public string PrepareSource(ShaderStage stage, string defines, GraphicsBackend backend = GraphicsBackend.OpenGL,
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
        if (normalized.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.StartsWith(ShaderRoot, StringComparison.Ordinal)
                ? normalized
                : ShaderRoot + normalized.TrimStart('/');
        }

        if (normalized.StartsWith(ShaderRoot, StringComparison.Ordinal))
            return normalized + ".glsl";

        return ShaderRoot + normalized.TrimStart('/') + ".glsl";
    }

    private bool ProcessSource(string resourceName, string fileName, StringBuilder builder, HashSet<string> visited)
    {
        var visitedKey = resourceName.Replace('\\', '/');
        if (!visited.Add(visitedKey))
            return true;

        LastWriteTimeUtc = DateTime.SpecifyKind(File.GetLastWriteTimeUtc(fileName), DateTimeKind.Utc) > LastWriteTimeUtc
            ? DateTime.SpecifyKind(File.GetLastWriteTimeUtc(fileName), DateTimeKind.Utc)
            : LastWriteTimeUtc;

        using var file = File.OpenRead(fileName);
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine() ?? string.Empty;
            if (TryParseInclude(line, out var includeName))
            {
                var includedResourceName = CombineResourcePath(resourceName, includeName);
                var includedFileName = OwnerCache?.GetResourceFileName(includedResourceName)
                    ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, includeName));
                if (!File.Exists(includedFileName) || !ProcessSource(includedResourceName, includedFileName, builder, visited))
                    return false;
            }
            else
            {
                builder.AppendLine(line);
            }
        }

        builder.AppendLine();
        return true;
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

    private static string GetBackendDefine(GraphicsBackend backend)
    {
        return backend switch
        {
            GraphicsBackend.OpenGL or GraphicsBackend.Auto => "URHO3D_OPENGL",
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

    private static string GetDefaultVersionDirective(GraphicsBackend backend)
    {
        return backend switch
        {
            GraphicsBackend.OpenGL or GraphicsBackend.Auto => "#version 410",
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
