using RedBolt;

using RedBolt.Engine;
using RedBolt.Logging;
namespace RedBolt.Tests;


public sealed class ApplicationCommandLineTests
{
    [Fact]
    public void Application_applies_engine_command_line_parameters()
    {
        using var application = new ProbeApplication();

        var exitCode = application.Run(
        [
            "--headless",
            "--full-screen",
            "--windowed",
            "--width=1234",
            "--height=567",
            "--touch",
            "--timeout=7",
            "--screenshot-on-exit=/tmp/redbolt-test.png",
            "--screenshot-parameters=3,2",
            "--app-name=RedBolt.Tests.Override",
            "--no-log-rotate-on-open",
            "--log=warn",
            "--log-rotate-by-size",
            "--log-rotate-max-files=7",
            "--log-rotate-max-size=4096",
            "--no-load-fonts"
        ]);

        Assert.Equal(0, exitCode);
        Assert.NotNull(application.ObservedParameters);
        Assert.True(application.Started);
        Assert.True(application.ObservedParameters!.Headless);
        Assert.False(application.ObservedParameters.Borderless);
        Assert.True(application.ObservedParameters.FullScreen);
        Assert.False(application.ObservedParameters.LogRotateOnOpen);
        Assert.Equal(LogLevel.Warning, application.ObservedParameters.LogLevel);
        Assert.True(application.ObservedParameters.LogRotateBySize);
        Assert.Equal(7u, application.ObservedParameters.LogMaxFiles);
        Assert.Equal(4096u, application.ObservedParameters.LogMaxSize);
        Assert.Equal(1234, application.ObservedParameters.WindowWidth);
        Assert.Equal(567, application.ObservedParameters.WindowHeight);
        Assert.True(application.ObservedParameters.TouchEmulation);
        Assert.Equal(7, application.ObservedParameters.TimeOut);
        Assert.Equal("/tmp/redbolt-test.png", application.ObservedParameters.ScreenshotOnExit);
        Assert.Equal(3, application.ObservedParameters.ScreenshotCount);
        Assert.Equal(2, application.ObservedParameters.ScreenshotFrameDelay);
        Assert.Equal("RedBolt.Tests.Override", application.ObservedParameters.ApplicationName);
        Assert.False(application.ObservedParameters.LoadFonts);
    }

    [Fact]
    public void Application_rejects_unknown_command_line_parameters()
    {
        using var application = new ProbeApplication();

        var exitCode = application.Run(["--does-not-exist"]);

        Assert.Equal(Application.FailureExitCode, exitCode);
        Assert.Equal("Unknown option '--does-not-exist'.", application.ErrorMessage);
        Assert.False(application.Started);
    }

    private sealed class ProbeApplication : Application
    {
        public EngineParameters? ObservedParameters { get; private set; }

        public bool Started { get; private set; }

        protected override void Setup(EngineParameters parameters)
        {
            parameters.ApplicationName = "RedBolt.Tests.Probe";
            parameters.Headless = true;
        }

        protected override void Start()
        {
            Started = true;
            ObservedParameters = new EngineParameters
            {
                ApplicationName = Engine.Parameters.ApplicationName,
                Borderless = Engine.Parameters.Borderless,
                FullScreen = Engine.Parameters.FullScreen,
                Headless = Engine.Parameters.Headless,
                LoadFonts = Engine.Parameters.LoadFonts,
                LogMaxFiles = Engine.Parameters.LogMaxFiles,
                LogMaxSize = Engine.Parameters.LogMaxSize,
                LogLevel = Engine.Parameters.LogLevel,
                LogRotateBySize = Engine.Parameters.LogRotateBySize,
                LogRotateOnOpen = Engine.Parameters.LogRotateOnOpen,
                ScreenshotCount = Engine.Parameters.ScreenshotCount,
                ScreenshotFrameDelay = Engine.Parameters.ScreenshotFrameDelay,
                ScreenshotOnExit = Engine.Parameters.ScreenshotOnExit,
                TimeOut = Engine.Parameters.TimeOut,
                TouchEmulation = Engine.Parameters.TouchEmulation,
                WindowHeight = Engine.Parameters.WindowHeight,
                WindowWidth = Engine.Parameters.WindowWidth
            };
            Exit();
        }
    }
}
