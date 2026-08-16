// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Engine;

using Microsoft.Extensions.Logging;
using System;
using System.Linq;
#if REDBOLT_PLATFORM_BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;
#endif

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Logging;
using LogLevel = global::RedBolt.Logging.LogLevel;
using RedBolt.Resource;

public abstract partial class Application : RedObject
{
    public const int FailureExitCode = 1;

    private string _startupErrors = string.Empty;
    private readonly Action<float> _updateAction;
#if REDBOLT_PLATFORM_BROWSER
    private static Application _currentApplication = null!;
#endif

    protected Application()
    {
        Engine = new Engine();
        _updateAction = Update;
#if REDBOLT_PLATFORM_BROWSER
        _currentApplication = this;
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(HandleScreenMode);
#endif
        SubscribeToEvent<IOEvents.LogMessage>(HandleLogMessage);
    }

    public override void Dispose()
    {
        Engine.Dispose();
        base.Dispose();
    }

    public int ExitCode { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    protected Engine Engine { get; }

    protected ResourceCache Resources => GetSubsystem<ResourceCache>()!;

    protected Graphics Graphics => GetSubsystem<Graphics>()!;

    protected Renderer Renderer => GetSubsystem<Renderer>()!;

    protected Input Input => GetSubsystem<Input>()!;

    protected virtual void Setup(EngineParameters parameters)
    {
    }

    protected virtual void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update(float timeStep)
    {
    }

    protected virtual void Stop()
    {
    }

    public int Run()
    {
        return Run(Environment.GetCommandLineArgs().Skip(1).ToArray());
    }

    public int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parameters = new EngineParameters();
#if REDBOLT_PLATFORM_BROWSER
        parameters.WindowWidth = GetCanvasWidth();
        parameters.WindowHeight = GetCanvasHeight();
#endif
        Setup(parameters);
#if REDBOLT_PLATFORM_ANDROID
        parameters.ApplicationPreferencesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            parameters.ApplicationName) + Path.DirectorySeparatorChar;
        parameters.Borderless = false;
        parameters.EngineCliParameters = false;
        parameters.FullScreen = true;
        parameters.ResourcePrefixPaths = "/apk";
        parameters.WindowMaximize = false;
        parameters.WindowResizable = false;
        if (args.Length != 0)
            parameters.EngineCliParameters = true;
#endif

        if (parameters.EngineCliParameters)
        {
            EngineCommandLine.ApplyEnvironmentDefaults(parameters);

            var parser = new CommandLineParser(parameters.ApplicationName);
            EngineCommandLine.Register(parser, parameters);
            SetupCommandLine(parser, parameters);

            var parseResult = parser.Parse(args);
            if (parseResult.IsHelpRequested)
            {
                Console.Write(parser.GetUsage());
                return ExitCode;
            }

            if (!parseResult.IsSuccess)
            {
                ErrorExit(parseResult.ErrorMessage);
                Log.GetLogger().LogError(parseResult.ErrorMessage);
                Console.Error.WriteLine(parseResult.ErrorMessage);
                Console.Error.WriteLine();
                Console.Error.Write(parser.GetUsage());
                return ExitCode;
            }
        }

        if (ShouldStopBeforeInitialization())
            return ExitCode;

        var started = false;
        var mainLoopStarted = false;
        try
        {
            if (!Engine.Initialize(parameters))
            {
                ErrorExit();
                SendEvent<EngineEvents.ApplicationStoppedEvent>();
                return ExitCode;
            }

            Start();
            started = true;
            SendEvent<EngineEvents.ApplicationStartedEvent>();

            if (!ShouldStopAfterStart())
                mainLoopStarted = RunMainLoop();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            Log.GetLogger().LogCritical(exception, "Unhandled exception in application run loop.");
#if REDBOLT_PLATFORM_BROWSER
            throw;
#else
            ErrorExit(exception.Message);
#endif
        }
        finally
        {
            if (started && !mainLoopStarted)
            {
                Stop();
                SendEvent<EngineEvents.ApplicationStoppedEvent>();
            }
        }

        return ExitCode;
    }

    protected void Exit(int exitCode = 0)
    {
        ExitCode = exitCode;
        Engine.Exit();
    }

    protected void HandleLogMessage(ref IOEvents.LogMessage eventData)
    {
        if (eventData.Level == (int)LogLevel.Error)
        {
            var error = eventData.Message;
            var bracketPos = error.IndexOf(']');
            if (bracketPos != -1)
                error = error[(bracketPos + 2)..];
            _startupErrors += error + "\n";
        }
    }

    protected void ErrorExit(string message = "")
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? string.IsNullOrEmpty(_startupErrors)
                ? "Application has been terminated due to unexpected error."
                : _startupErrors
            : message;
        Log.GetLogger().LogError(ErrorMessage);
        Exit(FailureExitCode);
    }

    private bool ShouldStopBeforeInitialization()
    {
        return ExitCode != 0 || Engine.IsExiting;
    }

    private bool ShouldStopAfterStart()
    {
        return ExitCode != 0 || Engine.IsExiting;
    }

    private bool RunMainLoop()
    {
#if REDBOLT_PLATFORM_BROWSER
        SetCanvasResizeHandlers();
        SetMainLoop();
        return true;
#else
        while (!Engine.IsExiting)
            Engine.RunFrame(_updateAction);

        return false;
#endif
    }

#if REDBOLT_PLATFORM_BROWSER
    private void RunFrame()
    {
        try
        {
            Engine.RunFrame(_updateAction);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            throw;
        }
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    internal static Task RunFrameAsync()
    {
        _currentApplication.RunFrame();
        return Task.CompletedTask;
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    internal static Task ResizeCanvasAsync(int width, int height, bool fullscreen, float dpiScale)
    {
        _currentApplication.Engine.OnCanvasResize(width, height, fullscreen, dpiScale);
        return Task.CompletedTask;
    }

    private static void HandleScreenMode(ref GraphicsEvents.ScreenModeEvent eventData)
    {
        SetRendererSize(eventData.Width, eventData.Height);
    }

    [JSImport("canvas.getWidth", "main.js")]
    private static partial int GetCanvasWidth();

    [JSImport("canvas.getHeight", "main.js")]
    private static partial int GetCanvasHeight();

    [JSImport("canvas.setResizeHandlers", "main.js")]
    private static partial void SetCanvasResizeHandlers();

    [JSImport("canvas.setRendererSize", "main.js")]
    private static partial void SetRendererSize(int width, int height);

    [JSImport("mainLoop.set", "main.js")]
    private static partial void SetMainLoop();
#endif
}
