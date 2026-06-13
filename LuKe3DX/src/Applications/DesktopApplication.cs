using LuKe3DX.Mathematics;
using LuKe3DX.Primitives;
using SDL3;

namespace LuKe3DX.Applications;

public sealed class DesktopApplication
{
    private nint _window = 0;
    private nint _renderer = 0;
    
    private readonly Clock _clock = new();
    
    private bool _isInitialized = false;
    private bool _isRunning = false;
    
    private string _title = "LuKe3DX Application";
    private int    _width = 800;
    private int    _height = 600;
    private float? _targetFramerate = 60;
    private bool   _vsyncEnabled = false;

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            
            if (_isInitialized)
                SDL.SetWindowTitle(_window, value);
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            _width = value;
            
            if (_isInitialized)
                SDL.SetWindowSize(_window, value, _height);
        }
    }

    public int Height
    {
        get => _width;
        set
        {
            _height = value;
            
            if (_isInitialized)
                SDL.SetWindowSize(_window, _width, value);
        }
    }
    
    public float? TargetFramerate
    {
        get => _targetFramerate == 0 ? null : _targetFramerate;
        set
        {
            if (value is not null && value < 0)
                throw new Exception($"The target framerate cannot be configured below zero.");
            
            _targetFramerate = value;
        }
    }

    public bool VSyncEnabled
    {
        get => _vsyncEnabled;
        set
        {
            if (_vsyncEnabled == value)
                return;
            
            if (!_isInitialized)
                throw new Exception("Please initialize the application before configuring the VSync state.");

            _vsyncEnabled = value;
            
            if (!SDL.SetRenderVSync(_renderer, value ? -1 : 0))
                throw new ApplicationException($"Failed to {(value ? "enable" : "disable")} vsync. {SDL.GetError()}");
        }
    }

    public void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
        
        if (_isInitialized)
            SDL.SetWindowSize(_window, width, height);
    }

    public event Action?          OnStart;
    public event Action?          OnQuit;
    public event Action<Keycode>? OnKeyDown;
    public event Action<float>?   OnUpdate;
    
    public void Run()
    {
        Initialize();
        OnStart?.Invoke();
        _isRunning = true;

        while (_isRunning)
        {
            _clock.Restart();
            PollEvents();

            SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
            SDL.RenderClear(_renderer);

            OnUpdate?.Invoke(_clock.ComputeDelta());
            SDL.RenderPresent(_renderer);

            if (!_vsyncEnabled && _targetFramerate is not null)
                _clock.RegulateFramerate(_targetFramerate.Value);
        }

        OnQuit?.Invoke();
        Destroy();
    }

    public void Stop()
        => _isRunning = false;

    public void Destroy()
    {
        if (!_isInitialized)
            return;
        
        if (_renderer != 0)
            SDL.DestroyRenderer(_renderer);

        if (_window != 0)
            SDL.DestroyWindow(_window);

        SDL.Quit();

        _renderer = 0;
        _window = 0;
        _isInitialized = false;
    }
        
    private void Initialize()
    {
        // Initialize SDL
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            throw new Exception($"Failed to initialize SDL: {SDL.GetError()}");

        if (!SDL.CreateWindowAndRenderer(
            _title,
            _width,
            _height,
            SDL.WindowFlags.Hidden,
            out var window,
            out var renderer))
                throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        SDL.RenderClear(renderer);
        SDL.ShowWindow(window);

        _renderer = renderer;
        _window = window;
        _isInitialized = true;
    }

    private void PollEvents()
    {
        while (SDL.PollEvent(out var @event))
        {
            switch ((SDL.EventType) @event.Type)
            {
                // ON QUIT
                case SDL.EventType.Quit:
                    OnQuit?.Invoke();
                    Stop();
                    break;

                // ON KEY DOWN
                case SDL.EventType.KeyDown:
                    OnKeyDown?.Invoke((Keycode)@event.Key.Key);
                    break;
            }
        }
    }
}

internal sealed class Clock
{
    private ulong _frameLastNs;
    private ulong _frameStartNs;

    public Clock()
    {
        _frameLastNs  = SDL.GetTicksNS();
        _frameStartNs = _frameLastNs;
    }

    /// <summary>
    ///   Captures the start time for the current frame.
    /// </summary>
    
    public void Restart()
    {
        _frameStartNs = SDL.GetTicksNS();
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
        ulong workDoneNs        = SDL.GetTicksNS() - _frameStartNs;

        if (workDoneNs < targetFrameTimeNs)
            SDL.DelayNS(targetFrameTimeNs - workDoneNs);
    }
}