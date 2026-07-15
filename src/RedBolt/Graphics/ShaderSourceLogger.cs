// C++: matches C++ within allowed deviations (C# reads Engine.Parameters.ShaderLogSources instead of Graphics::GetSettings().logShaderSources_).
namespace RedBolt;

public static class ShaderSourceLogger
{
    // C++: reads `graphics->GetSettings().logShaderSources_` from Graphics subsystem.
    // C#: reads from Engine.Parameters.ShaderLogSources.
    // C++ header: URHO3D_API void LogShaderSource(const FileIdentifier& fileName, ea::string_view defines, ea::string_view source);
    public static void LogShaderSource(FileIdentifier fileName, string defines, string source)
    {
        // C++: auto graphics = context->GetSubsystem<Graphics>();
        // C++: if (!graphics->GetSettings().logShaderSources_)
        var context = Context.Instance;
        var engine = context.GetSubsystem<Engine>()!;
        if (!engine.Parameters.ShaderLogSources)
            return;
        // C++: sourceFile->Write(header.data(), header.size());
        // C++: sourceFile->Write(source.data(), static_cast<unsigned>(source.size()));
        // C++ source file is implicitly closed (smart pointer). C# calls Close().
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
