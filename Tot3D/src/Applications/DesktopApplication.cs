using System.Diagnostics;
using Tot3D.Input;
using Tot3D.Primitives;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Tot3D.Applications;

public sealed class DesktopApplication : IApplication
{
    private readonly Sdl2Window _window;

    public uint Width => (uint)_window.Width;
    public uint Height => (uint)_window.Height;
    public PlatformType PlatformType => PlatformType.Desktop;

    public event Action<float>? Rendering;
    public event Action? Resized;
    public event Action<KeyEvent>? KeyPressed;

    private bool _windowResized = true;
    
    public DesktopApplication(
        string title,
        uint width,
        uint height)
    {
        WindowCreateInfo wci = new()
        {
            X = 100,
            Y = 100,
            WindowWidth = (int)width,
            WindowHeight = (int)height,
            WindowTitle = title
        };

        _window = VeldridStartup.CreateWindow(ref wci);
        _window.Resized += () => _windowResized = true;
        _window.KeyDown += OnKeyDown;
    }

    public void Run()
    {
        Stopwatch clock = Stopwatch.StartNew();
        double previousElapsed = clock.Elapsed.TotalSeconds;

        while (_window.Exists)
        {
            double newElapsed = clock.Elapsed.TotalSeconds;
            float deltaTime = (float)(newElapsed - previousElapsed);

            InputSnapshot inputSnapshot = _window.PumpEvents();
            InputTracker.UpdateFrameInput(inputSnapshot);

            if (_window.Exists)
            {
                previousElapsed = newElapsed;

                if (_windowResized)
                {
                    _windowResized = false;
                    Resized?.Invoke();
                }

                Rendering?.Invoke(deltaTime);
            }
        }
    }

    private void OnKeyDown(KeyEvent keyEvent)
    {
        KeyPressed?.Invoke(keyEvent);
    }
}