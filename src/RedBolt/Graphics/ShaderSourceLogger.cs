namespace RedBolt;

public static class ShaderSourceLogger
{
    public static void LogShaderSource(in FileIdentifier fileName, string defines, string source)
    {
        var context = Context.Instance;
        // TODO: port: auto graphics = context->GetSubsystem<Graphics>();
        // TODO: port: if (!graphics->GetSettings().logShaderSources_)
        var engine = context.GetSubsystem<Engine>()!;
        if (!engine.Parameters.ShaderLogSources)
            return;
        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var sourceFile = virtualFileSystem.OpenFile(fileName, File.FileMode.Write);
        if (sourceFile is not null)
        {
            try
            {
                var header = $"// {defines}\n";
                sourceFile.Write(System.Text.Encoding.UTF8.GetBytes(header));
                sourceFile.Write(System.Text.Encoding.UTF8.GetBytes(source));
            }
            finally
            {
                sourceFile.Close();
            }
        }
    }
}
