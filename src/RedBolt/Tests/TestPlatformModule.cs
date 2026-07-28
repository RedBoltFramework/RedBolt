namespace RedBolt.Tests;

using System.Runtime.CompilerServices;

internal static class TestPlatformModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LinuxPlatform.Initialize();
    }
}
