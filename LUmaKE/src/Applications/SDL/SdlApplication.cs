using System.Diagnostics;
using LUmaKE.Graphics.Gpu;
using LUmaKE.Mathematics;
using LUmaKE.Primitives;
using SDL3;

namespace LUmaKE.Applications;

/// <summary>
///   Work with an SDL backend. Allows for registration of multiple application windows.
/// </summary>
public sealed class SdlApplication : IApplication
{
    private IntPtr _windowHandle;
    private IntPtr _gpuHandle;

    private readonly Clock _clock = new();
    
    private bool         _running         = false;
    private string       _title           = "Untitled";
    private Vector2<int> _position        = new(0, 0);
    private Vector2<int> _size            = new(800, 600);
    private double?      _targetFramerate = 60.0;
    private bool         _vsyncEnabled    = false;

    public bool Running => _running;
    
    public event Action?          OnStart;
    public event Action?          OnClose;
    public event Action<Keycode>? OnKeyDown;
    public event Action<double>?  OnUpdate;
    public event Action<double>?  OnRender;
    
    /// <summary>
    ///   The window title.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
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
        }
    }

    internal SdlApplication(string title, int width, int height)
    {
        _title = title;
        _size  = new(width, height);
    }

    public void Run()
    {
        Initialize();
        OnStart?.Invoke();
        _running = true;

        while (_running)
        {
            double delta = _clock.ComputeDelta();

            Update(delta);

            if (!_vsyncEnabled && _targetFramerate is not null)
                _clock.RegulateFramerate(_targetFramerate.Value);

            _clock.CommitFrame();
        }

        OnClose?.Invoke();
        Destroy();
    }

    public void Close()
    {
        _running = false;
    }
    
    public void LoadPipeline(GpuPipeline pipeline)
        => throw new NotImplementedException();

    public void BindPipeline(GpuPipeline pipeline)
        => throw new NotImplementedException();

    public void UnloadPipeline(GpuPipeline pipeline)
        => throw new NotImplementedException();

    public void LoadBuffer(GpuBuffer buffer)
        => throw new NotImplementedException();

    public void UnloadBuffer(GpuBuffer buffer)
        => throw new NotImplementedException();
    
    private void Initialize()
    {
        // Initialize SDL.
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            throw new Exception($"Failed to initialize SDL: {SDL.GetError()}");

        // Initialize a window.
        IntPtr windowHandle = SDL.CreateWindow(
            _title,
            _size.X,
            _size.Y,
            SDL.WindowFlags.Hidden);
        
        if (windowHandle == IntPtr.Zero)
            throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        // Initialize the GPU device.
        IntPtr gpuDeviceHandle = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.MSL | SDL.GPUShaderFormat.DXIL,
            debugMode : false,
            name      : null);

        if (gpuDeviceHandle == IntPtr.Zero)
            throw new Exception($"Failed to create the GPU device: {SDL.GetError()}");

        // Bind the window to the GPU device.
        if (!SDL.ClaimWindowForGPUDevice(gpuDeviceHandle, windowHandle))
            throw new Exception($"Failed to bind GPU device to window: {SDL.GetError()}");

        // Show window post-init.
        SDL.ShowWindow(windowHandle);
    }

    private void Update(double delta)
    {
        PollEvents();

        // Signal window update.
        OnUpdate?.Invoke(delta);

        // Acquire a GPU command buffer.
        IntPtr commandBuffer = SDL.AcquireGPUCommandBuffer(_gpuHandle);
        if (commandBuffer == IntPtr.Zero)
            return;

        // Acquire the swapchain texture for the current frame.
        if (SDL.AcquireGPUSwapchainTexture(
            commandBuffer,
            _windowHandle,
            out IntPtr swapchainTexture,
            out uint width,
            out uint height))
        {
            if (swapchainTexture != IntPtr.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                    LoadOp     = SDL.GPULoadOp.Clear,
                    StoreOp    = SDL.GPUStoreOp.Store,
                    Texture    = swapchainTexture
                };

                IntPtr renderPass;

                unsafe
                {
                    renderPass = SDL.BeginGPURenderPass(commandBuffer, (IntPtr)(&colorTargetInfo), 1, IntPtr.Zero);
                }

                // Signal the window's render event.
                OnRender?.Invoke(delta);

                // End the GPU render pass.
                SDL.EndGPURenderPass(renderPass);
            }
        }

        SDL.SubmitGPUCommandBuffer(commandBuffer);
    }

    private void Destroy()
    {
        SDL.ReleaseWindowFromGPUDevice(_gpuHandle, _windowHandle);

        if (_gpuHandle != IntPtr.Zero)
            SDL.DestroyGPUDevice(_gpuHandle);

        if (_windowHandle != IntPtr.Zero)
            SDL.DestroyWindow(_windowHandle);

        _gpuHandle = IntPtr.Zero;
        _windowHandle = IntPtr.Zero;
        SDL.Quit();
    }

    private void PollEvents()
    {
        while (SDL.PollEvent(out var @event))
        {
            switch ((SDL.EventType) @event.Type)
            {
                // ON QUIT
                case SDL.EventType.Quit:
                    Close();
                    break;

                // ON KEY DOWN
                case SDL.EventType.KeyDown:
                    OnKeyDown?.Invoke((Keycode)@event.Key.Key);
                    break;
            }
        }
    }
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

    public long FrameStartTick { get => _frameStartTick; }
    
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