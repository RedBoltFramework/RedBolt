using RedBolt.Engine;

namespace RedBolt;

public abstract partial class Application
{
    static Application()
    {
        LinuxPlatform.Initialize();
    }

    private partial void InitializeMainLoop()
    {
    }

    private partial bool RunMainLoop()
    {
        var frameCount = 0;
        while (!Engine.IsExiting)
        {
            if (frameCount >= MaxFrameCount)
            {
                ErrorExit($"Application did not exit within {MaxFrameCount} frames.");
                break;
            }

            Engine.RunFrame(_updateAction);
            ++frameCount;
        }

        return false;
    }
}
