using RedBolt.Core;
using RedBolt.IO;
namespace RedBolt.Graphics;
using FileMode = global::RedBolt.IO.FileMode;


public static class ShaderSourceLogger
{
    public static void LogShaderSource(in FileIdentifier fileName, string defines, string source)
    {
        var context = Context.Instance;
        var graphics = context.GetSubsystem<GraphicsSystem>()!;
        if (!graphics.GetSettings().LogShaderSources)
            return;
        var virtualFileSystem = context.GetSubsystem<VirtualFileSystem>()!;
        var sourceFile = virtualFileSystem.OpenFile(fileName, FileMode.Write);
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
