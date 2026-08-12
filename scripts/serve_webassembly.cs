#:sdk Microsoft.NET.Sdk.Web

using Microsoft.Extensions.FileProviders;
using System.Runtime.CompilerServices;

var directory = "";
var port = 8000;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--directory" when index + 1 < args.Length:
            directory = args[++index];
            break;
        case "--port" when index + 1 < args.Length && int.TryParse(args[++index], out port):
            break;
        case "-h" or "--help":
            Console.WriteLine("Usage: dotnet run --file scripts/serve_webassembly.cs -- --directory PATH [--port PORT]");
            return;
        default:
            Console.Error.WriteLine("Usage: dotnet run --file scripts/serve_webassembly.cs -- --directory PATH [--port PORT]");
            Environment.ExitCode = 2;
            return;
    }
}

if (directory.Length == 0)
{
    Console.Error.WriteLine("--directory is required.");
    Environment.ExitCode = 2;
    return;
}
var repository = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, ".."));
directory = Path.GetFullPath(directory, repository);
if (!Directory.Exists(directory) || port is < 1 or > 65535)
{
    Console.Error.WriteLine(!Directory.Exists(directory)
        ? $"Directory not found: {directory}"
        : $"Invalid port: {port}");
    Environment.ExitCode = 2;
    return;
}

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
var files = new FileServerOptions
{
    FileProvider = new PhysicalFileProvider(directory),
    EnableDirectoryBrowsing = true
};
files.StaticFileOptions.ServeUnknownFileTypes = true;
app.UseFileServer(files);
Console.WriteLine($"Serving RedBolt WebAssembly at http://127.0.0.1:{port}");
await app.RunAsync();

static string SourcePath([CallerFilePath] string path = "") => path;
