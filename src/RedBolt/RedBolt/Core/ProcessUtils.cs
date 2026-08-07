namespace RedBolt.Core;

using System.Diagnostics;
using System.Globalization;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.Sdl;
using RedBolt.Container;
using RedBolt.IO;

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
    private static readonly List<string> _arguments = [];

    public static string DynLibSuffix =>
        OperatingSystem.IsWindows() ? ".dll" :
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() ? ".dylib" :
        ".so";

    /// <summary>Print Unicode text to the console.</summary>
    public static void PrintUnicode(string value, bool error = false)
    {
        (error ? Console.Error : Console.Out).Write(value);
    }

    /// <summary>Print Unicode text to the console with a newline appended.</summary>
    public static void PrintUnicodeLine(string value, bool error = false)
    {
        (error ? Console.Error : Console.Out).WriteLine(value);
    }

    /// <summary>Print text to the console with a newline appended.</summary>
    public static void PrintLine(string value, bool error = false)
    {
        (error ? Console.Error : Console.Out).WriteLine(value);
    }

    public static void ErrorDialog(string title, string message)
    {
        using var titleValue = new CString(title);
        using var messageValue = new CString(message);
        _ = SdlNativeBindings.SdlShowSimpleMessageBox((uint)SdlMessageBoxFlags.SdlMessageboxError, titleValue.Bytes, messageValue.Bytes, null!);
    }

    /// <summary>Parse arguments from a command line.</summary>
    public static IReadOnlyList<string> ParseArguments(string commandLine, bool skipFirstArgument = true)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        _arguments.Clear();

        var current = new StringBuilder();
        var inQuote = false;
        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuote)
            {
                if (current.Length > 0)
                {
                    _arguments.Add(current.ToString());
                    current.Clear();
                }
            }
            else
                current.Append(character);
        }

        if (current.Length > 0)
            _arguments.Add(current.ToString());

        if (skipFirstArgument && _arguments.Count > 0)
            _arguments.RemoveAt(0);

        if (OperatingSystem.IsWindows())
            _arguments.Remove("--win32-console");

        return _arguments;
    }

    /// <summary>Parse arguments from an argument array.</summary>
    public static IReadOnlyList<string> ParseArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments.Clear();
        for (var index = 1; index < arguments.Count; ++index)
            _arguments.Add(arguments[index]);
        return _arguments;
    }

    /// <summary>Return previously parsed arguments.</summary>
    public static IReadOnlyList<string> GetArguments() => _arguments;

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
            if (System.IO.File.Exists("/sys/firmware/devicetree/base/model"))
            {
                try
                {
                    var model = System.IO.File.ReadAllText("/sys/firmware/devicetree/base/model");
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

        if (OperatingSystem.IsLinux() && System.IO.File.Exists("/proc/cpuinfo"))
        {
            try
            {
                var physicalCores = new HashSet<string>(StringComparer.Ordinal);
                string physicalId = string.Empty;
                string coreId = string.Empty;

                foreach (var line in System.IO.File.ReadLines("/proc/cpuinfo"))
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

        return SystemMath.Max(1u, cpuCount);
    }

    public static uint GetNumLogicalCPUs() => SystemMath.Max(1u, (uint)Environment.ProcessorCount);

    public static void SetMiniDumpDir(string pathName)
    {
        ArgumentNullException.ThrowIfNull(pathName);
        _miniDumpDir = AddTrailingSlash(pathName);
    }

    public static string GetMiniDumpDir() => _miniDumpDir;

    public static ulong GetTotalMemory()
    {
        if (OperatingSystem.IsLinux() && System.IO.File.Exists("/proc/meminfo"))
        {
            try
            {
                foreach (var line in System.IO.File.ReadLines("/proc/meminfo"))
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

    public static int SystemRun(string fileName, ReadOnlySpan<string> arguments, out string output)
    {
        output = string.Empty;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            if (!process.Start())
                return -1;

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            output = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException or PlatformNotSupportedException or System.IO.IOException)
        {
            output = string.Empty;
            return -1;
        }
    }

    public static bool OpenURL(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return false;

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
