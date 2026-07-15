using Microsoft.Extensions.Logging;

namespace RedBolt;

public abstract class Application : Object
{
    public const int FailureExitCode = 1;

    private string _startupErrors = string.Empty;

    // C++: StringVariantMap commandLineParameters_;

    protected Application()
    {
        // C++: SubscribeToEvent(E_LOGMESSAGE, URHO3D_HANDLER(Application, HandleLogMessage));
        Engine = new Engine();
    }

    public int ExitCode { get; private set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public int MaxFrameCount { get; set; } = 10_000;

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
        Setup(parameters);

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
                Log.Logger.LogError(parseResult.ErrorMessage);
                Console.Error.WriteLine(parseResult.ErrorMessage);
                Console.Error.WriteLine();
                Console.Error.Write(parser.GetUsage());
                return ExitCode;
            }
        }

        Log.Initialize(parameters);

        if (ShouldStopBeforeInitialization())
            return ExitCode;

        var started = false;
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
            {
                var frameCount = 0;
                Action<float> updateAction = Update;
                while (!Engine.IsExiting)
                {
                    if (frameCount >= MaxFrameCount)
                    {
                        ErrorExit($"Application did not exit within {MaxFrameCount} frames.");
                        break;
                    }

                    Engine.RunFrame(updateAction);
                    ++frameCount;
                }
            }

        }
        catch (Exception exception)
        {
            Log.Logger.LogCritical(exception, "Unhandled exception in application run loop.");
            ErrorExit(exception.Message);
        }
        finally
        {
            if (started)
            {
                CaptureScreenshotOnExit();
                Stop();
                SendEvent<EngineEvents.ApplicationStoppedEvent>();
            }

            if (Engine.IsInitialized)
                Engine.Shutdown();
            Log.Shutdown();
        }

        return ExitCode;
    }

    protected void Exit(int exitCode = 0)
    {
        ExitCode = exitCode;
        Engine.Exit();
    }

    // C++: void HandleLogMessage(StringHash eventType, VariantMap& eventData);
    //       Accumulates LOG_ERROR messages into startupErrors_ (strips timestamp).
    // TODO: port - C++ code:
    // void Application::HandleLogMessage(StringHash eventType, VariantMap& eventData)
    // {
    //     using namespace LogMessage;
    //     if (eventData[P_LEVEL].GetInt() == LOG_ERROR)
    //     {
    //         ea::string error = eventData[P_MESSAGE].GetString();
    //         unsigned bracketPos = error.find(']');
    //         if (bracketPos != ea::string::npos)
    //             error = error.substr(bracketPos + 2);
    //         startupErrors_ += error + "\n";
    //     }
    // }

    protected void ErrorExit(string message = "")
    {
        // C++: if message is empty, uses startupErrors_ (collected log errors) or fallback string.
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Application has been terminated due to unexpected error."
            : message;
        Log.Logger.LogError(ErrorMessage);
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

    private void CaptureScreenshotOnExit()
    {
        var screenshotPath = Engine.Parameters.ScreenshotOnExit;
        if (Engine.IsHeadless || string.IsNullOrWhiteSpace(screenshotPath) || !Graphics.IsInitialized)
            return;

        try
        {
            if (Renderer.GetNumViews() > 0)
                Renderer.RenderFrame(Graphics, screenshotPath: screenshotPath);
            else
                Graphics.SaveScreenshot(screenshotPath);
        }
        catch (Exception exception)
        {
            Log.Logger.LogWarning(exception, "Failed to save screenshot to '{ScreenshotPath}'.", screenshotPath);
        }
    }

}
