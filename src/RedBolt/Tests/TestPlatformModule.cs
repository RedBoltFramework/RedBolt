namespace RedBolt.Tests;

using System.Runtime.CompilerServices;

using RedBolt.Platform;

internal static class TestPlatformModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LinuxPlatform.Initialize();
    }
}
