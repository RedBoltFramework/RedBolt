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
        if (linkInfo.LinkTarget is null)
        {
            if (linkInfo.Exists)
                return true;
        }
        else
        {
            // Windows reports a dangling directory link as existing, so test what the link resolves to.
            if (LinkResolves(linkInfo))
                return true;

            try
            {
                linkInfo.Delete();
            }
            catch (DirectoryNotFoundException)
            {
                // Unix reports a dangling link as a missing directory and leaves it in place. Removing
                // it as a file works there, and passes when a project built in parallel removed it first.
                // On Windows that leaves only a link a parallel project made meanwhile, which cannot be
                // removed as a file and is kept.
                try
                {
                    File.Delete(Link);
                }
                catch (UnauthorizedAccessException) when (Directory.Exists(Link))
                {
                    return true;
                }
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

    private static bool LinkResolves(DirectoryInfo linkInfo)
    {
        // Elsewhere the existence test follows the link, and resolves relative targets as the system does.
        if (!OperatingSystem.IsWindows())
            return linkInfo.Exists;

        try
        {
            return linkInfo.ResolveLinkTarget(returnFinalTarget: true) is DirectoryInfo { Exists: true };
        }
        catch (IOException)
        {
            // A chain of links that cannot be followed is as good as a dangling one.
            return false;
        }
    }
}
