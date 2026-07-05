using System.Text;

namespace RedBolt;

public enum ShaderStage
{
    Vertex,
    Pixel
}

public sealed class Shader : Resource
{
    private const string ShaderRoot = "Shaders/GLSL/v2/";

    private string sourceCode = string.Empty;

    public Shader(string name)
        : base(name)
    {
    }

    public string SourceCode => sourceCode;

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

        sourceCode = builder.ToString();
        MemoryUse = (uint)Encoding.UTF8.GetByteCount(sourceCode);
        return true;
    }

    public string GetShaderName()
    {
        var resourceName = GetResourceName(Name).Replace('\\', '/');
        if (!resourceName.StartsWith(ShaderRoot, StringComparison.Ordinal))
            return resourceName;

        return resourceName[ShaderRoot.Length..^".glsl".Length];
    }

    public string PrepareSource(ShaderStage stage, string defines, GraphicsBackend backend = GraphicsBackend.OpenGL)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#version 410");
        builder.AppendLine(stage == ShaderStage.Vertex ? "#define COMPILEVS" : "#define COMPILEPS");
        builder.AppendLine($"#define {GetBackendDefine(backend)}");

        foreach (var define in NormalizeDefines(defines))
        {
            var normalizedDefine = define.Replace('=', ' ');
            builder.Append("#define ");
            builder.Append(normalizedDefine);
            builder.AppendLine();
        }

        builder.Append(sourceCode);
        return builder.ToString();
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
}
