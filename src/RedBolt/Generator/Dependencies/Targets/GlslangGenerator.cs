namespace RedBolt.Generator.Dependencies;

using SharpCpp.Model.Ast;

internal sealed class GlslangGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "glslang",
        "RedBolt.Bindings.Glslang",
        "RedBolt.Bindings.Glslang",
        "GlslangNativeBindings",
        nativeLibraryName,
        "redbolt_glslang",
        "-xc",
        "-std=c17",
        false)
{
    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            if (compilation.Members[index] is not FunctionBinding
                {
                    NativeName: "glslang_shader_set_resource_set_binding"
                })
            {
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }
        compilation.InvalidateCaches();
        return prunedCount;
    }
}
