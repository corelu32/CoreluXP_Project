using System.Diagnostics;
using LUmosaiKE.Graphics;
using LUmosaiKE.Graphics.Gpu;
using LUmosaiKE.Mathematics;
using LUmosaiKE.Primitives;

namespace LUmosaiKE.Core;

/// <summary>
///   Represents a window or viewport accompanied with powerful event handling.
///   Can be configured to regulate a target framerate.
/// </summary>
public sealed class Window
{
    private readonly Clock _clock = new();
    
    private bool         _running         = false;
    private string       _title           = "Untitled";
    private Vector2<int> _position        = new(0, 0);
    private Vector2<int> _size            = new(800, 600);
    private double?      _targetFramerate = 60.0;
    private bool         _vsyncEnabled    = false;

    /// <summary>
    ///   The window title.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnTitleChange?.Invoke(value);
        }
    }

    /// <summary>
    ///   The window position.
    /// </summary>
    public Vector2<int> Position
    {
        get => _position;
        set
        {
            _position = value;
            OnMove?.Invoke(value.X, value.Y);
        }
    }

    /// <summary>
    ///   The window size.
    /// </summary>
    public Vector2<int> Size
    {
        get => _size;
        set
        {
            if (value.X < 0)
                throw new ArgumentException("The window width cannot be negative.");

            if (value.Y < 0)
                throw new ArgumentException("The window height cannot be negative.");
            
            _size = value;
            OnResize?.Invoke(value.X, value.Y);
        }
    }

    /// <summary>
    ///   The target framerate. 
    /// </summary>
    public double? TargetFramerate
    {
        get => _targetFramerate;
        set
        {
            if (value < 1)
                throw new ArgumentException("The target framerate cannot be configured below 1.");
            
            _targetFramerate = value;
            OnTargetFramerateChange?.Invoke(value);
        }
    }

    /// <summary>
    ///   Enable/disable V-Sync.
    /// </summary>
    public bool VSyncEnabled
    {
        get => _vsyncEnabled;
        set
        {
            _vsyncEnabled = value;
            OnVSyncChange?.Invoke(value);
        }
    }
    
    public void SignalKeyDown(Keycode keycode)
        => OnKeyDown?.Invoke(keycode);

    public void SignalUpdate(double delta)
        => OnUpdate?.Invoke(delta);

    public void SignalRender(double delta)
        => OnRender?.Invoke(delta);

    public void SignalClose()
        => OnClose?.Invoke();
    
    public event Action?              OnOpen;
    public event Action?              OnClose;
    public event Action<int, int>?    OnMove;
    public event Action<int, int>?    OnResize;
    public event Action<Keycode>?     OnKeyDown;
    public event Action<double>?      OnPlatformUpdate;
    public event Action<double>?      OnUpdate;
    public event Action<double>?      OnRender;
    public event Action<string>?      OnTitleChange;
    public event Action<double?>?     OnTargetFramerateChange;
    public event Action<bool>?        OnVSyncChange;
    public event Action<GpuPipeline>? OnNewGpuPipeline;

    public Window(string title = "New LUmosaiKE Project", int width = 800, int height = 600)
    {
        Title = title;
        Size  = new(width, height);
        TargetFramerate = 60.0;
    }
    
    /// <summary>
    ///   Run the window loop.
    /// </summary>
    public void Run()
    {
        OnOpen?.Invoke();
        _running = true;
        
        while (_running)
        {
            double delta = _clock.ComputeDelta();

            if (OnPlatformUpdate is not null)
                // If the platform update has subscribers, prioritize this call.
                OnPlatformUpdate.Invoke(delta);
            else
            {
                // If the platform update is unused, manually invoke OnUpdate and OnRender.
                OnUpdate?.Invoke(delta);
                OnRender?.Invoke(delta);
            }

            if (!_vsyncEnabled && _targetFramerate is not null)
                _clock.RegulateFramerate(_targetFramerate.Value);

            _clock.CommitFrame();
        }

        OnClose?.Invoke();
    }

    public void AddGpuPipeline(GpuPipeline pipeline)
        => OnNewGpuPipeline?.Invoke(pipeline);
}

/// <summary>
///   Regulates framerate and computes the delta time between frames.
/// </summary>
internal class Clock
{
    public readonly long HardwareFrequency;
    private readonly double _tickToSecondMultiplier;

    private long _frameStartTick;
    private long _previousTick;
    
    public Clock()
    {
        HardwareFrequency = Stopwatch.Frequency;
        _tickToSecondMultiplier = 1.0 / HardwareFrequency;
        
        long initial = Stopwatch.GetTimestamp();
        _previousTick = initial;
        _frameStartTick = initial;
    }

    /// <summary>
    ///   Computes the delta based on when the frame actually starts processing.
    /// </summary>
    public double ComputeDelta()
    {
        // Capture the moment the engine actually begins execution for this frame
        _frameStartTick = Stopwatch.GetTimestamp();

        // Calculate time passed since the previous frame completely finished (including any regulation/VSync stalls)
        double delta = (_frameStartTick - _previousTick) * _tickToSecondMultiplier;
        return delta;
    }

    /// <summary>
    ///   Regulate the framerate by pausing the main thread.
    /// </summary>
    public void RegulateFramerate(double fps)
    {
        long frameEndTick = Stopwatch.GetTimestamp();
        
        long targetTicks    = (long)((1.0 / fps) * HardwareFrequency);
        long usedTicks      = frameEndTick - _frameStartTick;
        long remainingTicks = targetTicks - usedTicks;

        if (remainingTicks > 0)
        {
            double msPerTick = 1000.0 / HardwareFrequency;
            double remainingTimeMs = remainingTicks * msPerTick;

            if (remainingTimeMs > 2.0)
                Thread.Sleep((int)(remainingTimeMs - 2.0));

            long targetTimestamp = _frameStartTick + targetTicks;
            while (Stopwatch.GetTimestamp() < targetTimestamp)
            {
                Thread.SpinWait(1);
            }
        }
    }

    /// <summary>
    ///   Commit the final frame by resetting the previous tick capture.
    /// </summary>
    public void CommitFrame()
    {
        // Anchor the baseline clock to the exact moment this frame finishes all work and waiting.
        _previousTick = Stopwatch.GetTimestamp();
    }
}