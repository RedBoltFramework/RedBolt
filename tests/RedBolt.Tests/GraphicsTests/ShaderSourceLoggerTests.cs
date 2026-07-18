namespace RedBolt.Tests;

public sealed class ShaderSourceLoggerTests
{
    [Fact]
    public void Shader_source_logger_writes_defines_header_and_source()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        var engine = new Engine();
        Context.SetInstance(context);

        var fileName = Path.Combine(Path.GetTempPath(), $"RedBoltShaderSource-{Guid.NewGuid():N}.glsl");
        try
        {
            context.InitializeEngineSubsystems(engine);
            engine.Parameters.ShaderLogSources = true;
            var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
            var mountPoint = virtualFileSystem.MountRoot();
            try
            {
                ShaderSourceLogger.LogShaderSource(
                    new FileIdentifier("file", fileName), "DEFINE=1", "void main() {}\n");
            }
            finally
            {
                virtualFileSystem.Unmount(mountPoint);
            }

            Assert.Equal("// DEFINE=1\nvoid main() {}\n", System.IO.File.ReadAllText(fileName));
        }
        finally
        {
            System.IO.File.Delete(fileName);
            context.Reset();
            Context.SetInstance(previousContext);
        }
    }

    [Fact]
    public void Function_container_and_borrowed_identifier_match_cpp_shape()
    {
        Assert.True(typeof(ShaderSourceLogger).IsAbstract);
        Assert.True(typeof(ShaderSourceLogger).IsSealed);

        var method = typeof(ShaderSourceLogger).GetMethod(nameof(ShaderSourceLogger.LogShaderSource));
        Assert.NotNull(method);
        var fileName = method.GetParameters()[0];
        Assert.True(fileName.ParameterType.IsByRef);
        Assert.True(fileName.IsIn);
    }
}
