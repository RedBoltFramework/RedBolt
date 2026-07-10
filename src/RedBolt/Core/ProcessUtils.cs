using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

public enum PlatformId
{
    Windows,
    UniversalWindowsPlatform,

    Linux,
    Android,
    RaspberryPi,

    MacOS,
    iOS,
    TvOS,

    Web,

    Count,
    Unknown = Count
}

public static class ProcessUtils
{
    private static string _miniDumpDir = string.Empty;

    public static string DynLibSuffix =>
        OperatingSystem.IsWindows() ? ".dll" :
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() ? ".dylib" :
        ".so";

    public static PlatformId GetPlatform()
    {
        if (OperatingSystem.IsAndroid())
            return PlatformId.Android;
        if (OperatingSystem.IsIOS())
            return PlatformId.iOS;
        if (OperatingSystem.IsTvOS())
            return PlatformId.TvOS;
        if (OperatingSystem.IsMacOS())
            return PlatformId.MacOS;
        if (OperatingSystem.IsWindows())
            return PlatformId.Windows;
        if (OperatingSystem.IsBrowser())
            return PlatformId.Web;
        if (OperatingSystem.IsLinux())
        {
            if (File.Exists("/sys/firmware/devicetree/base/model"))
            {
                try
                {
                    var model = File.ReadAllText("/sys/firmware/devicetree/base/model");
                    if (model.Contains("Raspberry Pi", StringComparison.OrdinalIgnoreCase))
                        return PlatformId.RaspberryPi;
                }
                catch
                {
                }
            }

            return PlatformId.Linux;
        }

        return PlatformId.Unknown;
    }

    public static string GetPlatformName()
    {
        switch (GetPlatform())
        {
            case PlatformId.Windows:
                return "Windows";
            case PlatformId.UniversalWindowsPlatform:
                return "UWP";

            case PlatformId.Linux:
                return "Linux";
            case PlatformId.Android:
                return "Android";
            case PlatformId.RaspberryPi:
                return "Raspberry Pi";

            case PlatformId.MacOS:
                return "macOS";
            case PlatformId.iOS:
                return "iOS";
            case PlatformId.TvOS:
                return "tvOS";

            case PlatformId.Web:
                return "Web";

            default:
                return "(?)";
        }
    }

    public static uint GetNumPhysicalCPUs()
    {
        var cpuCount = 0u;

        if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
        {
            try
            {
                var physicalCores = new HashSet<string>(StringComparer.Ordinal);
                string physicalId = string.Empty;
                string coreId = string.Empty;

                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("physical id", StringComparison.Ordinal))
                        physicalId = line[(line.IndexOf(':') + 1)..].Trim();
                    else if (line.StartsWith("core id", StringComparison.Ordinal))
                        coreId = line[(line.IndexOf(':') + 1)..].Trim();
                    else if (string.IsNullOrWhiteSpace(line))
                    {
                        if (!string.IsNullOrEmpty(physicalId) && !string.IsNullOrEmpty(coreId))
                            physicalCores.Add($"{physicalId}:{coreId}");

                        physicalId = string.Empty;
                        coreId = string.Empty;
                    }
                }

                if (!string.IsNullOrEmpty(physicalId) && !string.IsNullOrEmpty(coreId))
                    physicalCores.Add($"{physicalId}:{coreId}");

                if (physicalCores.Count > 0)
                    cpuCount = (uint)physicalCores.Count;
            }
            catch
            {
            }
        }

        if (cpuCount == 0)
            cpuCount = GetNumLogicalCPUs();

        return Math.Max(1u, cpuCount);
    }

    public static uint GetNumLogicalCPUs() => Math.Max(1u, (uint)Environment.ProcessorCount);

    public static void SetMiniDumpDir(string pathName)
    {
        ArgumentNullException.ThrowIfNull(pathName);
        _miniDumpDir = AddTrailingSlash(pathName);
    }

    public static string GetMiniDumpDir() => _miniDumpDir;

    public static ulong GetTotalMemory()
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                        continue;

                    var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (fields.Length >= 2 && ulong.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kiloBytes))
                        return kiloBytes * 1024UL;
                }
            }
            catch
            {
            }
        }

        var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return totalAvailableMemory > 0 ? (ulong)totalAvailableMemory : 0UL;
    }

    public static string GetLoginName() => string.IsNullOrWhiteSpace(Environment.UserName) ? "(?)" : Environment.UserName;

    public static string GetHostName() => string.IsNullOrWhiteSpace(Environment.MachineName) ? "(?)" : Environment.MachineName;

    public static string GetOSVersion()
    {
        var osDescription = RuntimeInformation.OSDescription;
        return string.IsNullOrWhiteSpace(osDescription) ? "(?)" : osDescription.Trim();
    }

    public static string GenerateUUID() => Guid.NewGuid().ToString();

    public static uint GetCurrentProcessID() => (uint)Environment.ProcessId;

    public static bool OpenURL(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string AddTrailingSlash(string pathName)
    {
        if (string.IsNullOrEmpty(pathName))
            return string.Empty;

        return Path.EndsInDirectorySeparator(pathName) ? pathName : pathName + Path.DirectorySeparatorChar;
    }
}
