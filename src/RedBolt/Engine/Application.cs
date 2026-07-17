using Microsoft.Extensions.Logging;

namespace RedBolt;

public abstract class Application : Object
{
    public const int FailureExitCode = 1;

    private string _startupErrors = string.Empty;

    protected Application()
    {
        Engine = new Engine();
        SubscribeToEvent<IOEvents.LogMessage>(HandleLogMessage);
    }

    public override void Dispose()
    {
        Engine.Dispose();
        base.Dispose();
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

}
