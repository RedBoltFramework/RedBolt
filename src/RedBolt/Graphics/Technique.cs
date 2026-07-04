namespace RedBolt;

public enum BlendMode
{
    Replace,
    Add,
    Multiply,
    Alpha,
    AddAlpha,
    PremulAlpha,
    InvDestAlpha,
    Subtract,
    SubtractAlpha,
    Decal
}

public enum CompareMode
{
    Always,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual
}

public enum LightingMode
{
    Unlit,
    PerVertex,
    PerPixel
}

public sealed class Pass
{
    private static readonly IReadOnlyList<string> DefaultPixelTextureDefines =
    [
        "Albedo",
        "Normal",
        "Properties",
        "Emission"
    ];

    private readonly List<string> vertexTextureDefines = [];
    private readonly List<string> pixelTextureDefines = [];
    private uint pipelineStateHash;

    public Pass(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.ToLowerInvariant();
        Index = Technique.GetPassIndex(Name);
        SetPixelTextureDefines(DefaultPixelTextureDefines);
    }

    public string Name { get; }

    public uint Index { get; }

    public BlendMode BlendMode { get; private set; } = BlendMode.Replace;

    public CullMode CullMode { get; private set; } = CullMode.Max;

    public CompareMode DepthTestMode { get; private set; } = CompareMode.LessEqual;

    public uint ShadersLoadedFrameNumber { get; private set; }

    public bool ColorWrite { get; private set; } = true;

    public bool DepthWrite { get; private set; } = true;

    public bool AlphaToCoverage { get; private set; }

    public bool IsAlphaMask { get; private set; }

    public string VertexShader { get; private set; } = string.Empty;

    public string PixelShader { get; private set; } = string.Empty;

    public string VertexShaderDefines { get; private set; } = string.Empty;

    public string PixelShaderDefines { get; private set; } = string.Empty;

    public string VertexShaderDefineExcludes { get; private set; } = string.Empty;

    public string PixelShaderDefineExcludes { get; private set; } = string.Empty;

    public IReadOnlyList<string> VertexTextureDefines => vertexTextureDefines;

    public IReadOnlyList<string> PixelTextureDefines => pixelTextureDefines;

    public uint GetPipelineStateHash() => pipelineStateHash;

    public void SetBlendMode(BlendMode mode)
    {
        BlendMode = mode;
        RefreshPipelineStateHash();
    }

    public void SetCullMode(CullMode mode)
    {
        CullMode = mode;
        RefreshPipelineStateHash();
    }

    public void SetDepthTestMode(CompareMode mode)
    {
        DepthTestMode = mode;
        RefreshPipelineStateHash();
    }

    public void SetColorWrite(bool enable)
    {
        ColorWrite = enable;
        RefreshPipelineStateHash();
    }

    public void SetDepthWrite(bool enable)
    {
        DepthWrite = enable;
        RefreshPipelineStateHash();
    }

    public void SetAlphaToCoverage(bool enable)
    {
        AlphaToCoverage = enable;
        RefreshPipelineStateHash();
    }

    public void SetVertexShader(string name)
    {
        VertexShader = name ?? string.Empty;
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetPixelShader(string name)
    {
        PixelShader = name ?? string.Empty;
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetVertexShaderDefines(string defines)
    {
        VertexShaderDefines = NormalizeDefines(defines);
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetPixelShaderDefines(string defines)
    {
        PixelShaderDefines = NormalizeDefines(defines);
        IsAlphaMask = PixelShaderDefines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("ALPHAMASK", StringComparer.OrdinalIgnoreCase);
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetVertexShaderDefineExcludes(string excludes)
    {
        VertexShaderDefineExcludes = NormalizeDefines(excludes);
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetPixelShaderDefineExcludes(string excludes)
    {
        PixelShaderDefineExcludes = NormalizeDefines(excludes);
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetVertexTextureDefines(IEnumerable<string> textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        vertexTextureDefines.Clear();
        vertexTextureDefines.AddRange(textures.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void SetPixelTextureDefines(IEnumerable<string> textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        pixelTextureDefines.Clear();
        pixelTextureDefines.AddRange(textures.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));
        ReleaseShaders();
        RefreshPipelineStateHash();
    }

    public void ReleaseShaders()
    {
    }

    public void MarkShadersLoaded(uint frameNumber)
    {
        ShadersLoadedFrameNumber = frameNumber;
    }

    public string GetEffectiveVertexShaderDefines() => FilterDefines(VertexShaderDefines, VertexShaderDefineExcludes);

    public string GetEffectivePixelShaderDefines() => FilterDefines(PixelShaderDefines, PixelShaderDefineExcludes);

    public Pass Clone()
    {
        var clone = new Pass(Name);
        clone.SetBlendMode(BlendMode);
        clone.SetCullMode(CullMode);
        clone.SetDepthTestMode(DepthTestMode);
        clone.SetColorWrite(ColorWrite);
        clone.SetDepthWrite(DepthWrite);
        clone.SetAlphaToCoverage(AlphaToCoverage);
        clone.SetVertexShader(VertexShader);
        clone.SetPixelShader(PixelShader);
        clone.SetVertexShaderDefines(VertexShaderDefines);
        clone.SetPixelShaderDefines(PixelShaderDefines);
        clone.SetVertexShaderDefineExcludes(VertexShaderDefineExcludes);
        clone.SetPixelShaderDefineExcludes(PixelShaderDefineExcludes);
        clone.SetVertexTextureDefines(vertexTextureDefines);
        clone.SetPixelTextureDefines(pixelTextureDefines);
        clone.MarkShadersLoaded(ShadersLoadedFrameNumber);
        return clone;
    }

    private static string FilterDefines(string defines, string excludes)
    {
        if (string.IsNullOrWhiteSpace(excludes))
            return defines;

        var defineSet = defines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        foreach (var exclude in excludes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            defineSet.RemoveAll(define => string.Equals(define, exclude, StringComparison.Ordinal));
        return string.Join(' ', defineSet);
    }

    private static string NormalizeDefines(string? defines)
    {
        return string.IsNullOrWhiteSpace(defines)
            ? string.Empty
            : string.Join(' ', defines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private void RefreshPipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(BlendMode);
        hash.Add(CullMode);
        hash.Add(DepthTestMode);
        hash.Add(ColorWrite);
        hash.Add(DepthWrite);
        hash.Add(AlphaToCoverage);
        hash.Add(VertexShader, StringComparer.Ordinal);
        hash.Add(PixelShader, StringComparer.Ordinal);
        hash.Add(VertexShaderDefines, StringComparer.Ordinal);
        hash.Add(PixelShaderDefines, StringComparer.Ordinal);
        hash.Add(VertexShaderDefineExcludes, StringComparer.Ordinal);
        hash.Add(PixelShaderDefineExcludes, StringComparer.Ordinal);
        foreach (var define in vertexTextureDefines)
            hash.Add(define, StringComparer.Ordinal);
        foreach (var define in pixelTextureDefines)
            hash.Add(define, StringComparer.Ordinal);
        pipelineStateHash = unchecked((uint)hash.ToHashCode());
    }
}

public sealed class Technique : Resource
{
    private static readonly Dictionary<string, uint> PassIndices = new(StringComparer.Ordinal);
    private readonly List<Pass?> passes = [];
    private readonly Dictionary<(uint VertexDefines, uint PixelDefines), Technique> cloneTechniques = new();

    static Technique()
    {
        BasePassIndex = RegisterBuiltInPass("base", 0);
        AlphaPassIndex = RegisterBuiltInPass("alpha", 1);
        MaterialPassIndex = RegisterBuiltInPass("material", 2);
        DeferredPassIndex = RegisterBuiltInPass("deferred", 3);
        LightPassIndex = RegisterBuiltInPass("light", 4);
        LitBasePassIndex = RegisterBuiltInPass("litbase", 5);
        LitAlphaPassIndex = RegisterBuiltInPass("litalpha", 6);
        ShadowPassIndex = RegisterBuiltInPass("shadow", 7);
    }

    public Technique(string name)
        : base(name)
    {
    }

    public static uint BasePassIndex { get; }

    public static uint AlphaPassIndex { get; }

    public static uint MaterialPassIndex { get; }

    public static uint DeferredPassIndex { get; }

    public static uint LightPassIndex { get; }

    public static uint LitBasePassIndex { get; }

    public static uint LitAlphaPassIndex { get; }

    public static uint ShadowPassIndex { get; }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        passes.Clear();
        cloneTechniques.Clear();
        MemoryUse = 0;

        var xml = new XMLFile(Name);
        if (!xml.Load(source))
            return false;

        var root = xml.GetRoot();
        var globalVs = root.GetAttribute("vs");
        var globalPs = root.GetAttribute("ps");
        var globalVsDefines = AppendDefineSuffix(root.GetAttribute("vsdefines"));
        var globalPsDefines = AppendDefineSuffix(root.GetAttribute("psdefines"));

        for (var passElement = root.GetChild("pass"); passElement.NotNull; passElement = passElement.GetNext("pass"))
        {
            if (!passElement.HasAttribute("name"))
                return false;

            var pass = CreatePass(passElement.GetAttribute("name"));
            if (passElement.HasAttribute("vs"))
            {
                pass.SetVertexShader(passElement.GetAttribute("vs"));
                pass.SetVertexShaderDefines(passElement.GetAttribute("vsdefines"));
            }
            else
            {
                pass.SetVertexShader(globalVs);
                pass.SetVertexShaderDefines(globalVsDefines + passElement.GetAttribute("vsdefines"));
            }

            if (passElement.HasAttribute("ps"))
            {
                pass.SetPixelShader(passElement.GetAttribute("ps"));
                pass.SetPixelShaderDefines(passElement.GetAttribute("psdefines"));
            }
            else
            {
                pass.SetPixelShader(globalPs);
                pass.SetPixelShaderDefines(globalPsDefines + passElement.GetAttribute("psdefines"));
            }

            var passExcludes = AppendDefineSuffix(passElement.GetAttribute("excludes"));
            pass.SetVertexShaderDefineExcludes(passExcludes + passElement.GetAttribute("vsexcludes"));
            pass.SetPixelShaderDefineExcludes(passExcludes + passElement.GetAttribute("psexcludes"));

            if (passElement.HasAttribute("vstextures"))
                pass.SetVertexTextureDefines(passElement.GetAttribute("vstextures").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (passElement.HasAttribute("pstextures"))
                pass.SetPixelTextureDefines(passElement.GetAttribute("pstextures").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (passElement.HasAttribute("blend"))
                pass.SetBlendMode(ParseBlendMode(passElement.GetAttribute("blend")));
            if (passElement.HasAttribute("cull"))
                pass.SetCullMode(ParseCullMode(passElement.GetAttribute("cull")));
            if (passElement.HasAttribute("depthtest"))
                pass.SetDepthTestMode(ParseCompareMode(passElement.GetAttribute("depthtest")));
            if (passElement.HasAttribute("colorwrite"))
                pass.SetColorWrite(passElement.GetBool("colorwrite"));
            if (passElement.HasAttribute("depthwrite"))
                pass.SetDepthWrite(passElement.GetBool("depthwrite"));
            if (passElement.HasAttribute("alphatocoverage"))
                pass.SetAlphaToCoverage(passElement.GetBool("alphatocoverage"));
        }

        MemoryUse = (uint)(passes.Count * 128);
        return true;
    }

    public Pass CreatePass(string name)
    {
        var existing = GetPass(name);
        if (existing is not null)
            return existing;

        var pass = new Pass(name);
        if (pass.Index >= passes.Count)
        {
            while (passes.Count <= pass.Index)
                passes.Add(null);
        }

        passes[(int)pass.Index] = pass;
        MemoryUse = (uint)(GetNumPasses() * 128);
        return pass;
    }

    public void RemovePass(string name)
    {
        var index = GetPassIndex(name);
        if (index < passes.Count)
            passes[(int)index] = null;
        MemoryUse = (uint)(GetNumPasses() * 128);
    }

    public void ReleaseShaders()
    {
        foreach (var pass in passes)
            pass?.ReleaseShaders();
    }

    public Technique Clone(string cloneName = "")
    {
        var clone = new Technique(string.IsNullOrEmpty(cloneName) ? Name : cloneName);
        foreach (var pass in passes)
        {
            if (pass is null)
                continue;

            var clonedPass = clone.CreatePass(pass.Name);
            CopyPass(clonedPass, pass);
        }

        return clone;
    }

    public bool HasPass(uint passIndex) => passIndex < passes.Count && passes[(int)passIndex] is not null;

    public bool HasPass(string name) => GetPass(name) is not null;

    public Pass? GetPass(uint passIndex) => passIndex < passes.Count ? passes[(int)passIndex] : null;

    public Pass? GetPass(string name)
    {
        var index = GetPassIndex(name);
        return GetPass(index);
    }

    public uint GetNumPasses() => (uint)passes.Count(static pass => pass is not null);

    public IReadOnlyList<string> GetPassNames() => passes.Where(static pass => pass is not null).Select(static pass => pass!.Name).ToArray();

    public IReadOnlyList<Pass> GetPasses() => passes.Where(static pass => pass is not null).Select(static pass => pass!).ToArray();

    public Technique CloneWithDefines(string vsDefines, string psDefines)
    {
        if (string.IsNullOrWhiteSpace(vsDefines) && string.IsNullOrWhiteSpace(psDefines))
            return this;

        var key = (StringHash.Calculate(vsDefines ?? string.Empty), StringHash.Calculate(psDefines ?? string.Empty));
        if (cloneTechniques.TryGetValue(key, out var cached))
            return cached;

        var clone = Clone(Name);
        foreach (var pass in clone.GetPasses())
        {
            if (!string.IsNullOrWhiteSpace(vsDefines))
                pass.SetVertexShaderDefines(AppendWithSpace(pass.VertexShaderDefines, vsDefines));
            if (!string.IsNullOrWhiteSpace(psDefines))
                pass.SetPixelShaderDefines(AppendWithSpace(pass.PixelShaderDefines, psDefines));
        }

        cloneTechniques[key] = clone;
        return clone;
    }

    public static uint GetPassIndex(string passName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);
        var lower = passName.ToLowerInvariant();
        if (PassIndices.TryGetValue(lower, out var index))
            return index;

        index = (uint)PassIndices.Count;
        PassIndices[lower] = index;
        return index;
    }

    private static string AppendDefineSuffix(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim() + " ";
    }

    private static string AppendWithSpace(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right.Trim();
        if (string.IsNullOrWhiteSpace(right))
            return left.Trim();
        return left.Trim() + " " + right.Trim();
    }

    private static void CopyPass(Pass destination, Pass source)
    {
        destination.SetBlendMode(source.BlendMode);
        destination.SetCullMode(source.CullMode);
        destination.SetDepthTestMode(source.DepthTestMode);
        destination.SetColorWrite(source.ColorWrite);
        destination.SetDepthWrite(source.DepthWrite);
        destination.SetAlphaToCoverage(source.AlphaToCoverage);
        destination.SetVertexShader(source.VertexShader);
        destination.SetPixelShader(source.PixelShader);
        destination.SetVertexShaderDefines(source.VertexShaderDefines);
        destination.SetPixelShaderDefines(source.PixelShaderDefines);
        destination.SetVertexShaderDefineExcludes(source.VertexShaderDefineExcludes);
        destination.SetPixelShaderDefineExcludes(source.PixelShaderDefineExcludes);
        destination.SetVertexTextureDefines(source.VertexTextureDefines);
        destination.SetPixelTextureDefines(source.PixelTextureDefines);
        destination.MarkShadersLoaded(source.ShadersLoadedFrameNumber);
    }

    private static uint RegisterBuiltInPass(string passName, uint index)
    {
        PassIndices[passName] = index;
        return index;
    }

    private static BlendMode ParseBlendMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "add" => BlendMode.Add,
            "multiply" => BlendMode.Multiply,
            "alpha" => BlendMode.Alpha,
            "addalpha" => BlendMode.AddAlpha,
            "premulalpha" => BlendMode.PremulAlpha,
            "invdestalpha" => BlendMode.InvDestAlpha,
            "subtract" => BlendMode.Subtract,
            "subtractalpha" => BlendMode.SubtractAlpha,
            "decal" => BlendMode.Decal,
            _ => BlendMode.Replace
        };
    }

    private static CompareMode ParseCompareMode(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == "false")
            return CompareMode.Always;

        return normalized switch
        {
            "always" => CompareMode.Always,
            "equal" => CompareMode.Equal,
            "notequal" => CompareMode.NotEqual,
            "less" => CompareMode.Less,
            "lessequal" => CompareMode.LessEqual,
            "greater" => CompareMode.Greater,
            "greaterequal" => CompareMode.GreaterEqual,
            _ => CompareMode.Less
        };
    }

    private static CullMode ParseCullMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "none" => CullMode.None,
            "cw" => CullMode.Clockwise,
            "ccw" => CullMode.CounterClockwise,
            _ => CullMode.Max
        };
    }
}
