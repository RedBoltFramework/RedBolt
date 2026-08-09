namespace RedBolt.Platform;

using RedBolt.Engine;

public abstract partial class Application
{
    static Application()
    {
        WindowsPlatform.Initialize();
    }

    private partial void InitializeMainLoop()
    {
    }

    private partial bool RunMainLoop()
    {
        while (!Engine.IsExiting)
            Engine.RunFrame(_updateAction);

        return false;
    }
}
