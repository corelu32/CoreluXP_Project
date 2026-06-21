using LUmaKE.Primitives;
using SDL3;

namespace LUmaKE.Platforms;

public partial class SdlPlatform : IPlatform
{
    /// <summary>
    ///   Poll SDL events.
    /// </summary>
    private void PollEvents(SdlWindowContext context)
    {
        while (SDL.PollEvent(out var @event))
        {
            switch ((SDL.EventType) @event.Type)
            {
                // ON QUIT
                case SDL.EventType.Quit:
                    context.Window.SignalClose();
                    break;

                // ON KEY DOWN
                case SDL.EventType.KeyDown:
                    context.Window.SignalKeyDown((Keycode)@event.Key.Key);
                    break;
            }
        }
    }
}