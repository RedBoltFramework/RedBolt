namespace SharpCpp.Rendering;

public sealed class GeneratedBindingSet
{
    public GeneratedBindingSet(IReadOnlyList<GeneratedBindingFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Files = files;
    }

    public IReadOnlyList<GeneratedBindingFile> Files { get; }

    public void WriteToDirectory(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var desiredRelativePaths = Files
            .Select(static file => file.RelativePath.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        if (Directory.Exists(outputDirectory))
        {
            foreach (var generatedFilePath in Directory.EnumerateFiles(outputDirectory, "*.g.cs", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(outputDirectory, "*.g.cpp", SearchOption.AllDirectories)))
            {
                var relativePath = Path.GetRelativePath(outputDirectory, generatedFilePath).Replace('\\', '/');
                if (!desiredRelativePaths.Contains(relativePath))
                {
                    File.Delete(generatedFilePath);
                }
            }
        }

        foreach (var file in Files)
        {
            var outputPath = Path.Combine(outputDirectory, file.RelativePath);
            var parentDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            File.WriteAllText(outputPath, file.Content);
        }
    }
}
