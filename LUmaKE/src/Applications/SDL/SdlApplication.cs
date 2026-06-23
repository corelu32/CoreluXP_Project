using System.Diagnostics;
using System.Runtime.InteropServices;
using LUmaKE.Graphics.Gpu;
using LUmaKE.Mathematics;
using LUmaKE.Primitives;
using SDL3;
using Silk.NET.Shaderc;

namespace LUmaKE.Applications;

/// <summary>
///   Work with an SDL backend. Allows for registration of multiple application windows.
/// </summary>
public sealed class SdlApplication : IApplication
{
    // GLSL is compiled using Silk.NET.Shaderc, separate from the SDL runtime.
    // Therefore, handles must reference back to here.
    private readonly Dictionary<IntPtr, byte[]> _compiledGlslCode = [];

    private readonly Dictionary<GpuShader,     IntPtr> _shaderHandles   = [];
    private readonly Dictionary<GpuPipeline,   IntPtr> _pipelineHandles = [];
    private readonly Dictionary<GpuBuffer,     IntPtr> _bufferHandles   = [];
    
    private IntPtr _windowHandle;
    private IntPtr _gpuHandle;
    private IntPtr _renderPassHandle;

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
        DestroyApplication();
    }

    public void DrawPrimitives(int vertexIndex, int vertexCount, int instanceIndex, int instanceCount)
    {
        SDL.DrawGPUPrimitives(
            _renderPassHandle,
            (uint)vertexCount,
            (uint)instanceCount,
            (uint)vertexIndex,
            (uint)instanceIndex);
    }
    
    public void Close()
    {
        _running = false;
    }

    public void LoadPipeline(GpuPipeline pipeline)
    {
        // If the pipeline was already loaded, then abort.
        if (_pipelineHandles.ContainsKey(pipeline))
            return;
        
        var vsPayload = pipeline.VertexShader;
        var fsPayload = pipeline.FragmentShader;

        // Compile the vertex shader if it wasn't already cached.
        if (!_shaderHandles.ContainsKey(vsPayload))
            _shaderHandles[vsPayload] = CompileShader(vsPayload);

        // Compile the fragment shader if it wasn't already cached.
        if (!_shaderHandles.ContainsKey(fsPayload))
            _shaderHandles[fsPayload] = CompileShader(fsPayload);

        // Create and cache the pipeline.
        _pipelineHandles[pipeline] = CreatePipeline(pipeline);
    }

    public void BindPipeline(GpuPipeline pipeline)
    {
        if (!IsPipelineLoaded(pipeline))
            throw new Exception($"You cannot bind a pipeline that wasn't loaded.");
        
        SDL.BindGPUGraphicsPipeline(_renderPassHandle, _pipelineHandles[pipeline]);
    }

    public void UnloadPipeline(GpuPipeline pipeline)
    {
        if (!IsPipelineLoaded(pipeline))
            return;

        // Filter for all to-be remaining pipeline keys.
        var remainingPipelineKeys = _pipelineHandles.Keys
            .Where(pl => pl != pipeline);

        // If no other pipelines reference this vertex shader, release it.
        if (!remainingPipelineKeys.Any(pl => pl.VertexShader == pipeline.VertexShader))
            ReleaseShader(pipeline.VertexShader);

        // If no other pipelines reference this fragment shader, release it.
        if (!remainingPipelineKeys.Any(pl => pl.FragmentShader == pipeline.FragmentShader))
            ReleaseShader(pipeline.FragmentShader);

        // Release the pipeline.
        ReleasePipeline(pipeline);
    }

    public bool IsPipelineLoaded(GpuPipeline pipeline)
        => _pipelineHandles.ContainsKey(pipeline);

    public void LoadBuffer(GpuBuffer buffer)
    {
        
    }

    public void BindBuffer(GpuBuffer buffer)
    {
        
    }

    public void BindBuffers(ICollection<GpuBuffer> buffers)
    {
        
    }

    public void UnloadBuffer(GpuBuffer buffer)
    {
        
    }

    public bool IsBufferLoaded(GpuBuffer buffer)
        => _bufferHandles.ContainsKey(buffer);
        
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
    
        // FIX 1: Use WaitAndAcquire to throttle the CPU thread to the display refresh rate
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
                    // Fix 2: Wrap execution to safeguard stack layout stability
                    _renderPassHandle = SDL.BeginGPURenderPass(commandBuffer, (IntPtr)(&colorTargetInfo), 1, IntPtr.Zero);
                    
                    // Signal the window's render event (your pipeline binding + draw call must live here)
                    OnRender?.Invoke(delta);
    
                    // End the GPU render pass.
                    SDL.EndGPURenderPass(_renderPassHandle);
                }
            }
        }
        else
        {
            // Log if swapchain context initialization explicitly failed
            string error = SDL.GetError();
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Swapchain failure: {error}");
            }
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

    private unsafe IntPtr CreatePipeline(GpuPipeline payload)
    {
        var vertDescripts =
        (
            from   desc in payload.Layout.Descriptions
            select new SDL.GPUVertexBufferDescription()
            {
                Slot             = (uint)desc.Slot,
                Pitch            = (uint)desc.Stride,
                InstanceStepRate = 0,
                
                InputRate = desc.InputClassification switch
                    {
                        InputClassification.PerVertex   => SDL.GPUVertexInputRate.Vertex,
                        InputClassification.PerInstance => SDL.GPUVertexInputRate.Instance,

                        _ => throw new Exception("Invalid input classification.")
                    }
            }
        ).ToArray();
        
        var vertAttribs =
        (
            from   desc in payload.Layout.Descriptions
            from   attr in desc.Attributes
            select new SDL.GPUVertexAttribute
            {
                BufferSlot = (uint)attr.ParentDescription.Slot,
                Offset     = (uint)attr.Offset,
                Format     = ConvertVertexAttributeTypeInfo(attr)
            }
        ).ToArray();

        var colorTargetDescripts =
        (
            from   desc in payload.ColorTargets
            select new SDL.GPUColorTargetDescription
            {
                Format = SDL.GetGPUSwapchainTextureFormat(_gpuHandle, _windowHandle),
                BlendState = new SDL.GPUColorTargetBlendState
                {
                    EnableBlend = false
                }
            }
        ).ToArray();
        
        fixed (void* pVertDescripts = vertDescripts,
                     pVertAttribs   = vertAttribs,
                     pTargDescripts = colorTargetDescripts)
        {
            var vertInputState = new SDL.GPUVertexInputState
            {
                VertexBufferDescriptions = (IntPtr) pVertDescripts,
                VertexAttributes         = (IntPtr) pVertAttribs,
                NumVertexBuffers         = (uint)   vertDescripts.Length,
                NumVertexAttributes      = (uint)   vertAttribs.Length,
            };
    
            var targetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = (IntPtr) pTargDescripts,
                NumColorTargets         = (uint)   colorTargetDescripts.Length
            };
            
            var plCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader     = _shaderHandles[payload.VertexShader],
                FragmentShader   = _shaderHandles[payload.FragmentShader],
                VertexInputState = vertInputState,
                TargetInfo       = targetInfo,
                
                PrimitiveType = payload.Topology switch
                {
                    PrimitiveTopology.Points        => SDL.GPUPrimitiveType.PointList,
                    PrimitiveTopology.Lines         => SDL.GPUPrimitiveType.LineList,
                    PrimitiveTopology.LineStrip     => SDL.GPUPrimitiveType.LineStrip,
                    PrimitiveTopology.Triangles     => SDL.GPUPrimitiveType.TriangleList,
                    PrimitiveTopology.TriangleStrip => SDL.GPUPrimitiveType.TriangleStrip,
    
                    _ => throw new Exception("Invalid topology provided.")
                },
    
                RasterizerState = new SDL.GPURasterizerState
                {
                    FillMode  = SDL.GPUFillMode.Fill,
                    CullMode  = SDL.GPUCullMode.None,
                    FrontFace = SDL.GPUFrontFace.CounterClockwise
                }
            };
    
            IntPtr pipelineHandle = SDL.CreateGPUGraphicsPipeline(_gpuHandle, in plCreateInfo);
    
            if (pipelineHandle == IntPtr.Zero)
                throw new Exception($"Failed to create the graphics pipeline, {SDL.GetError()}.");
    
            return pipelineHandle;
        }
    }

    private static SDL.GPUVertexElementFormat ConvertVertexAttributeTypeInfo(VertexAttribute attribute)
    {
        // Use a 2D lookup table to convert a VertexAttributeType (which defines the type and dimension separately)
        // into a compatible SDL VertexElementFormat.
        
        Dictionary<AttributeType, Dictionary<int, SDL.GPUVertexElementFormat>> map = new()
        {
            [AttributeType.Byte] =
            {
                [2] = SDL.GPUVertexElementFormat.Byte2,
                [4] = SDL.GPUVertexElementFormat.Byte4
            },
            [AttributeType.Int] =
            {
                [1] = SDL.GPUVertexElementFormat.Int,
                [2] = SDL.GPUVertexElementFormat.Int2,
                [3] = SDL.GPUVertexElementFormat.Int3,
                [4] = SDL.GPUVertexElementFormat.Int4
            },
            [AttributeType.Float] =
            {
                [1] = SDL.GPUVertexElementFormat.Float,
                [2] = SDL.GPUVertexElementFormat.Float2,
                [3] = SDL.GPUVertexElementFormat.Float3,
                [4] = SDL.GPUVertexElementFormat.Float4
            }
        };
        
        if (map.TryGetValue(attribute.Type, out var v) && v.TryGetValue(attribute.Dimensions, out var result))
            return result;

        throw new Exception($"Cannot create an SDL Vertex element format based on VertexAttribute type '{attribute.Type}' with {attribute.Dimensions} dimensions.");
    }
    
    private IntPtr CompileShader(GpuShader payload)
    {
        var stage = payload.Stage switch
        {
            ShaderStage.Vertex   => ShaderCross.ShaderStage.Vertex,
            ShaderStage.Fragment => ShaderCross.ShaderStage.Fragment,
            ShaderStage.Compute  => ShaderCross.ShaderStage.Compute,
            
            _ => throw new Exception("Cannot compile SDL GPU shader with unsupported shader stage.")
        };
        
        // Allocate native UTF-8 strings. CoTaskMem handles null-termination automatically.
        IntPtr ipCode = IntPtr.Zero;
        IntPtr ipEntryPoint = IntPtr.Zero;
    
        try
        {
            ipCode = Marshal.StringToCoTaskMemUTF8(payload.Code);
            ipEntryPoint = Marshal.StringToCoTaskMemUTF8(payload.EntryPoint);
    
            return payload.Format switch
            {
                ShaderFormat.Hlsl => CompileHlsl(ipCode, ipEntryPoint, stage),
                ShaderFormat.Glsl => CompileGlsl(ipCode, ipEntryPoint, stage, (nuint)payload.Code.Length),
                
                _ => throw new Exception("Unsupported shader source code format.")
            };
        }
        finally
        {
            // Free allocations after native compilation completes to prevent memory leaks
            if (ipCode != IntPtr.Zero) Marshal.FreeCoTaskMem(ipCode);
            if (ipEntryPoint != IntPtr.Zero) Marshal.FreeCoTaskMem(ipEntryPoint);
        }
    }

    private unsafe IntPtr CompileGlsl(
        IntPtr pCode,
        IntPtr pEntryPoint,
        ShaderCross.ShaderStage stage,
        nuint codeLen)
    {
        using var shaderc = Shaderc.GetApi();
        Compiler* compiler = shaderc.CompilerInitialize();
        CompileOptions* options = shaderc.CompileOptionsInitialize();
    
        ShaderKind shcStage = stage switch
        {
            ShaderCross.ShaderStage.Vertex   => ShaderKind.VertexShader,
            ShaderCross.ShaderStage.Fragment => ShaderKind.FragmentShader,
            ShaderCross.ShaderStage.Compute  => ShaderKind.ComputeShader,
            _ => throw new Exception("Unsupported shader stage.")
        };
        
        shaderc.CompileOptionsSetOptimizationLevel(options, OptimizationLevel.Zero);
        shaderc.CompileOptionsSetSourceLanguage(options, SourceLanguage.Glsl);
        
        // Explicitly target Vulkan 1.3 / SPIR-V 1.6 generation
        shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan13);
        
        CompilationResult* result = shaderc.CompileIntoSpv(
            compiler,
            (byte*)pCode,
            codeLen,
            shcStage,
            "runtime_shader",
            (byte*)pEntryPoint,
            options);
    
        CompilationStatus status = shaderc.ResultGetCompilationStatus(result);
    
        if (status != CompilationStatus.Success)
            throw new Exception($"Failed to compile GLSL to SPIR-V. {shaderc.ResultGetErrorMessageS(result)}.");
    
        nuint length = shaderc.ResultGetLength(result);
        byte* bytesPtr = shaderc.ResultGetBytes(result);
        IntPtr gpuShader = CompileSpirv((IntPtr)bytesPtr, length, pEntryPoint, stage);
        
        shaderc.ResultRelease(result);
        shaderc.CompileOptionsRelease(options);
        shaderc.CompilerRelease(compiler);
        
        return gpuShader;
    }
    
    private IntPtr CompileHlsl(
        IntPtr pCode,
        IntPtr pEntryPoint,
        ShaderCross.ShaderStage stage)
    {   
        var info = new ShaderCross.HLSLInfo
        {
            Source      = pCode,
            Entrypoint  = pEntryPoint,
            IncludeDir  = IntPtr.Zero,
            ShaderStage = stage
        };
        
        // Output parameters handle the returned SPIR-V byte length natively
        IntPtr pByteCode = ShaderCross.CompileSPIRVFromHLSL(ref info, out UIntPtr pByteCodeSize);
        
        if (pByteCode == IntPtr.Zero || pByteCodeSize == UIntPtr.Zero)
            throw new Exception($"Failed to compile HLSL into SPIR-V bytecode. SDL Error: {SDL.GetError()}.");
    
        return CompileSpirv(pByteCode, pByteCodeSize, pEntryPoint, stage);
    }
    
    private IntPtr CompileSpirv(
        IntPtr  pByteCode,
        UIntPtr pByteCodeSize,
        IntPtr  pEntryPoint,
        ShaderCross.ShaderStage stage)
    {
        var spirvInfo = new ShaderCross.SPIRVInfo
        {
            ByteCode     = pByteCode,
            ByteCodeSize = pByteCodeSize,
            Entrypoint   = pEntryPoint,
            ShaderStage  = stage
        };
    
        // TODO: Map layout descriptions directly from your incoming metadata representation
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
    
    private void ReleaseShader(GpuShader shader)
    {
        if (!_shaderHandles.TryGetValue(shader, out var handle))
            return;

        SDL.ReleaseGPUShader(_gpuHandle, handle);
        _shaderHandles.Remove(shader);
    }

    private void ReleasePipeline(GpuPipeline pipeline)
    {
        if (!_pipelineHandles.TryGetValue(pipeline, out var handle))
            return;

        SDL.ReleaseGPUGraphicsPipeline(_gpuHandle, handle);
        _pipelineHandles.Remove(pipeline);
    }
    
    private void DestroyApplication()
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