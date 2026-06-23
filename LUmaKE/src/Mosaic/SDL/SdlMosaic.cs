using System.Text;
using LUmaKE.Mathematics;
using LUmaKE.Primitives;
using LUmaKE.Utility;
using SDL3;

namespace LUmaKE.Mosaic;

public sealed class SdlMosaic : IMosaic
{
    private IntPtr _windowHandle;
    private IntPtr _gpuHandle;
    private IntPtr _renderPassHandle;
    private IntPtr _pipelineHandle;

    private readonly FramerateClock _clock = new();

    private bool         _running         = false;
    private string       _title           = string.Empty;
    private Vector2<int> _position        = new(0, 0);
    private Vector2<int> _size            = new(800, 600);
    private double?      _targetFramerate = 60.0;
    private bool         _vsyncEnabled    = false;
    
    public event Action?          OnStart;
    public event Action?          OnClose;
    public event Action<Keycode>? OnKeyDown;
    public event Action<double>?  OnUpdate;
    public event Action<double>?  OnRender;
    
    public bool Running => _running;

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

    internal SdlMosaic(string title, int width, int height)
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
        Release();
    }

    public void Close()
    {
        _running = false;
    }
    
    private void Initialize()
    {
        // Initialize SDL.
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            throw new Exception($"Failed to initialize SDL: {SDL.GetError()}");

        // Initialize a window.
        _windowHandle = SDL.CreateWindow(
            _title,
            _size.X,
            _size.Y,
            SDL.WindowFlags.Hidden);
        
        if (_windowHandle == IntPtr.Zero)
            throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        // Initialize the GPU device.
        _gpuHandle = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.MSL | SDL.GPUShaderFormat.DXIL,
            debugMode : false,
            name      : null);

        if (_gpuHandle == IntPtr.Zero)
            throw new Exception($"Failed to create the GPU device: {SDL.GetError()}");

        // Bind the window to the GPU device.
        if (!SDL.ClaimWindowForGPUDevice(_gpuHandle, _windowHandle))
            throw new Exception($"Failed to bind GPU device to window: {SDL.GetError()}");

        // Create the primary graphics pipeline.
        _pipelineHandle = CreatePipeline();
            
        // Show window post-init.
        SDL.ShowWindow(_windowHandle);
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
    
        // Use WaitAndAcquire to throttle the CPU thread to the display refresh rate
        if (SDL.WaitAndAcquireGPUSwapchainTexture(
            commandBuffer,
            _windowHandle,
            out IntPtr swapchainTexture,
            out uint width,
            out uint height))
        {
            // If the window is minimized or occluded, swapchainTexture might be Zero. Skip rendering.
            if (swapchainTexture != IntPtr.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    ClearColor = new SDL.FColor { R = 0.1f, G = 0.1f, B = 0.1f, A = 1.0f }, // Dark gray to see black artifacts
                    LoadOp     = SDL.GPULoadOp.Clear,
                    StoreOp    = SDL.GPUStoreOp.Store,
                    Texture    = swapchainTexture
                };
    
                unsafe
                {
                    _renderPassHandle = SDL.BeginGPURenderPass(commandBuffer, (IntPtr)(&colorTargetInfo), 1, IntPtr.Zero);

#if DEBUG_TRIANGLE
                    SDL.BindGPUGraphicsPipeline(_renderPassHandle, _pipelineHandle);
                    SDL.DrawGPUPrimitives(
                        _renderPassHandle,
                        (uint)3,
                        (uint)1,
                        (uint)0,
                        (uint)0);
#endif
                    
                    OnRender?.Invoke(delta);
                    SDL.EndGPURenderPass(_renderPassHandle);
                }
            }
        }
        else
        {
            string error = SDL.GetError();
            
            if (!string.IsNullOrEmpty(error))
                throw new Exception($"Swapchain failure: {error}");
        }
    
        // Submit commands to hardware queue
        SDL.SubmitGPUCommandBuffer(commandBuffer);
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

    private unsafe IntPtr CompileShader(string shaderFileName, ShaderCross.ShaderStage stage)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Shaders", shaderFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"SPIR-V shader bytecode not found at '{path}'.");

        byte[] code = File.ReadAllBytes(path);
        nuint codeSize = (nuint)code.Length;
        byte[] entryPoint = Encoding.UTF8.GetBytes("Main");

        fixed (void* pCode     = code,
                     pEntPoint = entryPoint)
        {
            var spirvInfo = new ShaderCross.SPIRVInfo
            {
                ByteCode     = (IntPtr)pCode,
                Entrypoint   = (IntPtr)pEntPoint,
                ByteCodeSize = codeSize,
                ShaderStage  = stage
            };

            var resourceInfo = new ShaderCross.GraphicsShaderResourceInfo
            {
                NumSamplers        = 0,
                NumStorageTextures = 0,
                NumStorageBuffers  = 0,
                NumUniformBuffers  = 0
            };

            IntPtr gpuShader = ShaderCross.CompileGraphicsShaderFromSPIRV(
                _gpuHandle,
                ref spirvInfo,
                ref resourceInfo,
                0);

            if (gpuShader == IntPtr.Zero)
                throw new Exception($"Failed to compile SPIR-V bytecode into native shader code. SDL Error: {SDL.GetError()}.");

            return gpuShader;
        }
    }

    private void ReleaseShader(IntPtr handle)
    {
        SDL.ReleaseGPUShader(_gpuHandle, handle);
    }

    private IntPtr CreatePipeline()
    {
        var vertexShader = CompileShader("Mosaic.vert.spv", ShaderCross.ShaderStage.Vertex);
        var fragShader   = CompileShader("Mosaic.frag.spv", ShaderCross.ShaderStage.Fragment);
        
        SDL.GPUTextureFormat swapchainFormat = SDL.GetGPUSwapchainTextureFormat(_gpuHandle, _windowHandle);
    
        SDL.GPUColorTargetDescription colorTarget = new SDL.GPUColorTargetDescription
        {
            Format = swapchainFormat
        };
        
        unsafe
        {
            var plCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader     = vertexShader,
                FragmentShader   = fragShader,
                VertexInputState = default, // Empty vertex layout
                PrimitiveType    = SDL.GPUPrimitiveType.TriangleList,
                RasterizerState  = new SDL.GPURasterizerState
                {
                    FillMode  = SDL.GPUFillMode.Fill,
                    CullMode  = SDL.GPUCullMode.None,
                    FrontFace = SDL.GPUFrontFace.CounterClockwise
                },
                
                // Assign target details to match your Render Pass destination
                TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
                {
                    NumColorTargets = 1,
                    ColorTargetDescriptions = (IntPtr)(&colorTarget),
                    HasDepthStencilTarget = false
                }
            };
    
            IntPtr pipelineHandle = SDL.CreateGPUGraphicsPipeline(_gpuHandle, in plCreateInfo);
    
            if (pipelineHandle == IntPtr.Zero)
                throw new Exception($"Failed to create the graphics pipeline, {SDL.GetError()}.");
            
            ReleaseShader(vertexShader);
            ReleaseShader(fragShader);
    
            return pipelineHandle;
        }
    }

    private void ReleasePipeline(IntPtr handle)
    {
        SDL.ReleaseGPUGraphicsPipeline(_gpuHandle, handle);
    }
    
    private void Release()
    {
        ReleasePipeline(_pipelineHandle);
        
        SDL.ReleaseWindowFromGPUDevice(_gpuHandle, _windowHandle);

        if (_gpuHandle != IntPtr.Zero)
            SDL.DestroyGPUDevice(_gpuHandle);

        if (_windowHandle != IntPtr.Zero)
            SDL.DestroyWindow(_windowHandle);

        _gpuHandle = IntPtr.Zero;
        _windowHandle = IntPtr.Zero;
        SDL.Quit();
    }
}