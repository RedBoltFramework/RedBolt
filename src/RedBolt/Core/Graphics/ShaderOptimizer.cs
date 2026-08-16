// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.SpirVTools;

public static class ShaderOptimizer
{
    [ThreadStatic]
    private static StringBuilder? _optimizerOutput;

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
        var output = new StringBuilder();
        _optimizerOutput = output;

        try
        {
            var result = OptimizeSpirVShader(CollectionsMarshal.AsSpan(shader.Bytecode), targetEnv, isVulkan,
                out var optimizedBytecode);
            if (!result)
                return !isVulkan;

            shader.Bytecode = optimizedBytecode;
            return true;
        }
        finally
        {
            _optimizerOutput = null;
            optimizerOutput += output;
        }
    }

    private static unsafe bool OptimizeSpirVShader(ReadOnlySpan<uint> bytecode, SpvTargetEnv targetEnv,
        bool isVulkan, out List<uint> optimizedBytecode)
    {
        optimizedBytecode = [];
        var optimizer = SpirVTools.SpvOptimizerCreate(targetEnv);
        var options = SpirVTools.SpvOptimizerOptionsCreate();
        SpvBinaryT? optimizedBinary = null;

        try
        {
            SpirVTools.SpvOptimizerSetMessageConsumer(
                optimizer,
                (nint)(delegate* unmanaged[Cdecl]<SpvMessageLevelT, nint, nint, nint, void>)&AppendOptimizerMessage);

            if (isVulkan)
                SpirVTools.SpvOptimizerRegisterLegalizationPasses(optimizer);

            ReadOnlySpan<byte> optimizationFlag = "-O\0"u8;
            fixed (byte* optimizationFlagPointer = optimizationFlag)
            {
                ReadOnlySpan<nint> optimizationFlags = [(nint)optimizationFlagPointer];
                if (!SpirVTools.SpvOptimizerRegisterPassesFromFlagsWhilePreservingTheInterface(
                        optimizer, optimizationFlags, (nuint)optimizationFlags.Length))
                {
                    return false;
                }
            }

            SpirVTools.SpvOptimizerOptionsSetRunValidator(options, !isVulkan);
            if (SpirVTools.SpvOptimizerRun(
                    optimizer, bytecode, (nuint)bytecode.Length, out optimizedBinary, options) !=
                SpvResultT.SpvSuccess)
            {
                return false;
            }

            if (isVulkan && !ValidateSpirV(optimizedBinary, targetEnv))
                return false;

            var optimizedSize = checked((int)optimizedBinary.WordCount);
            optimizedBytecode = new List<uint>(optimizedSize);
            var words = (uint*)optimizedBinary.Code;
            for (var i = 0; i < optimizedSize; ++i)
                optimizedBytecode.Add(words[i]);
            return true;
        }
        finally
        {
            if (optimizedBinary is not null)
            {
                if (!optimizedBinary.IsNull)
                    SpirVTools.SpvBinaryDestroy(optimizedBinary);
                optimizedBinary.Dispose();
            }

            SpirVTools.SpvOptimizerOptionsDestroy(options);
            options.Dispose();
            SpirVTools.SpvOptimizerDestroy(optimizer);
            optimizer.Dispose();
        }
    }

    private static bool ValidateSpirV(SpvBinaryT binary, SpvTargetEnv targetEnv)
    {
        var context = SpirVTools.SpvContextCreate(targetEnv);
        SpvDiagnosticT? diagnostic = null;

        try
        {
            unsafe
            {
                var words = new ReadOnlySpan<uint>((void*)binary.Code, checked((int)binary.WordCount));
                if (SpirVTools.SpvValidateBinary(
                        context, words, (nuint)words.Length, out diagnostic) == SpvResultT.SpvSuccess)
                {
                    return true;
                }
            }

            if (diagnostic is not null && !diagnostic.IsNull)
                AppendError(Marshal.PtrToStringUTF8(diagnostic.Error));
            return false;
        }
        finally
        {
            if (diagnostic is not null)
            {
                if (!diagnostic.IsNull)
                    SpirVTools.SpvDiagnosticDestroy(diagnostic);
                diagnostic.Dispose();
            }

            SpirVTools.SpvContextDestroy(context);
            context.Dispose();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void AppendOptimizerMessage(SpvMessageLevelT level, nint source, nint position, nint message)
    {
        if (level <= SpvMessageLevelT.SpvMsgError)
            AppendError(Marshal.PtrToStringUTF8(message));
    }

    private static void AppendError(string? message)
    {
        _optimizerOutput?.Append("Failed to optimize SPIR-V shader: ").Append(message).Append('\n');
    }
}
