using System.Text;
using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

public class RawShader : DeviceObject
{
    private IShader? handle;
    private ShaderBytecode bytecode;

    protected RawShader(ShaderType type)
    {
        bytecode = new ShaderBytecode(type);
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
        if (handle is null || handle.IsNull)
            CreateGPU();
    }

    public override void Destroy()
    {
        base.Destroy();
        DestroyGPU();
    }

    public ShaderBytecode GetBytecode() => new(bytecode);

    public ShaderType GetShaderType() => bytecode.Type;

    public IShader? GetHandle() => handle;

    public void CreateFromBinary(ShaderBytecode bytecode)
    {
        ArgumentNullException.ThrowIfNull(bytecode);
        if (bytecode.Type != this.bytecode.Type)
            throw new InvalidOperationException("Shader bytecode type does not match shader type.");

        this.bytecode = new ShaderBytecode(bytecode);
        CreateGPU();
    }

    protected virtual void CreateGPU()
    {
        DestroyGPU();

        if (bytecode.IsEmpty())
            return;

        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        if (device is null || device.IsNull)
            return;

        using var shaderName = new PinnedUtf8(GetDebugName());
        using var entryPoint = new PinnedUtf8("main");
        using var shaderDesc = new ShaderDesc
        {
            Name = shaderName.Pointer,
            ShaderType = ToNativeShaderType(bytecode.Type),
            UseCombinedTextureSamplers = true
        };
        using var shaderCreateInfo = new ShaderCreateInfo
        {
            Desc = shaderDesc,
            EntryPoint = entryPoint.Pointer,
            LoadConstantBufferReflection = true
        };

        if (string.Equals(bytecode.Mime, "application/glsl", StringComparison.Ordinal))
        {
            using var source = new PinnedUtf8(Encoding.UTF8.GetString(bytecode.Bytecode));
            shaderCreateInfo.Source = source.Pointer;
            shaderCreateInfo.SourceLanguage = ShaderSourceLanguage.ShaderSourceLanguageGlslVerbatim;
            device.CreateShader(shaderCreateInfo, out handle, out var compilerOutput);
            ReleaseIfNotNull(compilerOutput);
        }
        else
        {
            var pinnedBytecode = default(GCHandle);
            try
            {
                pinnedBytecode = GCHandle.Alloc(bytecode.Bytecode, GCHandleType.Pinned);
                shaderCreateInfo.ByteCode = pinnedBytecode.AddrOfPinnedObject();
                shaderCreateInfo.ByteCodeSize = (nuint)bytecode.Bytecode.Length;
                device.CreateShader(shaderCreateInfo, out handle, out var compilerOutput);
                ReleaseIfNotNull(compilerOutput);
            }
            finally
            {
                if (pinnedBytecode.IsAllocated)
                    pinnedBytecode.Free();
            }
        }

        if (handle is { IsNull: true })
            handle = null;
    }

    protected void DestroyGPU()
    {
        ReleaseIfNotNull(handle);
        handle = null;
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
