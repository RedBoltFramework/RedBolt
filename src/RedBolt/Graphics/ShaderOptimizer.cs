using System.Runtime.InteropServices;
using System.Text;
using RedBolt.Bindings.SpirVTools;
using RedBoltNative;

namespace RedBolt;

public static class ShaderOptimizer
{
    private static SpvTargetEnv GetTarget(TargetShaderLanguage targetLanguage)
    {
        return targetLanguage switch
        {
            TargetShaderLanguage.Vulkan10 => SpvTargetEnv.SpvEnvVulkan10,
            _ => SpvTargetEnv.SpvEnvUniversal16
        };
    }

    public static bool OptimizeSpirVShader(ref SpirVShader shader, ref string optimizerOutput,
        TargetShaderLanguage targetLanguage)
    {
        var isVulkan = targetLanguage == TargetShaderLanguage.Vulkan10;
        var targetEnv = GetTarget(targetLanguage);

        using var spirvOptimizer = new SpirVOptimizerSupport();
        var bytecode = CollectionsMarshal.AsSpan(shader.Bytecode);
        uint emptyBytecode = 0;
        ref var firstWord = ref (bytecode.IsEmpty ? ref emptyBytecode : ref MemoryMarshal.GetReference(bytecode));
        var result = spirvOptimizer.Optimize(in firstWord, (nuint)bytecode.Length, (uint)targetEnv, isVulkan);
        optimizerOutput += Encoding.UTF8.GetString(spirvOptimizer.GetOutput());

        if (!result)
            return !isVulkan;

        var optimizedSize = spirvOptimizer.GetBytecodeSize();
        var optimizedBytecode = new List<uint>(checked((int)optimizedSize));
        for (nuint i = 0; i < optimizedSize; ++i)
            optimizedBytecode.Add(spirvOptimizer.GetBytecodeWord(i));
        shader.Bytecode = optimizedBytecode;

        return true;
    }
}
