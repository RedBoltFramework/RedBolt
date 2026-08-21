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
            linkInfo.Delete();

        Directory.CreateDirectory(Path.GetDirectoryName(Link)!);
        Directory.CreateSymbolicLink(Link, Source);
        return true;
    }
}
