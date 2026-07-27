using System.Runtime.CompilerServices;

namespace RedBolt.Tests;

internal static class TestPlatformModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LinuxPlatform.Initialize();
    }
}
