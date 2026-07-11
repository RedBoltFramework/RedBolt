namespace RedBolt;

public static class ShaderSourceLogger
{
    public static void LogShaderSource(FileIdentifier fileName, string defines, string source)
    {
        var context = Context.Instance;
        var engine = context.GetSubsystem<Engine>()!;
        if (!engine.Parameters.ShaderLogSources)
            return;

        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var sourceFile = virtualFileSystem.OpenFile(fileName, File.FileMode.Write);
        if (sourceFile is not null)
        {
            var header = $"// {defines}\n";
            sourceFile.Write(System.Text.Encoding.UTF8.GetBytes(header));
            sourceFile.Write(System.Text.Encoding.UTF8.GetBytes(source));
            sourceFile.Close();
        }
    }
}
