using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using System.Numerics;
using System.Runtime.InteropServices;

namespace WebGpuApp;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex3D
{
    public float X, Y, Z; // Position (vec3)
    public float R, G, B; // Color (vec3)

    public Vertex3D(float x, float y, float z, float r, float g, float b)
    {
        X = x; Y = y; Z = z;
        R = r; G = g; B = b;
    }
}

class Program
{
    private static IWindow? _window;
    private static WebGPU? _wgpu;
    private static unsafe Instance* _instance;
    private static unsafe Device* _device;
    private static unsafe Surface* _surface;
    private static TextureFormat _swapChainFormat;

    // 3D Pipeline Assets
    private static unsafe RenderPipeline* _pipeline;
    private static unsafe Silk.NET.WebGPU.Buffer* _vertexBuffer;
    private static unsafe Silk.NET.WebGPU.Buffer* _indexBuffer;
    private static unsafe Silk.NET.WebGPU.Buffer* _uniformBuffer;
    private static unsafe BindGroup* _bindGroup;
    private static uint _indexCount;

    // Animation / Engine Variables
    private static string _backendName = "Unknown Backend";
    private static double _frameCount = 0;
    private static double _timeElapsed = 0;
    private static float _rotationAngle = 0.0f;

    // WGSL 3D Shading Script Language Module
    private const string ShaderSource = @"
        struct Uniforms {
            mvp: mat4x4<f32>,
        };
        @group(0) @binding(0) var<uniform> config: Uniforms;

        struct VertexInput {
            @location(0) position: vec3<f32>,
            @location(1) color: vec3<f32>,
        };

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
            @location(0) vertex_color: vec3<f32>,
        };

        @vertex
        fn vs_main(model: VertexInput) -> VertexOutput {
            var out: VertexOutput;
            // Matrix multiplication transforms 3D geometry vertices into perspective clip space
            out.clip_position = config.mvp * vec4<f32>(model.position, 1.0);
            out.vertex_color = model.color;
            return out;
        }

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return vec4<f32>(in.vertex_color, 1.0);
        }
    ";

    static unsafe void Main(string[] args)
    {
        GlfwWindowing.Use();
        GlfwInput.RegisterPlatform();

        var options = WindowOptions.Default with
        {
            Title = "Silk.NET WebGPU 3D Rotating Cube",
            Size = new Silk.NET.Maths.Vector2D<int>(800, 600),
            API = GraphicsAPI.None 
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClosing; 

        _window.Run();
    }

    private static unsafe void OnLoad()
    {
        if (_window == null) return;
        _wgpu = WebGPU.GetApi();

        InstanceDescriptor instanceDesc = new InstanceDescriptor();
        _instance = _wgpu.CreateInstance(&instanceDesc);
        _surface = _window.CreateWebGPUSurface(_wgpu, _instance);

        RequestAdapterOptions adapterOptions = new RequestAdapterOptions
        {
            CompatibleSurface = _surface,
            PowerPreference = PowerPreference.HighPerformance
        };

        Adapter* adapter = null;
        _wgpu.InstanceRequestAdapter(_instance, &adapterOptions, PfnRequestAdapterCallback.From((status, adpr, msg, data) => {
            if (status == RequestAdapterStatus.Success) adapter = adpr;
        }), null);

        AdapterProperties properties = new AdapterProperties();
        _wgpu.AdapterGetProperties(adapter, &properties);
        string deviceName = properties.Name != null ? SilkMarshal.PtrToString((nint)properties.Name) : "Unknown GPU";
        _backendName = $"{properties.BackendType} ({deviceName})";

        DeviceDescriptor deviceDescriptor = new DeviceDescriptor();
        _device = null;
        _wgpu.AdapterRequestDevice(adapter, &deviceDescriptor, PfnRequestDeviceCallback.From((status, dev, msg, data) => {
            if (status == RequestDeviceStatus.Success) _device = dev;
        }), null);

        SurfaceCapabilities capabilities;
        _wgpu.SurfaceGetCapabilities(_surface, adapter, &capabilities);
        _swapChainFormat = (capabilities.FormatCount > 0 && capabilities.Formats != null) ? *capabilities.Formats : TextureFormat.Bgra8Unorm;

        SurfaceConfiguration surfaceConfig = new SurfaceConfiguration
        {
            Device = _device,
            Usage = TextureUsage.RenderAttachment,
            Format = _swapChainFormat, 
            PresentMode = PresentMode.Fifo, 
            Width = (uint)_window.Size.X,
            Height = (uint)_window.Size.Y,
            AlphaMode = CompositeAlphaMode.Opaque
        };
        _wgpu.SurfaceConfigure(_surface, &surfaceConfig);
        _wgpu.SurfaceCapabilitiesFreeMembers(capabilities);

        // CREATE 3D CUBE GEOMETRY BUFFERS
        CreateCubeBuffers();

        // COMPILE SHADERS & GRAPHICS PIPELINE
        CreateRenderPipeline();

        Console.WriteLine($"WebGPU context initialized on backend: {_backendName}");
    }

    private static unsafe void CreateCubeBuffers()
    {
        // 8 Corners of a Cube with Unique Vertex Colors
        Vertex3D[] vertices = new Vertex3D[]
        {
            new(-0.5f, -0.5f,  0.5f,  1.0f, 0.0f, 0.0f), // Front-Bottom-Left (Red)
            new( 0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 0.0f), // Front-Bottom-Right (Green)
            new( 0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f), // Front-Top-Right (Blue)
            new(-0.5f,  0.5f,  0.5f,  1.0f, 1.0f, 0.0f), // Front-Top-Left (Yellow)
            new(-0.5f, -0.5f, -0.5f,  1.0f, 0.0f, 1.0f), // Back-Bottom-Left (Magenta)
            new( 0.5f, -0.5f, -0.5f,  0.0f, 1.0f, 1.0f), // Back-Bottom-Right (Cyan)
            new( 0.5f,  0.5f, -0.5f,  1.0f, 1.0f, 1.0f), // Back-Top-Right (White)
            new(-0.5f,  0.5f, -0.5f,  0.0f, 0.0f, 0.0f)  // Back-Top-Left (Black)
        };

        // Indices Mapping out the 12 Triangles that Make Up the 6 Cube Faces
        ushort[] indices = new ushort[]
        {
            0, 1, 2,  2, 3, 0, // Front Face
            1, 5, 6,  6, 2, 1, // Right Face
            4, 0, 3,  3, 7, 4, // Left Face
            4, 5, 1,  1, 0, 4, // Bottom Face
            3, 2, 6,  6, 7, 3, // Top Face
            5, 4, 7,  7, 6, 5  // Back Face
        };
        _indexCount = (uint)indices.Length;

        // Upload Vertices to GPU
        BufferDescriptor vertexBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
            Size = (ulong)(vertices.Length * sizeof(Vertex3D)),
            MappedAtCreation = false
        };
        _vertexBuffer = _wgpu!.DeviceCreateBuffer(_device, &vertexBufferDesc);
        fixed (void* pVerts = vertices)
        {
            _wgpu.QueueWriteBuffer(_wgpu.DeviceGetQueue(_device), _vertexBuffer, 0, pVerts, (nuint)vertexBufferDesc.Size);
        }

        // Upload Indices to GPU
        BufferDescriptor indexBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Index | BufferUsage.CopyDst,
            Size = (ulong)(indices.Length * sizeof(ushort)),
            MappedAtCreation = false
        };
        _indexBuffer = _wgpu.DeviceCreateBuffer(_device, &indexBufferDesc);
        fixed (void* pIndices = indices)
        {
            _wgpu.QueueWriteBuffer(_wgpu.DeviceGetQueue(_device), _indexBuffer, 0, pIndices, (nuint)indexBufferDesc.Size);
        }

        // Create Uniform Matrix Matrix Buffer (4x4 matrix = 16 floats = 64 bytes)
        BufferDescriptor uniformBufferDesc = new BufferDescriptor
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = 64,
            MappedAtCreation = false
        };
        _uniformBuffer = _wgpu.DeviceCreateBuffer(_device, &uniformBufferDesc);
    }

    private static unsafe void CreateRenderPipeline()
    {
        // 1. Compile Shader Source
        ShaderModuleWGSLDescriptor wgslDesc = new ShaderModuleWGSLDescriptor
        {
            Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
            Code = (byte*)SilkMarshal.StringToPtr(ShaderSource)
        };
        ShaderModuleDescriptor shaderDesc = new ShaderModuleDescriptor { NextInChain = (ChainedStruct*)&wgslDesc };
        ShaderModule* shaderModule = _wgpu!.DeviceCreateShaderModule(_device, &shaderDesc);
        SilkMarshal.Free((nint)wgslDesc.Code);

        // 2. Define Uniform Bind Group Resource Layout
        BindGroupLayoutEntry bindLayoutEntry = new BindGroupLayoutEntry
        {
            Binding = 0,
            Visibility = ShaderStage.Vertex,
            Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform, MinBindingSize = 64 }
        };
        BindGroupLayoutDescriptor bindGroupLayoutDesc = new BindGroupLayoutDescriptor { EntryCount = 1, Entries = &bindLayoutEntry };
        BindGroupLayout* bindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, &bindGroupLayoutDesc);

        PipelineLayoutDescriptor pipelineLayoutDesc = new PipelineLayoutDescriptor { BindGroupLayoutCount = 1, BindGroupLayouts = &bindGroupLayout };
        PipelineLayout* pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, &pipelineLayoutDesc);

        // Create Unified Frame Bind Group Instance
        BindGroupEntry bindGroupEntry = new BindGroupEntry
        {
            Binding = 0,
            Buffer = _uniformBuffer,
            Offset = 0,
            Size = 64
        };
        BindGroupDescriptor bindGroupDesc = new BindGroupDescriptor { Layout = bindGroupLayout, EntryCount = 1, Entries = &bindGroupEntry };
        _bindGroup = _wgpu.DeviceCreateBindGroup(_device, &bindGroupDesc);

        // 3. Configure Input Geometry Vertex Layout Attributes
        VertexAttribute* vertexAttributes = stackalloc VertexAttribute[2];
        vertexAttributes[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 }; // Pos
        vertexAttributes[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 }; // Color

        VertexBufferLayout vertexBufferLayout = new VertexBufferLayout
        {
            ArrayStride = (ulong)sizeof(Vertex3D),
            StepMode = VertexStepMode.Vertex,
            AttributeCount = 2,
            Attributes = vertexAttributes
        };

        // 4. Construct Final Multi-Stage Render Pipeline State Layout Object
        ColorTargetState colorTarget = new ColorTargetState { Format = _swapChainFormat, WriteMask = ColorWriteMask.All };
        FragmentState fragmentState = new FragmentState { Module = shaderModule, EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"), TargetCount = 1, Targets = &colorTarget };
        
        RenderPipelineDescriptor pipelineDesc = new RenderPipelineDescriptor
        {
            Layout = pipelineLayout,
            Vertex = new VertexState { Module = shaderModule, EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"), BufferCount = 1, Buffers = &vertexBufferLayout },
            Fragment = &fragmentState,
            // FIX 2: Change IndexFormat to StripIndexFormat
            Primitive = new PrimitiveState 
            { 
                Topology = PrimitiveTopology.TriangleList, 
                StripIndexFormat = IndexFormat.Undefined, // Undefined is correct for individual non-strip triangle lists
                FrontFace = FrontFace.Ccw, 
                CullMode = CullMode.Back 
            },
            Multisample = new MultisampleState { Count = 1, Mask = uint.MaxValue },
            DepthStencil = null
        };

        _pipeline = _wgpu.DeviceCreateRenderPipeline(_device, &pipelineDesc);
        SilkMarshal.Free((nint)fragmentState.EntryPoint);
        SilkMarshal.Free((nint)pipelineDesc.Vertex.EntryPoint);
    }

    private static unsafe void OnRender(double deltaTime)
    {
        if (_wgpu == null || _device == null || _surface == null || _window == null) return;

        // 1. Calculate FPS & 3D Math Rotations
        _frameCount++;
        _timeElapsed += deltaTime;
        _rotationAngle += (float)deltaTime * 1.0f; // Spin rate speed adjustment

        if (_timeElapsed >= 1.0)
        {
            _window.Title = $"Silk.NET WebGPU Cube | Backend: {_backendName} | FPS: {_frameCount / _timeElapsed:F1}";
            _frameCount = 0; _timeElapsed = 0;
        }

        // Generate 3D Model-View-Projection Transforms Matrix using System.Numerics
        Matrix4x4 model = Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1.0f, 1.0f, 0.0f)), _rotationAngle);
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, -2.5f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4.0f, (float)_window.Size.X / _window.Size.Y, 0.1f, 100.0f);
        Matrix4x4 mvp = model * view * projection;

        // Transpose or feed data smoothly into unmanaged matrix uniform
        _wgpu.QueueWriteBuffer(_wgpu.DeviceGetQueue(_device), _uniformBuffer, 0, &mvp, 64);

        // 2. Begin Drawing Context Sequence Pass
        SurfaceTexture surfaceTexture;
        _wgpu.SurfaceGetCurrentTexture(_surface, &surfaceTexture);
        if (surfaceTexture.Status == SurfaceGetCurrentTextureStatus.Timeout || surfaceTexture.Status == SurfaceGetCurrentTextureStatus.Outdated) return;

        TextureViewDescriptor viewDescriptor = new TextureViewDescriptor
        {
            Format = _swapChainFormat, Dimension = TextureViewDimension.Dimension2D, MipLevelCount = uint.MaxValue, ArrayLayerCount = uint.MaxValue, Aspect = TextureAspect.All
        };
        TextureView* targetView = _wgpu.TextureCreateView(surfaceTexture.Texture, &viewDescriptor);

        CommandEncoderDescriptor encoderDescriptor = new CommandEncoderDescriptor();
        CommandEncoder* encoder = _wgpu.DeviceCreateCommandEncoder(_device, &encoderDescriptor);

        RenderPassColorAttachment colorAttachment = new RenderPassColorAttachment
        {
            View = targetView, LoadOp = LoadOp.Clear, StoreOp = StoreOp.Store, ClearValue = new Color(0.1, 0.1, 0.12, 1.0) // Charcoal grey
        };
        RenderPassDescriptor renderPassDescriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &colorAttachment };

        RenderPassEncoder* renderPass = _wgpu.CommandEncoderBeginRenderPass(encoder, &renderPassDescriptor);
        
        // 3. Emit Render States to GPU
        _wgpu.RenderPassEncoderSetPipeline(renderPass, _pipeline);
        _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, null);
        _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, _vertexBuffer, 0, nuint.MaxValue);
        _wgpu.RenderPassEncoderSetIndexBuffer(renderPass, _indexBuffer, IndexFormat.Uint16, 0, nuint.MaxValue);
        _wgpu.RenderPassEncoderDrawIndexed(renderPass, _indexCount, 1, 0, 0, 0);

        _wgpu.RenderPassEncoderEnd(renderPass);

        CommandBufferDescriptor cmdBufferDescriptor = new CommandBufferDescriptor();
        CommandBuffer* commandBuffer = _wgpu.CommandEncoderFinish(encoder, &cmdBufferDescriptor);
        _wgpu.QueueSubmit(_wgpu.DeviceGetQueue(_device), 1, &commandBuffer);
        _wgpu.SurfacePresent(_surface);
        
        // Native cleanup memory checks
        _wgpu.TextureViewRelease(targetView);
        _wgpu.CommandBufferRelease(commandBuffer);
        _wgpu.CommandEncoderRelease(encoder);
        _wgpu.TextureRelease(surfaceTexture.Texture);
    }

    private static unsafe void OnClosing()
    {
        if (_wgpu != null)
        {
            if (_vertexBuffer != null) _wgpu.BufferRelease(_vertexBuffer);
            if (_indexBuffer != null) _wgpu.BufferRelease(_indexBuffer);
            if (_uniformBuffer != null) _wgpu.BufferRelease(_uniformBuffer);
            if (_bindGroup != null) _wgpu.BindGroupRelease(_bindGroup);
            if (_pipeline != null) _wgpu.RenderPipelineRelease(_pipeline);
            if (_device != null) _wgpu.DeviceRelease(_device);
            if (_surface != null) _wgpu.SurfaceRelease(_surface);
            if (_instance != null) _wgpu.InstanceRelease(_instance);
        }
    }
}
