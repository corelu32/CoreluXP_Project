using static SDL.SDL3;
using SDL;
using CoreluXP.Primitives;
using CoreluXP.Mathematics;
using CoreluXP.Graphics;

namespace CoreluXP.Core;

/// <summary>
///   Application window and renderer. 
/// </summary>

public unsafe sealed class Application : IDisposable
{
    public readonly struct ApplicationSdlContext(Application app)
    {
        private readonly Application _app = app;
        public SDL_Window*   GetWindow()   => _app._window;
        public SDL_Renderer* GetRenderer() => _app._renderer;
    }
    
    private SDL_Window*    _window;
    private SDL_Renderer*  _renderer;
    private readonly Clock _clock;

    public event Action?          OnStart;
    public event Action?          OnQuit;
    public event Action<KeyCode>? OnKeyDown;
    public event Action<float>?   OnUpdate;
    
    public string Title           { get; private set; }
    public int    DefaultWidth    { get; private set; }
    public int    DefaultHeight   { get; private set; }
    public bool   IsRunning       { get; private set; }
    public float? TargetFramerate { get; private set; } = 60.0f;
    public bool   VSyncEnabled    { get; private set; } = false;
    
    public SubsystemProfile SubsystemProfile { get; set; }
    public ApplicationSdlContext SdlContext { get; private set; }
    
    public Application(
        string title,
        int width,
        int height,
        SubsystemProfile profile)
    {
        Title            = title;
        DefaultWidth     = width;
        DefaultHeight    = height;
        SubsystemProfile = profile;
        IsRunning        = false;

        SdlContext = new(this);
        _clock = new Clock();
    }
    
    public void Run()
    {
        Initialize();
        OnStart?.Invoke();
        IsRunning = true;
        
        while (IsRunning)
        {
            _clock.Restart();
            
            PollEvents();

            SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL_RenderClear(_renderer);
            
            OnUpdate?.Invoke(_clock.ComputeDelta());
            SDL_RenderPresent(_renderer);

            if (!VSyncEnabled && TargetFramerate is not null)
                _clock.RegulateFramerate(TargetFramerate.Value);
        }

        OnQuit?.Invoke();
        Destroy();
    }

    public void Stop()
        => IsRunning = false;

    public void SetTargetFramerate(float? framerate)
        => TargetFramerate = framerate;

    public void EnableVSync(bool enabled)
    {
        var state = enabled
                        ? SDL_RENDERER_VSYNC_ADAPTIVE
                        : SDL_RENDERER_VSYNC_DISABLED;
        
        if (!SDL_SetRenderVSync(_renderer, state))
            throw new ApplicationException($"Failed to {(enabled ? "enable" : "disable")} vsync. {SDL_GetError()}");
    }

    public void Draw(IDrawable drawable) => drawable.Draw(this);
    
    public void WriteDebug(string text, float left = 10, float top = 10)
    {
        byte r, g, b, a;
        SDL_GetRenderDrawColor(_renderer, &r, &g, &b, &a);
        SDL_SetRenderDrawColor(_renderer, 255, 255, 255, 255);
        SDL_RenderDebugText(_renderer, left, top, text);
        SDL_SetRenderDrawColor(_renderer, r, g, b, a);
    }

    private void PollEvents()
    {
        SDL_Event e;

        while (SDL_PollEvent(&e))
        {
            switch ((SDL_EventType)e.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    OnQuit?.Invoke();
                    Stop();
                    break;

                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                    OnKeyDown?.Invoke((KeyCode)e.key.key);
                    break;
            }
        }
    }
    
    private void Initialize()
    {
        // Initialize SDL3.
        if (!SDL_Init((SDL_InitFlags) SubsystemProfile.AsUint32()))
            throw new ApplicationException($"Failed to initialize SDL3. {SDL_GetError()}");

        // Create the window.
        _window = SDL_CreateWindow(Title, DefaultWidth, DefaultHeight, SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        if (_window is null)
            throw new ApplicationException($"Failed to create the window. {SDL_GetError()}");

        // Create the renderer.
        _renderer = SDL_CreateRenderer(_window, (byte*)null);
        if (_renderer is null)
            throw new ApplicationException($"Failed to create the renderer. {SDL_GetError()}");

        SDL_RenderClear(_renderer);
        SDL_ShowWindow(_window);
    }

    private void Destroy()
    {
        // Destroy the renderer.
        if (_renderer is not null)
            SDL_DestroyRenderer(_renderer);

        // Destroy the window.
        if (_window is not null)
            SDL_DestroyWindow(_window);

        // Quit SDL3.
        SDL_Quit();

        _renderer = null;
        _window = null;
    }
    
    public void Dispose() => Destroy();
}



internal sealed class Clock
{
    private ulong _frameLastNs;
    private ulong _frameStartNs;

    public Clock()
    {
        _frameLastNs  = SDL_GetTicksNS();
        _frameStartNs = _frameLastNs;
    }

    /// <summary>
    ///   Captures the start time for the current frame.
    /// </summary>
    
    public void Restart()
    {
        _frameStartNs = SDL_GetTicksNS();
    }

    /// <summary>
    ///   Calculates the time elapsed since the last frame in seconds.
    /// </summary>
    
    public float ComputeDelta()
    {
        ulong deltaNs = _frameStartNs - _frameLastNs;
        _frameLastNs  = _frameStartNs;
        
        return deltaNs / UnitConversions.NsPerSecond;
    }

    /// <summary>
    ///   Delays the thread to maintain a target framerate.
    /// </summary>
    
    public void RegulateFramerate(float framerate)
    {
        ulong targetFrameTimeNs = (ulong)(UnitConversions.NsPerSecond / framerate);
        ulong workDoneNs        = SDL_GetTicksNS() - _frameStartNs;

        if (workDoneNs < targetFrameTimeNs)
            SDL_DelayNS(targetFrameTimeNs - workDoneNs);
    }
}