using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

/// <summary>Base class for GPU shader. It should be kept alive as long as the shader is used by any
/// pipeline state. Shader handle is null if shader is not loaded or failed to load.</summary>
public class RawShader : DeviceObject
{
    private IShader? _handle;
    private ShaderBytecode _bytecode;

    public event Action? Reloaded;

    protected RawShader(ShaderType type)
    {
        _bytecode = new ShaderBytecode(type);
    }

    public RawShader(ShaderBytecode bytecode)
        : this(bytecode?.Type ?? throw new ArgumentNullException(nameof(bytecode)))
    {
        CreateFromBinary(bytecode);
    }

    public override void Invalidate()
    {
        base.Invalidate();
        DestroyGPU();
    }

    public override void Restore()
    {
        base.Restore();
        if (_handle is null || _handle.IsNull)
            CreateGPU();
    }

    public override void Destroy()
    {
        base.Destroy();
        DestroyGPU();
    }

    public ShaderBytecode GetBytecode() => new(_bytecode);

    public ShaderType GetShaderType() => _bytecode.Type;

    public IShader? GetHandle() => _handle;

    /// <summary>Create shader from platform-specific binary.</summary>
    public void CreateFromBinary(ShaderBytecode bytecode)
    {
        ArgumentNullException.ThrowIfNull(bytecode);
        if (bytecode.Type != this._bytecode.Type)
            throw new InvalidOperationException("Shader bytecode type does not match shader type.");

        this._bytecode = new ShaderBytecode(bytecode);
        CreateGPU();
        Reloaded?.Invoke();
    }

    protected virtual void CreateGPU()
    {
        DestroyGPU();

        if (_bytecode.IsEmpty())
            return;

        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        if (device is null || device.IsNull)
            return;

        using var shaderName = new CString(GetDebugName());
        using var entryPoint = new CString("main");
        using var shaderDesc = new ShaderDesc
        {
            Name = shaderName.Pointer,
            ShaderType = ToNativeShaderType(_bytecode.Type),
            UseCombinedTextureSamplers = true
        };
        using var shaderCreateInfo = new ShaderCreateInfo
        {
            Desc = shaderDesc,
            EntryPoint = entryPoint.Pointer,
            LoadConstantBufferReflection = true
        };

        // C++ RawShader::CreateGPU selects source vs bytecode by RenderBackend, not MIME.
        var backend = GetSubsystem<RenderDevice>()?.GetBackend() ?? RenderBackend.OpenGL;
        if (backend == RenderBackend.OpenGL
            || string.Equals(_bytecode.Mime, "application/glsl", StringComparison.Ordinal))
        {
            using var source = new CString(Encoding.UTF8.GetString(_bytecode.Bytecode));
            shaderCreateInfo.Source = source.Pointer;
            shaderCreateInfo.SourceLanguage = ShaderSourceLanguage.ShaderSourceLanguageGlslVerbatim;
            device.CreateShader(shaderCreateInfo, out _handle, out var compilerOutput);
            ReleaseIfNotNull(compilerOutput);
        }
        else
        {
            var pinnedBytecode = default(GCHandle);
            try
            {
                pinnedBytecode = GCHandle.Alloc(_bytecode.Bytecode, GCHandleType.Pinned);
                shaderCreateInfo.ByteCode = pinnedBytecode.AddrOfPinnedObject();
                shaderCreateInfo.ByteCodeSize = (nuint)_bytecode.Bytecode.Length;
                device.CreateShader(shaderCreateInfo, out _handle, out var compilerOutput);
                ReleaseIfNotNull(compilerOutput);
            }
            finally
            {
                if (pinnedBytecode.IsAllocated)
                    pinnedBytecode.Free();
            }
        }

        if (_handle is null || _handle.IsNull)
        {
            _handle = null;
            Log.Logger.LogError("Failed to create shader '{Name}'", GetDebugName());
        }
    }

    protected void DestroyGPU()
    {
        ReleaseIfNotNull(_handle);
        _handle = null;
    }

    private static RedBolt.Bindings.Diligent.ShaderType ToNativeShaderType(ShaderType shaderType) => shaderType switch
    {
        ShaderType.VS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
        ShaderType.PS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
        ShaderType.GS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeGeometry,
        ShaderType.HS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeHull,
        ShaderType.DS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeDomain,
        ShaderType.CS => RedBolt.Bindings.Diligent.ShaderType.ShaderTypeCompute,
        _ => throw new ArgumentOutOfRangeException(nameof(shaderType), shaderType, null)
    };

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }
}
