using LUmosaiKE.Graphics;
using LUmosaiKE.Mathematics;
using LUmosaiKE.Primitives;
using SDL3;

namespace LUmosaiKE.Applications;

public sealed class DesktopApplication
{
    public class ApplicationSdlContext(DesktopApplication app)
    {
        private readonly DesktopApplication _app = app;
        public IntPtr GetWindow()    => _app._window;
        public IntPtr GetGpuDevice() => _app._gpuDevice;
    }
    
    public ApplicationSdlContext SdlContext { get; private set; }
    
    private IntPtr _window    = IntPtr.Zero;
    private IntPtr _gpuDevice = IntPtr.Zero;
    
    private readonly Clock _clock = new();
    
    private bool _isInitialized = false;
    private bool _isRunning = false;
    
    private string _title = "LUmosaiKE Application";
    private int    _width = 800;
    private int    _height = 600;
    private float? _targetFramerate = 60;
    private bool   _vsyncEnabled = true; // The GPU device enabled vsync by default.

    public event Action?          OnStart;
    public event Action?          OnQuit;
    public event Action<Keycode>? OnKeyDown;
    public event Action<float>?   OnUpdate;
    public event Action<float>?   OnRender;
    
    public DesktopApplication()
    {
        SdlContext = new(this);
    }
    
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
        get => _height;
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

            _vsyncEnabled = value;
            
            // Define target swapchain canvas properties
            var composition = SDL.GPUSwapchainComposition.SDR;
            SDL.GPUPresentMode targetMode;
        
            if (_vsyncEnabled)
            {
                // Opt for Mailbox if available (lowest latency VSync), fallback to basic VSync
                if (SDL.WindowSupportsGPUPresentMode(_gpuDevice, _window, SDL.GPUPresentMode.Mailbox))
                {
                    targetMode = SDL.GPUPresentMode.Mailbox;
                }
                else
                {
                    targetMode = SDL.GPUPresentMode.VSync;
                }
            }
            else
            {
                // Turn VSync Off (Check if Immediate mode is supported by hardware driver)
                if (SDL.WindowSupportsGPUPresentMode(_gpuDevice, _window, SDL.GPUPresentMode.Immediate))
                {
                    targetMode = SDL.GPUPresentMode.Immediate;
                }
                else
                {
                    // If completely unsupported, default back to VSYNC safe baseline
                    targetMode = SDL.GPUPresentMode.VSync;
                }
            }
        
            // Apply the chosen present mode parameters to the swapchain context
            if (!SDL.SetGPUSwapchainParameters(_gpuDevice, _window, composition, targetMode))
                throw new Exception($"Failed to toggle vsync: ${SDL.GetError()}");
        }
    }

    public void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
        
        if (_isInitialized)
            SDL.SetWindowSize(_window, width, height);
    }
    
    public void Run()
    {
        Initialize();
        OnStart?.Invoke();
        _isRunning = true;

        while (_isRunning)
        {
            _clock.Restart();
            float delta = _clock.ComputeDelta();
            
            PollEvents();
            OnUpdate?.Invoke(delta);
            
            // Acquire a command buffer.
            IntPtr commandBuffer = SDL.AcquireGPUCommandBuffer(_gpuDevice);
            if (commandBuffer == IntPtr.Zero)
                continue;

            // Acquire the swapchain texture for the current frame.
            if (SDL.AcquireGPUSwapchainTexture(
                commandBuffer,
                _window,
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

                    

                    OnRender?.Invoke(delta);
                    
                    SDL.EndGPURenderPass(renderPass);
                }
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);

            if (!_vsyncEnabled && _targetFramerate is not null)
                _clock.RegulateFramerate(_targetFramerate.Value);
        }

        OnQuit?.Invoke();
        Destroy();
    }

    public void Draw(IDrawable drawable)
        => drawable.Draw(this);

    public void Stop()
        => _isRunning = false;

    public void Destroy()
    {
        if (!_isInitialized)
            return;

        SDL.ReleaseWindowFromGPUDevice(_gpuDevice, _window);
            
        if (_gpuDevice != IntPtr.Zero)
            SDL.DestroyGPUDevice(_gpuDevice);

        if (_window != IntPtr.Zero)
            SDL.DestroyWindow(_window);

        SDL.Quit();
        _gpuDevice     = IntPtr.Zero;
        _window        = IntPtr.Zero;
        _isInitialized = false;
    }
        
    private void Initialize()
    {
        // Initialize SDL.
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            throw new Exception($"Failed to initialize SDL: {SDL.GetError()}");

        // Create the window.
        _window = SDL.CreateWindow(
            _title,
            _width,
            _height,
            SDL.WindowFlags.Hidden);

        if (_window == IntPtr.Zero)
            throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        // Create the GPU device.
        _gpuDevice = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.MSL | SDL.GPUShaderFormat.DXIL,
            debugMode: false,
            name: null);
        
        if (_gpuDevice == IntPtr.Zero)
            throw new Exception($"Failed to create the GPU device: {SDL.GetError()}");

        // Bind the GPU device to the window.
        if (!SDL.ClaimWindowForGPUDevice(_gpuDevice, _window))
            throw new Exception($"Failed to bind GPU device to window: {SDL.GetError()}");

        // Disable VSync
        VSyncEnabled = false;
            
        // Prepare and show the window.
        SDL.ShowWindow(_window);
        
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
        
        // Cast to float first to preserve the precise sub-second delta fraction
        return (float)deltaNs / UnitConversions.NsPerSecond;
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