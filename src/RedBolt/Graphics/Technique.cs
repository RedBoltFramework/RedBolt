using Microsoft.Extensions.Logging;

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
    Decal,
    Max
}

public enum CompareMode
{
    Always,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Max
}

public enum LightingMode
{
    Unlit,
    PerVertex,
    PerPixel
}

/// <summary>Material rendering pass, which defines shaders and render state.</summary>
public class Pass : PipelineStateTracker
{
    /// <summary>Construct.</summary>
    public Pass(string name)
    {
        _blendMode = BlendMode.Replace;
        _cullMode = CullMode.Max;
        _depthTestMode = CompareMode.LessEqual;
        _shadersLoadedFrameNumber = 0;
        _alphaToCoverage = false;
        _colorWrite = true;
        _depthWrite = true;

        _name = name.ToLowerInvariant();
        _index = Technique.GetPassIndex(_name);

        SetPixelTextureDefines(["Albedo", "Normal", "Properties", "Emission"]);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _name = string.Empty;
        _pixelTextureDefines.Clear();
        _vertexTextureDefines.Clear();
        _extraPixelShaders.Clear();
        _extraVertexShaders.Clear();
        _pixelShaders.Clear();
        _vertexShaders.Clear();
        _pixelShaderDefineExcludes = string.Empty;
        _vertexShaderDefineExcludes = string.Empty;
        _pixelShaderDefines = string.Empty;
        _vertexShaderDefines = string.Empty;
        _pixelShaderName = string.Empty;
        _vertexShaderName = string.Empty;
        base.Dispose();
    }

    /// <summary>Set blend mode.</summary>
    public void SetBlendMode(BlendMode mode)
    {
        _blendMode = mode;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set culling mode override. By default culling mode is read from the material instead. Set the illegal culling mode Max to disable override again.</summary>
    public void SetCullMode(CullMode mode)
    {
        _cullMode = mode;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set depth compare mode.</summary>
    public void SetDepthTestMode(CompareMode mode)
    {
        _depthTestMode = mode;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set depth write on/off.</summary>
    public void SetDepthWrite(bool enable)
    {
        _depthWrite = enable;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set color write on/off.</summary>
    public void SetColorWrite(bool enable)
    {
        _colorWrite = enable;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set alpha-to-coverage on/off.</summary>
    public void SetAlphaToCoverage(bool enable)
    {
        _alphaToCoverage = enable;
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set vertex shader name.</summary>
    public void SetVertexShader(string name)
    {
        _vertexShaderName = name;
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set pixel shader name.</summary>
    public void SetPixelShader(string name)
    {
        _pixelShaderName = name;
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set vertex shader defines. Separate multiple defines with spaces.</summary>
    public void SetVertexShaderDefines(string defines)
    {
        _vertexShaderDefines = defines;
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set pixel shader defines. Separate multiple defines with spaces.</summary>
    public void SetPixelShaderDefines(string defines)
    {
        _pixelShaderDefines = defines;
        _isAlphaMask = _pixelShaderDefines.Contains("ALPHAMASK", StringComparison.Ordinal);
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set vertex shader define excludes. Use to mark defines that the shader code will not recognize, to prevent compiling redundant shader variations.</summary>
    public void SetVertexShaderDefineExcludes(string excludes)
    {
        _vertexShaderDefineExcludes = excludes;
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Set pixel shader define excludes. Use to mark defines that the shader code will not recognize, to prevent compiling redundant shader variations.</summary>
    public void SetPixelShaderDefineExcludes(string excludes)
    {
        _pixelShaderDefineExcludes = excludes;
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    public void SetVertexTextureDefines(IEnumerable<string> textures)
    {
        _vertexTextureDefines.Clear();
        _vertexTextureDefines.AddRange(textures);
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    public void SetPixelTextureDefines(IEnumerable<string> textures)
    {
        _pixelTextureDefines.Clear();
        _pixelTextureDefines.AddRange(textures);
        ReleaseShaders();
        MarkPipelineStateHashDirty();
    }

    /// <summary>Reset shader pointers.</summary>
    public void ReleaseShaders()
    {
        _vertexShaders.Clear();
        _pixelShaders.Clear();
        _extraVertexShaders.Clear();
        _extraPixelShaders.Clear();
    }

    /// <summary>Mark shaders loaded this frame.</summary>
    public void MarkShadersLoaded(uint frameNumber)
    {
        _shadersLoadedFrameNumber = frameNumber;
    }

    /// <summary>Return pass name.</summary>
    public string Name => _name;

    /// <summary>Return pass index. This is used for optimal render-time pass queries that avoid map lookups.</summary>
    public int Index => _index;

    /// <summary>Return blend mode.</summary>
    public BlendMode BlendMode => _blendMode;

    /// <summary>Return culling mode override. If pass is not overriding culling mode (default), the illegal mode Max is returned.</summary>
    public CullMode CullMode => _cullMode;

    /// <summary>Return depth compare mode.</summary>
    public CompareMode DepthTestMode => _depthTestMode;

    /// <summary>Return last shaders loaded frame number.</summary>
    public uint ShadersLoadedFrameNumber => _shadersLoadedFrameNumber;

    /// <summary>Return color write mode.</summary>
    public bool ColorWrite => _colorWrite;

    /// <summary>Return depth write mode.</summary>
    public bool DepthWrite => _depthWrite;

    /// <summary>Return alpha-to-coverage mode.</summary>
    public bool AlphaToCoverage => _alphaToCoverage;

    /// <summary>Return whether the pass uses cutout transparency via ALPHAMASK.</summary>
    public bool IsAlphaMask => _isAlphaMask;

    /// <summary>Return vertex shader name.</summary>
    public string VertexShader => _vertexShaderName;

    /// <summary>Return pixel shader name.</summary>
    public string PixelShader => _pixelShaderName;

    /// <summary>Return vertex shader defines.</summary>
    public string VertexShaderDefines => _vertexShaderDefines;

    /// <summary>Return pixel shader defines.</summary>
    public string PixelShaderDefines => _pixelShaderDefines;

    /// <summary>Return vertex shader define excludes.</summary>
    public string VertexShaderDefineExcludes => _vertexShaderDefineExcludes;

    /// <summary>Return pixel shader define excludes.</summary>
    public string PixelShaderDefineExcludes => _pixelShaderDefineExcludes;

    /// <summary>Return vertex shaders.</summary>
    public List<ShaderVariation?> GetVertexShaders() => _vertexShaders;

    /// <summary>Return pixel shaders.</summary>
    public List<ShaderVariation?> GetPixelShaders() => _pixelShaders;

    /// <summary>Return names of textures to be reported as defines in vertex shader code.</summary>
    public IReadOnlyList<string> VertexTextureDefines => _vertexTextureDefines;

    /// <summary>Return names of textures to be reported as defines in pixel shader code.</summary>
    public IReadOnlyList<string> PixelTextureDefines => _pixelTextureDefines;

    /// <summary>Return vertex shaders with extra defines from the renderpath.</summary>
    public List<ShaderVariation?> GetVertexShaders(StringHash extraDefinesHash)
    {
        if (!extraDefinesHash.IsEmpty())
        {
            if (!_extraVertexShaders.TryGetValue(extraDefinesHash, out var shaders))
            {
                shaders = [];
                _extraVertexShaders.Add(extraDefinesHash, shaders);
            }

            return shaders;
        }

        return _vertexShaders;
    }

    /// <summary>Return pixel shaders with extra defines from the renderpath.</summary>
    public List<ShaderVariation?> GetPixelShaders(StringHash extraDefinesHash)
    {
        if (!extraDefinesHash.IsEmpty())
        {
            if (!_extraPixelShaders.TryGetValue(extraDefinesHash, out var shaders))
            {
                shaders = [];
                _extraPixelShaders.Add(extraDefinesHash, shaders);
            }

            return shaders;
        }

        return _pixelShaders;
    }

    /// <summary>Return the effective vertex shader defines, accounting for excludes. Called internally by Renderer.</summary>
    public string GetEffectiveVertexShaderDefines() => FilterDefines(_vertexShaderDefines, _vertexShaderDefineExcludes);

    /// <summary>Return the effective pixel shader defines, accounting for excludes. Called internally by Renderer.</summary>
    public string GetEffectivePixelShaderDefines() => FilterDefines(_pixelShaderDefines, _pixelShaderDefineExcludes);

    private static string FilterDefines(string defines, string excludes)
    {
        if (excludes.Length == 0)
            return defines;

        var defineSet = defines.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var excludeSet = excludes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < excludeSet.Length; ++i)
            defineSet.Remove(excludeSet[i]);

        return string.Join(' ', defineSet);
    }

    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, (uint)_blendMode);
        CombineHash(ref hash, (uint)_cullMode);
        CombineHash(ref hash, (uint)_depthTestMode);
        CombineHash(ref hash, _colorWrite ? 1u : 0u);
        CombineHash(ref hash, _depthWrite ? 1u : 0u);
        CombineHash(ref hash, _alphaToCoverage ? 1u : 0u);
        CombineHash(ref hash, StringHash.Calculate(_vertexShaderName));
        CombineHash(ref hash, StringHash.Calculate(_pixelShaderName));
        CombineHash(ref hash, StringHash.Calculate(_vertexShaderDefines));
        CombineHash(ref hash, StringHash.Calculate(_pixelShaderDefines));
        CombineHash(ref hash, StringHash.Calculate(_vertexShaderDefineExcludes));
        CombineHash(ref hash, StringHash.Calculate(_pixelShaderDefineExcludes));
        CombineHash(ref hash, MakeHash(_vertexTextureDefines));
        CombineHash(ref hash, MakeHash(_pixelTextureDefines));
        return hash;
    }

    private static uint MakeHash(IReadOnlyList<string> values)
    {
        var hash = 0u;
        for (var i = 0; i < values.Count; ++i)
            CombineHash(ref hash, StringHash.Calculate(values[i]));
        return hash;
    }

    private static void CombineHash(ref uint hash, uint value)
    {
        hash ^= unchecked(value + 0x9e3779b9u + (hash << 6) + (hash >> 2));
    }

    private int _index;
    private BlendMode _blendMode;
    private CullMode _cullMode;
    private CompareMode _depthTestMode;
    private uint _shadersLoadedFrameNumber;
    private bool _colorWrite;
    private bool _depthWrite;
    private bool _alphaToCoverage;
    private bool _isAlphaMask;
    private string _vertexShaderName = string.Empty;
    private string _pixelShaderName = string.Empty;
    private string _vertexShaderDefines = string.Empty;
    private string _pixelShaderDefines = string.Empty;
    private string _vertexShaderDefineExcludes = string.Empty;
    private string _pixelShaderDefineExcludes = string.Empty;
    private readonly List<ShaderVariation?> _vertexShaders = [];
    private readonly List<ShaderVariation?> _pixelShaders = [];
    private readonly Dictionary<StringHash, List<ShaderVariation?>> _extraVertexShaders = [];
    private readonly Dictionary<StringHash, List<ShaderVariation?>> _extraPixelShaders = [];
    private readonly List<string> _vertexTextureDefines = [];
    private readonly List<string> _pixelTextureDefines = [];
    private string _name = string.Empty;
}

/// <summary>Material technique. Consists of several passes.</summary>
public class Technique : Resource
{
    /// <summary>Construct.</summary>
    public Technique(string name)
        : base(name)
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        foreach (var clone in _cloneTechniques.Values)
            clone.Dispose();
        _cloneTechniques.Clear();
        for (var i = _passes.Count - 1; i >= 0; --i)
            _passes[i]?.Dispose();
        _passes.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Technique>();
        reflection.SetObjectFactory(static () => new Technique(string.Empty));
    }

    /// <summary>Index for base pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int BasePassIndex;
    /// <summary>Index for alpha pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int AlphaPassIndex;
    /// <summary>Index for prepass material pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int MaterialPassIndex;
    /// <summary>Index for deferred G-buffer pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int DeferredPassIndex;
    /// <summary>Index for per-pixel light pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int LightPassIndex;
    /// <summary>Index for lit base pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int LitBasePassIndex;
    /// <summary>Index for lit alpha pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int LitAlphaPassIndex;
    /// <summary>Index for shadow pass. Initialized once GetPassIndex() has been called for the first time.</summary>
    public static int ShadowPassIndex;

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        for (var i = _passes.Count - 1; i >= 0; --i)
            _passes[i]?.Dispose();
        _passes.Clear();
        foreach (var clone in _cloneTechniques.Values)
            clone.Dispose();
        _cloneTechniques.Clear();
        MemoryUse = 0;

        using var xml = new XMLFile(Name);
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
            {
                Log.GetLogger().LogError("Missing pass name");
                continue;
            }

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

            IReadOnlyList<string> passVsTextures = [];
            IReadOnlyList<string> passPsTextures = [];
            if (pass.Index != ShadowPassIndex)
                passPsTextures = ["Albedo", "Normal", "Properties", "Emission"];

            if (passElement.HasAttribute("vstextures"))
                passVsTextures = passElement.GetAttribute("vstextures").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (passElement.HasAttribute("pstextures"))
                passPsTextures = passElement.GetAttribute("pstextures").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            pass.SetVertexTextureDefines(passVsTextures);
            pass.SetPixelTextureDefines(passPsTextures);

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

        MemoryUse = _passes.Count * 128;
        return true;
    }

    /// <summary>Create a new pass.</summary>
    public Pass CreatePass(string name)
    {
        var existing = GetPass(name);
        if (existing is not null)
            return existing;

        var pass = new Pass(name);
        if (pass.Index >= _passes.Count)
        {
            while (_passes.Count <= pass.Index)
                _passes.Add(null);
        }

        _passes[pass.Index] = pass;
        MemoryUse = checked((int)GetNumPasses() * 128);
        return pass;
    }

    /// <summary>Remove a pass.</summary>
    public void RemovePass(string name)
    {
        if (!PassIndices.TryGetValue(name.ToLowerInvariant(), out var index))
            return;
        if (index < _passes.Count && _passes[(int)index] is not null)
        {
            _passes[(int)index]!.Dispose();
            _passes[(int)index] = null;
            MemoryUse = checked((int)GetNumPasses() * 128);
        }
    }

    /// <summary>Reset shader pointers in all passes.</summary>
    public void ReleaseShaders()
    {
        foreach (var pass in _passes)
            pass?.ReleaseShaders();
    }

    /// <summary>Clone the technique. Passes will be deep copied to allow independent modification.</summary>
    public Technique Clone(string cloneName = "")
    {
        var clone = new Technique(cloneName);
        foreach (var pass in _passes)
        {
            if (pass is null)
                continue;

            var clonedPass = clone.CreatePass(pass.Name);
            clonedPass.SetCullMode(pass.CullMode);
            clonedPass.SetBlendMode(pass.BlendMode);
            clonedPass.SetDepthTestMode(pass.DepthTestMode);
            clonedPass.SetColorWrite(pass.ColorWrite);
            clonedPass.SetDepthWrite(pass.DepthWrite);
            clonedPass.SetAlphaToCoverage(pass.AlphaToCoverage);
            clonedPass.SetVertexShader(pass.VertexShader);
            clonedPass.SetPixelShader(pass.PixelShader);
            clonedPass.SetVertexShaderDefines(pass.VertexShaderDefines);
            clonedPass.SetPixelShaderDefines(pass.PixelShaderDefines);
            clonedPass.SetVertexShaderDefineExcludes(pass.VertexShaderDefineExcludes);
            clonedPass.SetPixelShaderDefineExcludes(pass.PixelShaderDefineExcludes);
            clonedPass.SetVertexTextureDefines(pass.VertexTextureDefines);
            clonedPass.SetPixelTextureDefines(pass.PixelTextureDefines);
        }

        return clone;
    }

    /// <summary>Return whether has a pass.</summary>
    public bool HasPass(int passIndex) => passIndex >= 0 && passIndex < _passes.Count && _passes[passIndex] is not null;

    /// <summary>Return whether has a pass by name. This overload should not be called in time-critical rendering loops; use a pre-acquired pass index instead.</summary>
    public bool HasPass(string name) => GetPass(name) is not null;

    /// <summary>Return a pass, or null if not found.</summary>
    public Pass? GetPass(int passIndex) => passIndex >= 0 && passIndex < _passes.Count ? _passes[passIndex] : null;

    /// <summary>Return a pass by name, or null if not found. This overload should not be called in time-critical rendering loops; use a pre-acquired pass index instead.</summary>
    public Pass? GetPass(string name)
    {
        return PassIndices.TryGetValue(name.ToLowerInvariant(), out var index) ? GetPass(index) : null;
    }

    /// <summary>Return number of passes.</summary>
    public int GetNumPasses()
    {
        var count = 0;
        foreach (var pass in _passes)
        {
            if (pass is not null)
                ++count;
        }

        return count;
    }

    /// <summary>Return all pass names.</summary>
    public IReadOnlyList<string> GetPassNames()
    {
        var result = new List<string>();
        foreach (var pass in _passes)
        {
            if (pass is not null)
                result.Add(pass.Name);
        }

        return result;
    }

    /// <summary>Return all passes.</summary>
    public IReadOnlyList<Pass> GetPasses()
    {
        var result = new List<Pass>();
        foreach (var pass in _passes)
        {
            if (pass is not null)
                result.Add(pass);
        }

        return result;
    }

    /// <summary>Return a clone with added shader compilation defines. Called internally by Material.</summary>
    public Technique CloneWithDefines(string vsDefines, string psDefines)
    {
        if (vsDefines.Length == 0 && psDefines.Length == 0)
            return this;

        var key = (new StringHash(vsDefines), new StringHash(psDefines));
        if (_cloneTechniques.TryGetValue(key, out var cached))
            return cached;

        var clone = Clone(Name);
        foreach (var pass in clone.GetPasses())
        {
            if (vsDefines.Length != 0)
                pass.SetVertexShaderDefines(pass.VertexShaderDefines + " " + vsDefines);
            if (psDefines.Length != 0)
                pass.SetPixelShaderDefines(pass.PixelShaderDefines + " " + psDefines);
        }

        _cloneTechniques[key] = clone;
        return clone;
    }

    /// <summary>Return a pass type index by name. Allocate new if not used yet.</summary>
    public static int GetPassIndex(string passName)
    {
        // C++ Technique::GetPassIndex returns M_MAX_UNSIGNED for empty pass names, mapped to int.MaxValue
        // (used by ScenePass constructors for unused deferred/light slots).
        if (string.IsNullOrEmpty(passName))
            return int.MaxValue;

        if (PassIndices.Count == 0)
        {
            BasePassIndex = PassIndices["base"] = 0;
            AlphaPassIndex = PassIndices["alpha"] = 1;
            MaterialPassIndex = PassIndices["material"] = 2;
            DeferredPassIndex = PassIndices["deferred"] = 3;
            LightPassIndex = PassIndices["light"] = 4;
            LitBasePassIndex = PassIndices["litbase"] = 5;
            LitAlphaPassIndex = PassIndices["litalpha"] = 6;
            ShadowPassIndex = PassIndices["shadow"] = 7;
        }

        var lower = passName.ToLowerInvariant();
        if (PassIndices.TryGetValue(lower, out var index))
            return index;

        index = PassIndices.Count;
        PassIndices[lower] = index;
        return index;
    }

    private static string AppendDefineSuffix(string value)
    {
        return value.Length == 0 ? string.Empty : value + " ";
    }

    private static BlendMode ParseBlendMode(string value)
    {
        return value.ToLowerInvariant() switch
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
        var normalized = value.ToLowerInvariant();
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
        return value.ToLowerInvariant() switch
        {
            "none" => CullMode.None,
            "cw" => CullMode.Clockwise,
            "ccw" => CullMode.CounterClockwise,
            _ => CullMode.Max
        };
    }

    private readonly List<Pass?> _passes = [];
    private readonly Dictionary<(StringHash VertexDefines, StringHash PixelDefines), Technique> _cloneTechniques = new();
    private static readonly Dictionary<string, int> PassIndices = new(StringComparer.Ordinal);
}
