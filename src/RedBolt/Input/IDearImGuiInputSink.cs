namespace RedBolt;

public interface IDearImGuiInputSink
{
    bool ProcessEvent(nint sdlEvent);

    bool WantCaptureKeyboard { get; }

    bool WantCaptureMouse { get; }
}

