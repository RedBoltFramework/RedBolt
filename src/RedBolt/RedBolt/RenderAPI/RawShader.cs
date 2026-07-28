namespace RedBolt.RenderAPI;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Logging;

/// <summary>Base class for GPU shader. It should be kept alive as long as the shader is used by any
/// pipeline state. Shader handle is null if shader is not loaded or failed to load.</summary>
public class RawShader : DeviceObject
{
    public event Action? OnReloaded;

    public RawShader(ShaderBytecode bytecode)
        : this(bytecode.Type)
    {
        CreateFromBinary((ShaderBytecode)bytecode.Clone());
    }

    public override void Dispose()
    {
        DestroyGPU();
        base.Dispose();
    }

    public override void Invalidate()
    {
        DestroyGPU();
    }

    public override void Restore()
    {
        if (_handle is null || _handle.IsNull)
            CreateGPU();
    }

    public override void Destroy()
    {
        DestroyGPU();
    }

    public ShaderBytecode GetBytecode() => (ShaderBytecode)_bytecode.Clone();

    public ShaderType GetShaderType() => _bytecode.Type;

    public IShader? GetHandle() => _handle;

    protected RawShader(ShaderType type)
    {
        _bytecode = new ShaderBytecode(type);
    }

    /// <summary>Create shader from platform-specific binary.</summary>
    protected void CreateFromBinary(ShaderBytecode bytecode)
    {
        System.Diagnostics.Debug.Assert(bytecode.Type == _bytecode.Type, "Shader bytecode type does not match shader type.");

        bytecode = (ShaderBytecode)bytecode.Clone();
        _bytecode = bytecode;
        CreateGPU();
        OnReloaded?.Invoke();
    }

    private void CreateGPU()
    {
        DestroyGPU();

        if (_bytecode.IsEmpty())
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

        var pinnedBytecode = default(GCHandle);
        try
        {
            pinnedBytecode = GCHandle.Alloc(_bytecode.Bytecode, GCHandleType.Pinned);
            switch (_renderDevice!.GetBackend())
            {
                case RenderBackend.D3D11:
                case RenderBackend.D3D12:
                case RenderBackend.Vulkan:
                    shaderCreateInfo.ByteCode = pinnedBytecode.AddrOfPinnedObject();
                    shaderCreateInfo.ByteCodeSize = checked((nuint)_bytecode.Bytecode.Length);
                    break;

                case RenderBackend.OpenGL:
                    shaderCreateInfo.Source = pinnedBytecode.AddrOfPinnedObject();
                    shaderCreateInfo.SourceLength = checked((nuint)_bytecode.Bytecode.Length);
                    shaderCreateInfo.SourceLanguage = ShaderSourceLanguage.ShaderSourceLanguageGlslVerbatim;
                    break;

                default:
                    System.Diagnostics.Debug.Fail("Not implemented");
                    return;
            }

            _renderDevice.GetRenderDevice()!.CreateShader(shaderCreateInfo, out _handle, out var compilerOutput);
            ReleaseIfNotNull(compilerOutput);
        }
        finally
        {
            if (pinnedBytecode.IsAllocated)
                pinnedBytecode.Free();
        }

        if (_handle is null || _handle.IsNull)
        {
            _handle = null;
            Log.GetLogger().LogError("Failed to create shader '{Name}'", GetDebugName());
        }
    }

    private void DestroyGPU()
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

    private ShaderBytecode _bytecode;
    private IShader? _handle;
}
