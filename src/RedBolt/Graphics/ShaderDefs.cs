// C++: matches C++ within allowed deviations (SpirVShader/ShaderDefineArray are struct-based wrappers for SPIR-V cross-compilation; no direct C++ counterpart file).
namespace RedBolt;

/// <summary>
/// SPIR-V shader.
/// </summary>
public struct SpirVShader
{
    public List<uint> Bytecode = new();
    public string CompilerOutput = string.Empty;

    public SpirVShader()
    {
    }

    public bool IsValid() => Bytecode.Count != 0;

    public static implicit operator bool(SpirVShader value) => value.IsValid();
}

/// <summary>
/// List of supported target shader languages.
/// </summary>
public enum TargetShaderLanguage
{
    Glsl41,
    GlslEs30,
    Hlsl50,
    Vulkan10
}

/// <summary>
/// Array of shader defines: pairs of name and value.
/// </summary>
public struct ShaderDefineArray
{
    /// <summary>
    /// Vector of defines.
    /// </summary>
    public List<(string Name, string Value)> Defines = new();

    /// <summary>
    /// Construct default.
    /// </summary>
    public ShaderDefineArray()
    {
    }

    /// <summary>
    /// Construct from string.
    /// </summary>
    public ShaderDefineArray(string defineString)
    {
        var items = defineString.Split(' ');
        Defines.Capacity = items.Length;
        foreach (var item in items)
        {
            var equalsPos = item.IndexOf('=');
            if (equalsPos >= 0)
                Defines.Add((item[..equalsPos], item[(equalsPos + 1)..]));
            else
                Defines.Add((item, "1"));
        }
    }

    /// <summary>
    /// Append define without value.
    /// </summary>
    public void Append(string define) => Defines.Add((define, "1"));

    /// <summary>
    /// Append define with value.
    /// </summary>
    public void Append(string define, string value) => Defines.Add((define, value));

    /// <summary>
    /// Return size.
    /// </summary>
    public uint Size() => (uint)Defines.Count;

    /// <summary>
    /// Find defines unused in source code.
    /// </summary>
    public List<string> FindUnused(string code)
    {
        var unusedDefines = new List<string>();
        foreach (var define in Defines)
        {
            if (!code.Contains(define.Name, StringComparison.Ordinal))
                unusedDefines.Add(define.Name);
        }
        return unusedDefines;
    }
}
