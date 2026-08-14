using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

return await AndroidEmulator.RunAsync(args);

internal static class AndroidEmulator
{
    private const string ApplicationId = "org.redbolt.samples";

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            return await ExecuteAsync(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(string[] args)
    {
        var repository = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, ".."));
        var dataDirectory = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        var androidRoot = ResolvePath(repository,
            EnvironmentValue("REDBOLT_ANDROID_ROOT", Path.Combine(dataDirectory, "redbolt-android")));
        var sdkRoot = ResolvePath(repository, EnvironmentValue("ANDROID_SDK_ROOT", Path.Combine(androidRoot, "sdk")));
        var javaRoot = ResolvePath(repository, EnvironmentValue("JAVA_HOME", Path.Combine(androidRoot, "jdk")));
        var avdHome = ResolvePath(repository, EnvironmentValue("ANDROID_AVD_HOME", Path.Combine(androidRoot, "avd")));
        var avdName = EnvironmentValue("REDBOLT_ANDROID_AVD", "redbolt-api36");
        var systemImage = EnvironmentValue("REDBOLT_ANDROID_SYSTEM_IMAGE", "system-images;android-36;google_apis;x86_64");
        var emulatorGpu = EnvironmentValue("REDBOLT_ANDROID_GPU", "swangle");
        var apk = EnvironmentValue("REDBOLT_ANDROID_APK",
            Path.Combine(repository, "build", "bin", "Debug", "android-x64", "app", "RedBolt.Sample.Android.apk"));
        string? sample = null;
        string? screenshot = null;
        var emulatorWindow = false;
        var shutdown = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--apk" when index + 1 < args.Length:
                    apk = args[++index];
                    break;
                case "--sample" when index + 1 < args.Length:
                    sample = args[++index];
                    break;
                case "--screenshot" when index + 1 < args.Length:
                    screenshot = args[++index];
                    break;
                case "--window":
                    emulatorWindow = true;
                    break;
                case "--shutdown":
                    shutdown = true;
                    break;
                case "-h" or "--help":
                    return Usage();
                default:
                    return Usage(error: true);
            }
        }

        if (sample is not null && !Regex.IsMatch(sample, "^[a-z0-9-]+$"))
        {
            Console.Error.WriteLine($"Invalid sample id: {sample}");
            return 2;
        }
        apk = ResolvePath(repository, apk);

        var adb = Path.Combine(sdkRoot, "platform-tools", Executable("adb"));
        var configuredEmulator = Environment.GetEnvironmentVariable("REDBOLT_ANDROID_EMULATOR");
        var emulator = configuredEmulator is null
            ? Path.Combine(androidRoot, "emulator-stable", Executable("emulator"))
            : ResolvePath(repository, configuredEmulator);
        if (configuredEmulator is null && !File.Exists(emulator))
            emulator = Path.Combine(sdkRoot, "emulator", Executable("emulator"));
        var avdManager = Path.Combine(sdkRoot, "cmdline-tools", "latest", "bin",
            OperatingSystem.IsWindows() ? "avdmanager.bat" : "avdmanager");

        foreach (var tool in new[] { adb, emulator, avdManager })
        {
            if (!File.Exists(tool))
                throw new InvalidOperationException($"Missing Android tool: {tool}");
        }
        if (!File.Exists(apk))
            throw new InvalidOperationException($"APK not found: {apk}");

        Environment.SetEnvironmentVariable("ANDROID_AVD_HOME", avdHome);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdkRoot);
        Environment.SetEnvironmentVariable("JAVA_HOME", javaRoot);
        Directory.CreateDirectory(avdHome);

        var avds = await CommandAsync(avdManager, ["list", "avd"]);
        if (!avds.Output.Contains($"Name: {avdName}", StringComparison.Ordinal))
        {
            await CommandAsync(avdManager,
                ["create", "avd", "--force", "--name", avdName, "--package", systemImage], input: "no\n", echo: true);
        }

        var serial = await FindEmulatorAsync(adb);
        Process? emulatorProcess = null;
        var accelerationRequest = EnvironmentValue("REDBOLT_ANDROID_ACCEL", "auto");
        var acceleration = accelerationRequest;
        var bootAttempts = 180;
        switch (acceleration)
        {
            case "auto":
                acceleration = (await CommandAsync(emulator, ["-accel-check"], allowFailure: true)).ExitCode == 0
                    ? "on"
                    : "off";
                break;
            case "on" or "off":
                break;
            default:
                Console.Error.WriteLine($"Invalid REDBOLT_ANDROID_ACCEL value: {acceleration} (expected auto, on, or off)");
                return 2;
        }
        if (acceleration == "off")
            bootAttempts = 600;

        var runtimeTimeout = Environment.GetEnvironmentVariable("REDBOLT_ANDROID_RUNTIME_TIMEOUT") is { Length: > 0 } timeout
            ? PositiveInteger(timeout, "REDBOLT_ANDROID_RUNTIME_TIMEOUT")
            : bootAttempts > 180 ? 600 : 180;

        if (serial is null)
        {
            var emulatorArguments = new List<string>
            {
                "-avd", avdName, "-no-boot-anim", "-no-snapshot", "-no-metrics", "-no-audio",
                "-gpu", emulatorGpu, "-accel", acceleration
            };
            if (acceleration == "on")
                Console.WriteLine("KVM acceleration is available; enabling it.");
            else if (accelerationRequest == "off")
                Console.WriteLine("KVM acceleration is disabled by REDBOLT_ANDROID_ACCEL.");
            else
                Console.WriteLine("KVM acceleration is unavailable; using the slower software CPU emulator.");
            if (!emulatorWindow)
                emulatorArguments.Add("-no-window");
            emulatorProcess = StartEmulator(emulator, emulatorArguments);
        }

        for (var attempt = 0; attempt < bootAttempts; attempt++)
        {
            if (emulatorProcess is { HasExited: true })
                throw new InvalidOperationException($"Android emulator exited during startup with code {emulatorProcess.ExitCode}.");
            serial = await FindEmulatorAsync(adb);
            if (serial is not null && await AndroidReadyAsync(adb, serial))
                break;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        if (serial is null || !await AndroidReadyAsync(adb, serial))
            throw new InvalidOperationException("Android emulator did not finish booting.");

        await AdbAsync(adb, serial, ["shell", "am", "force-stop", ApplicationId], allowFailure: true);
        await AdbAsync(adb, serial, ["install", "--no-incremental", "-r", apk], echo: true);
        await AdbAsync(adb, serial, ["logcat", "-c"]);
        await AdbAsync(adb, serial, ["shell", "am", "force-stop", ApplicationId]);
        await AdbAsync(adb, serial,
            ["shell", "settings", "put", "secure", "immersive_mode_confirmations", "confirmed"], allowFailure: true);

        var resolved = await AdbAsync(adb, serial,
        [
            "shell", "cmd", "package", "resolve-activity", "--brief",
            "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER", ApplicationId
        ]);
        var activity = resolved.Output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (activity is null || !activity.Contains('/'))
            throw new InvalidOperationException("Unable to resolve the RedBolt launcher activity.");

        var launchArguments = new List<string> { "shell", "am", "start", "-W", "-n", activity };
        if (sample is not null)
            launchArguments.AddRange(["--es", "sample", sample]);
        await AdbAsync(adb, serial, launchArguments, echo: true);

        var pidResult = await AdbAsync(adb, serial, ["shell", "pidof", ApplicationId], allowFailure: true, timeoutSeconds: 10);
        var applicationPid = pidResult.Output.Replace("\r", "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (applicationPid is null)
            throw new InvalidOperationException("Android sample process exited during launch.");
        var logcatArguments = new[]
        {
            "logcat", "-d", $"--pid={applicationPid}", "-s", "RedBolt:V", "Diligent Engine:V",
            "AndroidRuntime:E", "mono-rt:E", "*:S"
        };

        if (sample is not null)
        {
            var waitForDearImGui = !sample.StartsWith("urho2d-", StringComparison.Ordinal)
                                   && !sample.StartsWith("physics2d", StringComparison.Ordinal);
            DateTime? runningSince = null;
            var deadline = DateTime.UtcNow.AddSeconds(runtimeTimeout);
            var logs = "";
            while (DateTime.UtcNow < deadline)
            {
                logs = (await AdbAsync(adb, serial, logcatArguments, allowFailure: true, timeoutSeconds: 10)).Output;
                if (Regex.IsMatch(logs, "FATAL EXCEPTION|Unhandled Exception|Debug assertion failed|Sample .* exited with code [^0]"))
                    throw new InvalidOperationException($"Android sample failed during startup.{Environment.NewLine}{logs}");
                if (logs.Contains("Verified packaged CoreData asset", StringComparison.Ordinal))
                {
                    runningSince ??= DateTime.UtcNow;
                    if (logs.Contains("Dear ImGui UI ready:", StringComparison.Ordinal)
                        || !waitForDearImGui && DateTime.UtcNow - runningSince >= TimeSpan.FromSeconds(5))
                        break;
                }
                var running = await AdbAsync(adb, serial, ["shell", "pidof", ApplicationId], allowFailure: true, timeoutSeconds: 10);
                if (running.ExitCode != 0 || string.IsNullOrWhiteSpace(running.Output))
                    throw new InvalidOperationException(
                        $"Android sample process exited before reaching the runtime gate.{Environment.NewLine}{logs}");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            if (DateTime.UtcNow >= deadline)
                throw new InvalidOperationException(
                    $"Android sample did not reach the runtime gate within {runtimeTimeout}s.{Environment.NewLine}{logs}");
            Console.WriteLine($"Android runtime gate passed for sample: {sample}");
        }

        if (screenshot is not null)
        {
            screenshot = ResolvePath(repository, screenshot);
            Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
            await AdbToFileAsync(adb, serial, ["exec-out", "screencap", "-p"], screenshot);
            Console.WriteLine($"Screenshot: {screenshot}");
        }

        var finalLogs = await AdbAsync(adb, serial, logcatArguments);
        Console.Write(finalLogs.Output);
        if (shutdown)
            await AdbAsync(adb, serial, ["emu", "kill"], echo: true);
        return 0;
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;

    private static int Usage(bool error = false)
    {
        var output = error ? Console.Error : Console.Out;
        output.WriteLine("Usage: dotnet run --file scripts/run_android_emulator.cs -- [--apk PATH] [--sample ID] [--screenshot PATH] [--window] [--shutdown]");
        return error ? 2 : 0;
    }

    private static string EnvironmentValue(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static string Executable(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    private static string ResolvePath(string repository, string path) =>
        Path.GetFullPath(path, Path.IsPathRooted(path) ? Path.GetPathRoot(path)! : repository);

    private static int PositiveInteger(string value, string name) =>
        int.TryParse(value, out var result) && result > 0
            ? result
            : throw new InvalidOperationException($"Invalid {name} value: {value} (expected a positive integer)");

    private static async Task<string?> FindEmulatorAsync(string adb)
    {
        var devices = await CommandAsync(adb, ["devices"], allowFailure: true, timeoutSeconds: 10);
        foreach (var line in devices.Output.Replace("\r", "").Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 2 && fields[0].StartsWith("emulator-", StringComparison.Ordinal) && fields[1] == "device")
                return fields[0];
        }
        return null;
    }

    private static async Task<bool> AndroidReadyAsync(string adb, string serial)
    {
        var boot = await AdbAsync(adb, serial,
            ["shell", "getprop", "sys.boot_completed"], allowFailure: true, timeoutSeconds: 10);
        var provisioned = await AdbAsync(adb, serial,
            ["shell", "settings", "get", "global", "device_provisioned"], allowFailure: true, timeoutSeconds: 10);
        var framework = await AdbAsync(adb, serial,
            ["shell", "cmd", "package", "path", "android"], allowFailure: true, timeoutSeconds: 10);
        return boot.ExitCode == 0 && boot.Output.Trim() == "1"
               && provisioned.ExitCode == 0 && provisioned.Output.Trim() is "0" or "1"
               && framework.ExitCode == 0 && framework.Output.Trim().StartsWith("package:", StringComparison.Ordinal);
    }

    private static Process StartEmulator(string executable, IEnumerable<string> arguments)
    {
        var startInfo = StartInfo(executable, arguments, redirect: false);
        startInfo.RedirectStandardInput = true;
        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException($"Unable to start Android emulator: {executable}");
        process.StandardInput.Close();
        return process;
    }

    private static Task<CommandResult> AdbAsync(string adb, string serial, IEnumerable<string> arguments,
        bool allowFailure = false, bool echo = false, int timeoutSeconds = 0) =>
        CommandAsync(adb, ["-s", serial, .. arguments], allowFailure, echo, timeoutSeconds: timeoutSeconds);

    private static async Task<CommandResult> CommandAsync(string executable, IEnumerable<string> arguments,
        bool allowFailure = false, bool echo = false, string? input = null, int timeoutSeconds = 0)
    {
        var argumentList = arguments.ToArray();
        using var process = new Process { StartInfo = StartInfo(executable, argumentList, redirect: true) };
        process.StartInfo.RedirectStandardInput = input is not null;
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }
        try
        {
            if (timeoutSeconds > 0)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                await process.WaitForExitAsync(cancellation.Token);
            }
            else
                await process.WaitForExitAsync();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(output, error);
            return new CommandResult(124, output.Result, error.Result);
        }
        await Task.WhenAll(output, error);
        var result = new CommandResult(process.ExitCode, output.Result, error.Result);
        if (echo)
        {
            Console.Write(result.Output);
            Console.Error.Write(result.Error);
        }
        if (result.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException(
                $"Command failed ({result.ExitCode}): {executable}{Environment.NewLine}{result.Error}{result.Output}");
        return result;
    }

    private static async Task AdbToFileAsync(string adb, string serial, IEnumerable<string> arguments, string outputPath)
    {
        using var process = new Process { StartInfo = StartInfo(adb, ["-s", serial, .. arguments], redirect: true) };
        process.Start();
        await using var output = File.Create(outputPath);
        var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(copy, error);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Command failed ({process.ExitCode}): {adb}{Environment.NewLine}{error.Result}");
    }

    private static ProcessStartInfo StartInfo(string executable, IEnumerable<string> arguments, bool redirect)
    {
        var usesCommandInterpreter = OperatingSystem.IsWindows()
                                     && Path.GetExtension(executable).Equals(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = usesCommandInterpreter
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect
        };
        if (usesCommandInterpreter)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("call");
            startInfo.ArgumentList.Add(executable);
        }
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
