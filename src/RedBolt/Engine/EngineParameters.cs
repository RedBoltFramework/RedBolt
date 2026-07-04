namespace RedBolt;

public sealed class EngineParameters
{
    public string ApplicationName { get; set; } = "RedBolt";

    public GraphicsBackend GraphicsBackend { get; set; } = GraphicsBackend.Auto;

    public bool Headless { get; set; }

    public int WindowHeight { get; set; } = 720;

    public string WindowTitle { get; set; } = "RedBolt";

    public int WindowWidth { get; set; } = 1280;

    internal EngineParameters Clone()
    {
        return new EngineParameters
        {
            ApplicationName = ApplicationName,
            GraphicsBackend = GraphicsBackend,
            Headless = Headless,
            WindowHeight = WindowHeight,
            WindowTitle = WindowTitle,
            WindowWidth = WindowWidth
        };
    }
}
