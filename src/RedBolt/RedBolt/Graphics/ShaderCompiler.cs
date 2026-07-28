namespace RedBolt.Graphics;

using System.Runtime.InteropServices;
using System.Text;

using RedBoltNative;

using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

public static class ShaderCompiler
{
    public static bool CompileHLSLToBinary(out byte[] bytecode, out string compilerOutput,
        string sourceCode, ShaderType type)
    {
        bytecode = [];
        compilerOutput = string.Empty;

        var sourceCodePointer = Marshal.StringToCoTaskMemUTF8(sourceCode);
        try
        {
            using var compiler = new ShaderCompilerSupport();
#if DEBUG
            const bool debug = true;
#else
            const bool debug = false;
#endif
            var result = compiler.Compile(sourceCodePointer, (nuint)Encoding.UTF8.GetByteCount(sourceCode),
                (uint)type, debug);

            compilerOutput = Encoding.UTF8.GetString(compiler.GetOutput());
            var bytecodeSize = compiler.GetBytecodeSize();
            bytecode = new byte[checked((int)bytecodeSize)];
            for (nuint i = 0; i < bytecodeSize; ++i)
                bytecode[i] = compiler.GetBytecodeByte(i);

            return result;
        }
        finally
        {
            Marshal.FreeCoTaskMem(sourceCodePointer);
        }
    }
}
