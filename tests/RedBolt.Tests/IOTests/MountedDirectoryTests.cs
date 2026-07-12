using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using RedBolt;

namespace RedBolt.Tests;

public sealed class MountedDirectoryTests
{
    [Fact]
    public void MountedDirectory_sanitizes_directory_and_maps_identifiers()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            Context.SetInstance(new Context());
            var mountPoint = new MountedDirectory(directory, "assets");
            var identifier = new FileIdentifier("assets", "Textures/Box.png");
            var absoluteName = Path.Combine(mountPoint.GetDirectory(), "Textures/Box.png");

            Assert.Equal("assets", identifier.Scheme);
            Assert.Equal("assets://" + mountPoint.GetDirectory(), mountPoint.GetName());
            mountPoint.SetWatching(true);
            Assert.True(mountPoint.IsWatching());
            mountPoint.SetWatching(false);
            Assert.False(mountPoint.IsWatching());
            Assert.Equal(string.Empty, mountPoint.GetAbsoluteNameFromIdentifier(identifier));
            Assert.Equal(new FileIdentifier("assets", "Textures/Box.png"), mountPoint.GetIdentifierFromAbsoluteName(absoluteName));
        }
        finally
        {
            Context.SetInstance(new Context());
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MountedDirectory_opens_scans_and_reports_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "Textures"));
        var fileName = Path.Combine(directory, "Textures", "Box.txt");

        try
        {
            System.IO.File.WriteAllText(fileName, "box");
            var mountPoint = new MountedDirectory(directory, "assets");
            var identifier = new FileIdentifier("assets", "Textures/Box.txt");

            Assert.True(mountPoint.Exists(identifier));
            Assert.NotNull(mountPoint.GetLastModifiedTime(identifier, false));

            var result = new List<string>();
            mountPoint.Scan(result, string.Empty, "*.txt", ScanFlags.ScanFiles | ScanFlags.ScanRecursive);
            Assert.Contains("Textures/Box.txt", result);

            var input = mountPoint.OpenFile(identifier, File.FileMode.Read);
            try
            {
                Assert.NotNull(input);
                Assert.Equal(3u, input.Read(new byte[3]));
            }
            finally
            {
                input?.Close();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MountedDirectory_dispatches_file_changed_event_on_begin_frame()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"redbolt-mounted-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var context = new Context();
        Context.SetInstance(context);
        var mountPoint = new MountedDirectory(directory, "assets");
        ResourceEvents.FileChanged? observed = null;
        context.SubscribeToEvent<ResourceEvents.FileChanged>((ref ResourceEvents.FileChanged eventData) => observed = eventData);

        try
        {
            mountPoint.SetWatching(true);
            System.IO.File.WriteAllText(Path.Combine(directory, "Changed.txt"), "changed");
            Thread.Sleep(1100);

            var beginFrame = new CoreEvents.BeginFrameEvent();
            context.SendEvent(ref beginFrame);

            Assert.NotNull(observed);
            Assert.Equal(Path.Combine(mountPoint.GetDirectory(), "Changed.txt"), observed.Value.FileName);
            Assert.Equal("assets://Changed.txt", observed.Value.ResourceName);
        }
        finally
        {
            mountPoint.SetWatching(false);
            Context.SetInstance(new Context());
            Directory.Delete(directory, true);
        }
    }
}
