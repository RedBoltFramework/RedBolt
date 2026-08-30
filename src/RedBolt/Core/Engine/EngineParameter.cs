// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Engine;

using RedBolt.Logging;
using RedBolt.RenderAPI;

// Keep sorted when adding new elements.
public static class EngineParameter
{
    public const string ApplicationName = "ApplicationName";
    public const string ApplicationPreferencesDir = "ApplicationPreferencesDir";
    public const string AutoloadPaths = "AutoloadPaths";
    public const string Borderless = "Borderless";
    public const string ConfigName = "ConfigName";
    public const string DearImGuiFlags = "DearImGuiFlags";
    public const string DiscardShaderCache = "DiscardShaderCache";
    public const string EngineAutoLoadScripts = "EngineAutoLoadScripts";
    public const string EngineCliParameters = "EngineCliParameters";
    public const string ExternalWindow = "ExternalWindow";
    public const string FrameLimiter = "FrameLimiter";
    public const string FullScreen = "FullScreen";
    public const string GPUDebug = "GPUDebug";
    public const string Headless = "Headless";
    public const string LoadFonts = "LoadFonts";
    public const string LogLevel = "LogLevel";
    public const string LogName = "LogName";
    public const string LogQuiet = "LogQuiet";
    public const string LogRotateOnOpen = "LogRotateOnOpen";
    public const string LogRotateBySize = "LogRotateBySize";
    public const string LogMaxFiles = "LogMaxFiles";
    public const string LogMaxSize = "LogMaxSize";
    public const string MainPlugin = "MainPlugin";
    public const string Monitor = "Monitor";
    public const string MouseEmulation = "MouseEmulation";
    public const string MultiSample = "MultiSample";
    public const string OrganizationName = "OrganizationName";
    public const string Orientations = "Orientations";
    public const string PackageCacheDir = "PackageCacheDir";
    public const string Plugins = "Plugins";

    public const string PluginPaths = "PluginPaths";
    public const string Profile = "Profile";
    public const string PsoCache = "PsoCacheDir";
    public const string RefreshRate = "RefreshRate";
    public const string ReloadPlugins = "ReloadPlugins";
    public const string RenderAdapterId = "RenderAdapterId";
    public const string RenderBackend = "RenderBackend";
    public const string ResourcePackages = "ResourcePackages";
    public const string ResourcePaths = "ResourcePaths";
    public const string ResourcePrefixPaths = "ResourcePrefixPaths";
    public const string ResourceRootFile = "ResourceRootFile";
    public const string SaveShaderCache = "SaveShaderCache";
    public const string ScreenshotCount = "ScreenshotCount";
    public const string ScreenshotFrameDelay = "ScreenshotFrameDelay";
    public const string ScreenshotOnExit = "ScreenshotOnExit";
    public const string ShaderCacheDir = "ShaderCacheDir";
    public const string ShaderLogSources = "ShaderLogSource";
    public const string ShaderPolicy = "ShaderPolicy";
    public const string Sound = "Sound";
    public const string SoundBuffer = "SoundBuffer";
    public const string SoundInterpolation = "SoundInterpolation";
    public const string SoundMixRate = "SoundMixRate";
    public const string SoundMode = "SoundMode";
    public const string SystemUIFlags = "SystemUIFlags";
    public const string TextureAnisotropy = "TextureAnisotropy";
    public const string TextureFilterMode = "TextureFilterMode";
    public const string TextureQuality = "TextureQuality";
    public const string TimeOut = "TimeOut";
    public const string TouchEmulation = "TouchEmulation";
    public const string TweakD3D12 = "TweakD3D12";
    public const string TweakVulkan = "TweakVulkan";
    public const string ValidateShaders = "ValidateShaders";
    public const string VSync = "VSync";
    public const string WindowHeight = "WindowHeight";
    public const string WindowIcon = "WindowIcon";
    public const string WindowMaximize = "WindowMaximize";
    public const string WindowPositionX = "WindowPositionX";
    public const string WindowPositionY = "WindowPositionY";
    public const string WindowResizable = "WindowResizable";
    public const string WindowTitle = "WindowTitle";
    public const string WindowWidth = "WindowWidth";
    public const string WorkerThreads = "WorkerThreads";
    public const string XR = "XR";

    public const string IsRunningInEditor = "IsRunningInEditor";
    public const string SceneName = "SceneName";
    public const string ScenePosition = "ScenePosition";
    public const string SceneRotation = "SceneRotation";
}
