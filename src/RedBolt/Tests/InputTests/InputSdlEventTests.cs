namespace RedBolt.Tests;

using RedBolt.Bindings.Sdl;
using RedBolt.Input;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class InputSdlEventTests
{
    private readonly GraphicsEngineFixture _fixture;

    public InputSdlEventTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private Input Input => _fixture.Input;

    [Fact]
    public void Escape_key_does_not_request_exit()
    {
        InputEvents.KeyDownEvent keyDownEvent = default;
        var exitRequested = false;
        using var receiver = new TestEventReceiver();
        receiver.SubscribeToEvent<InputEvents.KeyDownEvent>(
            (ref InputEvents.KeyDownEvent value) => keyDownEvent = value);
        receiver.SubscribeToEvent<InputEvents.ExitRequestedEvent>(
            (ref InputEvents.ExitRequestedEvent _) => exitRequested = true);

        using var focusGained = new SdlEvent();
        focusGained.Type = (uint)SdlEventType.SdlWindowevent;
        focusGained.WindowEvent = (byte)SdlWindowEventID.SdlWindoweventFocusGained;
        Assert.Equal(1, SdlNativeBindings.SdlPushEvent(focusGained));

        using var keyDown = new SdlEvent();
        keyDown.Type = (uint)SdlEventType.SdlKeydown;
        keyDown.KeyKeysymScancode = SdlScancode.SdlScancodeEscape;
        keyDown.KeyKeysymSym = (int)SdlKeyCode.SdlkEscape;
        Assert.Equal(1, SdlNativeBindings.SdlPushEvent(keyDown));

        Input.BeginFrame();
        Input.PollEvents();

        Assert.Equal(Key.Escape, keyDownEvent.Key);
        Assert.False(exitRequested);
        Assert.False(Input.ExitRequested);

        using var keyUp = new SdlEvent();
        keyUp.Type = (uint)SdlEventType.SdlKeyup;
        keyUp.KeyKeysymScancode = SdlScancode.SdlScancodeEscape;
        keyUp.KeyKeysymSym = (int)SdlKeyCode.SdlkEscape;
        Assert.Equal(1, SdlNativeBindings.SdlPushEvent(keyUp));
        Input.PollEvents();
    }
}
