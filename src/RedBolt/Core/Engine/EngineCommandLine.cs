// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

// C++ defines engine command-line parameters in Engine::DefineParameters(CLI::App&, StringVariantMap&) in Engine.cpp.

namespace RedBolt.Engine;

using System.Globalization;
using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.RenderAPI;

internal static class EngineCommandLine
{
    public static void ApplyEnvironmentDefaults(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(parameters.ResourcePrefixPaths))
            parameters.ResourcePrefixPaths = Environment.GetEnvironmentVariable("URHO3D_PREFIX_PATH") ?? string.Empty;
    }

    public static void Register(CommandLineParser parser, EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(parameters);

        parser.AddFlag(["--headless"], "Do not initialize the graphics subsystem", () => parameters.Headless = true);
        parser.AddFlag(["--validate-shaders"], "Validate shaders before submitting them to the backend", () => parameters.ValidateShaders = true);
        parser.AddFlag(["--nolimit"], "Disable frame limiting", () => parameters.FrameLimiter = false);
        parser.AddFlag(["--landscape"], "Force landscape orientations", () => parameters.Orientations = "LandscapeLeft LandscapeRight " + parameters.Orientations);
        parser.AddFlag(["--portrait"], "Force portrait orientations", () => parameters.Orientations = "Portrait PortraitUpsideDown " + parameters.Orientations);
        parser.AddFlag(["--nosound"], "Disable sound", () => parameters.Sound = false);
        parser.AddFlag(["--noip"], "Disable sound interpolation", () => parameters.SoundInterpolation = false);
        parser.AddOption(["--speakermode"], "mode", "Force sound speaker mode", value => parameters.SoundMode = ParseInt(value));
        parser.AddFlag(["--nothreads"], "Disable worker threads", () => parameters.WorkerThreads = false);
        parser.AddFlag(["-v", "--vsync"], "Enable vertical sync", () => parameters.VSync = true);
        parser.AddFlag(["-w", "--windowed"], "Use decorated windowed mode", () =>
        {
            parameters.Borderless = false;
        });
        parser.AddFlag(["-f", "--full-screen"], "Use full-screen mode", () => parameters.FullScreen = true);
        parser.AddFlag(["--borderless"], "Use borderless window mode", () => parameters.Borderless = true);
        parser.AddFlag(["-s", "--resizable"], "Allow resizing the window", () => parameters.WindowResizable = true);
        parser.AddFlag(["-q", "--quiet"], "Disable logging output", () => parameters.LogQuiet = true);
        parser.AddOption(["-l", "--log"], "level", "Set log level", value => parameters.LogLevel = ParseLogLevel(value));
        parser.AddOption(["--log-file"], "path", "Set log output file", value => parameters.LogName = value);
        parser.AddFlag(["--no-log-rotate-on-open"], "Disable log rotation on log open", () => parameters.LogRotateOnOpen = false);
        parser.AddFlag(["--log-rotate-by-size"], "Rotate log file by size", () => parameters.LogRotateBySize = true);
        parser.AddOption(["--log-rotate-max-files"], "count", "Set maximum number of log files to keep", value => parameters.LogMaxFiles = ParseUInt(value));
        parser.AddOption(["--log-rotate-max-size"], "bytes", "Set maximum log file size", value => parameters.LogMaxSize = ParseUInt(value));
        parser.AddOption(["-x", "--width"], "pixels", "Set the window width", value => parameters.WindowWidth = ParseInt(value));
        parser.AddOption(["-y", "--height"], "pixels", "Set the window height", value => parameters.WindowHeight = ParseInt(value));
        parser.AddOption(["--monitor"], "index", "Select the display monitor", value => parameters.Monitor = ParseInt(value));
        parser.AddOption(["--hz"], "rate", "Set the refresh rate", value => parameters.RefreshRate = ParseInt(value));
        parser.AddOption(["-m", "--multisample"], "samples", "Set multisampling sample count", value => parameters.MultiSample = ParseInt(value));
        parser.AddOption(["-b", "--sound-buffer"], "milliseconds", "Set the sound buffer size", value => parameters.SoundBuffer = ParseInt(value));
        parser.AddOption(["-r", "--mix-rate"], "hz", "Set the sound mixing rate", value => parameters.SoundMixRate = ParseInt(value));
        parser.AddOption(["--pp", "--prefix-paths"], "paths", "Set resource prefix paths", value => parameters.ResourcePrefixPaths = value);
        parser.AddOption(["--pr", "--resource-paths"], "paths", "Set resource paths", value => parameters.ResourcePaths = value);
        parser.AddOption(["--pf", "--resource-packages"], "paths", "Set resource packages", value => parameters.ResourcePackages = value);
        parser.AddOption(["--ap", "--autoload-paths"], "paths", "Set resource autoload paths", value => parameters.AutoloadPaths = value);
        parser.AddOption(["--cn", "--config-name"], "file", "Set config file name", value => parameters.ConfigName = value);
        parser.AddOption(["--tq"], "quality", "Set texture quality level", value => parameters.TextureQuality = ParseTextureQuality(value));
        parser.AddOption(["--tf"], "mode", "Set texture filter mode", value => parameters.TextureFilterMode = ParseTextureFilterMode(value));
        parser.AddOption(["--af"], "level", "Enable anisotropic filtering at the requested level", value =>
        {
            parameters.TextureFilterMode = TextureFilterMode.Anisotropic;
            parameters.TextureAnisotropy = SystemMath.Max(ParseInt(value), 1);
        });
        parser.AddFlag(["--touch"], "Enable touch emulation", () => parameters.TouchEmulation = true);
        parser.AddOption(["--timeout"], "seconds", "Exit after the specified timeout", value => parameters.TimeOut = ParseInt(value));
        parser.AddOption(["--plugins"], "plugins", "Set plugin list", value => parameters.Plugins = value);
        parser.AddOption(["--plugin-paths"], "paths", "Set managed plugin search paths", value => parameters.PluginPaths = value);
        parser.AddOption(["--main"], "plugin", "Set the main plugin", value => parameters.MainPlugin = value);
        parser.AddFlag(["--log-shader-sources"], "Log shader sources to the shader cache directory", () => parameters.ShaderLogSources = true);
        parser.AddFlag(["--discard-shader-cache"], "Discard cached shader bytecode", () => parameters.DiscardShaderCache = true);
        parser.AddFlag(["--no-save-shader-cache"], "Disable saving shader cache data", () => parameters.SaveShaderCache = false);
        parser.AddFlag(["--xr"], "Launch in XR mode", () => parameters.XR = true);
        parser.AddFlag(["--profile"], "Enable profiling", () => parameters.Profile = true);
        parser.AddFlag(["--no-profile"], "Disable profiling", () => parameters.Profile = false);

        parser.AddFlag(["--d3d11"], "Use the D3D11 backend", () => parameters.RenderBackend = RenderBackend.D3D11);
        parser.AddFlag(["--d3d12"], "Use the D3D12 backend", () => parameters.RenderBackend = RenderBackend.D3D12);
        parser.AddFlag(["--opengl"], "Use the OpenGL backend", () => parameters.RenderBackend = RenderBackend.OpenGL);
        parser.AddFlag(["--vulkan"], "Use the Vulkan backend", () => parameters.RenderBackend = RenderBackend.Vulkan);

        parser.AddOption(["--app-name"], "name", "Set the application name", value => parameters.ApplicationName = value);
        parser.AddOption(["--organization"], "name", "Set the organization name", value => parameters.OrganizationName = value);
        parser.AddOption(["--preferences-dir"], "path", "Set the application preferences directory", value => parameters.ApplicationPreferencesDir = value);
        parser.AddOption(["--window-title"], "title", "Set the window title", value => parameters.WindowTitle = value);
        parser.AddOption(["--window-icon"], "path", "Set the window icon resource path", value => parameters.WindowIcon = value);
        parser.AddFlag(["--maximize"], "Start with a maximized window", () => parameters.WindowMaximize = true);
        parser.AddOption(["--window-x"], "pixels", "Set the initial window X position", value => parameters.WindowPositionX = ParseInt(value));
        parser.AddOption(["--window-y"], "pixels", "Set the initial window Y position", value => parameters.WindowPositionY = ParseInt(value));
        parser.AddOption(["--resource-root-file"], "file", "Set the resource root file name", value => parameters.ResourceRootFile = value);
        parser.AddOption(["--package-cache-dir"], "path", "Set the package cache directory", value => parameters.PackageCacheDir = value);
        parser.AddOption(["--shader-cache-dir"], "path", "Set the shader cache directory", value => parameters.ShaderCacheDir = value);
        parser.AddOption(["--pso-cache"], "path", "Set the PSO cache path", value => parameters.PsoCache = value);
        parser.AddOption(["--shader-policy"], "policy", "Set shader translation policy", value => parameters.ShaderPolicy = value);
        parser.AddFlag(["--gpu-debug"], "Enable GPU debug mode", () => parameters.GPUDebug = true);
        parser.AddOption(["--adapter-id"], "id", "Select the graphics adapter", value => parameters.RenderAdapterId = ParseUInt(value));
        parser.AddFlag(["--reload-plugins"], "Enable plugin hot-reload", () => parameters.ReloadPlugins = true);
        parser.AddOption(["--imgui-flags"], "flags", "Set Dear ImGui flags", value => parameters.DearImGuiFlags = ParseUInt(value));
        parser.AddOption(["--tweak-d3d12"], "json", "Set D3D12 tweak JSON", value => parameters.TweakD3D12 = value);
        parser.AddOption(["--tweak-vulkan"], "json", "Set Vulkan tweak JSON", value => parameters.TweakVulkan = value);
        parser.AddFlag(["--engine-auto-load-scripts"], "Enable engine script autoloading", () => parameters.EngineAutoLoadScripts = true);
        parser.AddFlag(["--no-load-fonts"], "Disable automatic font loading", () => parameters.LoadFonts = false);
        parser.AddOption(["--screenshot-on-exit"], "path", "Save a screenshot just before shutdown", value => parameters.ScreenshotOnExit = value);
        parser.AddOption(["--screenshot-parameters"], "count,delay", "Set screenshot count and frame delay", value => ParseScreenshotParameters(value, parameters));
    }

    private static int ParseInt(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static uint ParseUInt(string value)
    {
        return uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static void ParseScreenshotParameters(string value, EngineParameters parameters)
    {
        var separator = value.IndexOf(',');
        if (separator <= 0 || separator == value.Length - 1 || value.IndexOf(',', separator + 1) >= 0)
            throw new ArgumentException($"Invalid screenshot parameters '{value}'. Expected count,delay.", nameof(value));

        var count = ParseInt(value[..separator]);
        var frameDelay = ParseInt(value[(separator + 1)..]);
        if (count < 1)
            throw new ArgumentException($"Screenshot count '{count}' must be positive.", nameof(value));
        if (frameDelay < 0)
            throw new ArgumentException($"Screenshot frame delay '{frameDelay}' cannot be negative.", nameof(value));

        parameters.ScreenshotCount = count;
        parameters.ScreenshotFrameDelay = frameDelay;
    }

    private static LogLevel ParseLogLevel(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Info,
            "WARN" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            _ => throw new ArgumentException($"Unsupported log level '{value}'.", nameof(value))
        };
    }

    private static MaterialQuality ParseTextureQuality(string value)
    {
        var quality = ParseInt(value);
        if (quality is < (int)MaterialQuality.Low or > (int)MaterialQuality.Max)
            throw new ArgumentException($"Texture quality '{value}' is out of range.", nameof(value));

        return (MaterialQuality)quality;
    }

    private static TextureFilterMode ParseTextureFilterMode(string value)
    {
        return value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-') switch
        {
            "nearest" or "point" => TextureFilterMode.Nearest,
            "bilinear" or "linear" => TextureFilterMode.Bilinear,
            "trilinear" => TextureFilterMode.Trilinear,
            "anisotropic" => TextureFilterMode.Anisotropic,
            "nearest-anisotropic" => TextureFilterMode.NearestAnisotropic,
            "default" => TextureFilterMode.Default,
            _ => throw new ArgumentException($"Unsupported texture filter mode '{value}'.", nameof(value))
        };
    }
}
