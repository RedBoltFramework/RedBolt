// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.ModuleBuild.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class EnsureDirectoryLink : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Source { get; set; } = string.Empty;

    [Required]
    public string Link { get; set; } = string.Empty;

    public override bool Execute()
    {
        if (!Directory.Exists(Source))
        {
            Log.LogError("Resource directory does not exist: {0}", Source);
            return false;
        }

        var linkInfo = new DirectoryInfo(Link);
        if (linkInfo.Exists)
            return true;

        if (linkInfo.LinkTarget is not null)
        {
            try
            {
                linkInfo.Delete();
            }
            catch (DirectoryNotFoundException)
            {
                // Unix reports a dangling link as a missing directory and leaves it in place. Removing
                // it as a file works there, and passes when a project built in parallel removed it first.
                File.Delete(Link);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Link)!);
        try
        {
            Directory.CreateSymbolicLink(Link, Source);
        }
        catch (IOException) when (Directory.Exists(Link))
        {
            // A project built in parallel linked the same shared directory first.
        }

        return true;
    }
}
