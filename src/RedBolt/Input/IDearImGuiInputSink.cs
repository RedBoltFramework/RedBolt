// C++: no direct C++ counterpart - adapter class
namespace RedBolt;

public interface IDearImGuiInputSink
{
    bool ProcessEvent(nint sdlEvent);

    bool WantCaptureKeyboard { get; }

    bool WantCaptureMouse { get; }
}

