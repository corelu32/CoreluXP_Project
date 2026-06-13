using System.Runtime.InteropServices;
using Prototype.V2.Graphics;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace Prototype.V2.Renderers;

/// <summary>
/// Renders a collection of <see cref="LegacyShape"/> instances using WebGPU.
/// No vertex buffers are allocated — all geometry is produced procedurally
/// inside the WGSL vertex shader from per-shape storage buffers.
/// </summary>
public unsafe sealed class LegacyRenderer : IDisposable
{
    // ── WGSL source ──────────────────────────────────────────────────────────
    // The shader receives:
    //   @group(0) bind_group 0 — frame-level uniforms (camera, lighting, time)
    //   @group(1) bind_group 1 — per-shape geometry  (slice centres + radii)
    //   @group(2) bind_group 2 — per-shape instance  (model matrix, material)
    //
    // vertex_index encodes:
    //   - For side triangles:  two rings of (Edges+1) vertices × Slices quads
    //   - For cap triangles:   top/bottom fans
    // The shader decodes slice/edge index arithmetically — no buffer lookup.
    // ─────────────────────────────────────────────────────────────────────────
    private const string ShaderSource = """
        // ── Binding layouts ──────────────────────────────────────────────────

        struct FrameUniforms {
            view_proj     : mat4x4<f32>,   // camera view-projection
            camera_pos    : vec3<f32>,
            time          : f32,
            light_pos     : vec3<f32>,
            light_color   : vec3<f32>,
            ambient_color : vec3<f32>,
        };

        // Per-slice data packed as:
        //   centre : vec4<f32>        (xyz + pad)
        //   radii  : array<f32, N>   (one per edge, padded to vec4)
        // We store it as a raw f32 array and index manually.
        struct GeometryBuffer {
            data : array<f32>,
        };

        struct InstanceUniforms {
            model     : mat4x4<f32>,
            color     : vec4<f32>,
            uv_scale  : vec2<f32>,
            uv_offset : vec2<f32>,
            shininess : f32,
            specular  : f32,
            ambient   : f32,
            _pad      : f32,
        };

        struct ShapeParams {
            edges        : u32,
            slices       : u32,
            edge_stride  : u32,  // padded edge count (multiple of 4)
            _pad         : u32,
        };

        @group(0) @binding(0) var<uniform>         frame    : FrameUniforms;
        @group(1) @binding(0) var<storage, read>   geometry : GeometryBuffer;
        @group(1) @binding(1) var<uniform>         shape    : ShapeParams;
        @group(2) @binding(0) var<uniform>         instance : InstanceUniforms;

        // ── Helpers ───────────────────────────────────────────────────────────

        fn floats_per_slice(edge_stride: u32) -> u32 {
            return 4u + edge_stride;
        }

        fn slice_centre(slice_idx: u32, edge_stride: u32) -> vec3<f32> {
            let base = slice_idx * floats_per_slice(edge_stride);
            return vec3<f32>(
                geometry.data[base + 0u],
                geometry.data[base + 1u],
                geometry.data[base + 2u]
            );
        }

        fn edge_radius(edge_idx: u32, slice_idx: u32, edge_stride: u32) -> f32 {
            let base = slice_idx * floats_per_slice(edge_stride) + 4u;
            return geometry.data[base + edge_idx];
        }

        // Construct a ring vertex at (edgeIdx, sliceIdx) in local space.
        fn ring_vertex(edge_idx: u32, slice_idx: u32) -> vec3<f32> {
            let centre = slice_centre(slice_idx, shape.edge_stride);
            let r      = edge_radius(edge_idx % shape.edges, slice_idx, shape.edge_stride);
            let angle  = (2.0 * 3.14159265358979) * f32(edge_idx) / f32(shape.edges);
            return centre + vec3<f32>(cos(angle) * r, 0.0, sin(angle) * r);
        }

        fn ring_normal(edge_idx: u32, slice_idx: u32) -> vec3<f32> {
            // Approximate outward normal via centre→vertex direction
            let centre = slice_centre(slice_idx, shape.edge_stride);
            let v      = ring_vertex(edge_idx, slice_idx);
            let diff   = v - centre;
            return normalize(vec3<f32>(diff.x, 0.0, diff.z));
        }

        fn ring_uv(edge_idx: u32, slice_idx: u32) -> vec2<f32> {
            let u = f32(edge_idx) / f32(shape.edges);
            let v = f32(slice_idx) / f32(shape.slices);
            return vec2<f32>(u, v);
        }

        // ── Vertex output ─────────────────────────────────────────────────────

        struct VertexOutput {
            @builtin(position) clip_pos  : vec4<f32>,
            @location(0)       world_pos : vec3<f32>,
            @location(1)       normal    : vec3<f32>,
            @location(2)       uv        : vec2<f32>,
        };

        // ── Vertex shader ─────────────────────────────────────────────────────
        //
        // Draw call topology:  triangle-list
        //
        // Vertex count per shape:
        //   side quads : Slices × Edges × 6  (2 triangles per quad)
        //   bottom cap : Edges × 3
        //   top    cap : Edges × 3
        //
        // vertex_index layout:
        //   [0, side_count)              → side triangles
        //   [side_count, side_count + Edges×3)   → bottom cap
        //   [side_count + Edges×3, total)         → top cap

        @vertex
        fn vs_main(@builtin(vertex_index) vid: u32) -> VertexOutput {
            let edges        = shape.edges;
            let slices       = shape.slices;
            let edge_stride  = shape.edge_stride;

            let side_verts   = slices * edges * 6u;
            let cap_verts    = edges  * 3u;

            var local_pos  : vec3<f32>;
            var local_norm : vec3<f32>;
            var uv         : vec2<f32>;

            if (vid < side_verts) {
                // ── Side triangles ────────────────────────────────────────
                // Each quad (slice s, edge e) has 6 vertices:
                //   tri0: v00, v10, v11
                //   tri1: v00, v11, v01
                // where v{ds}{de} = ring_vertex(e+de, s+ds)

                let quad_id  = vid / 6u;
                let vert_in_quad = vid % 6u;

                let s = quad_id / edges;
                let e = quad_id % edges;

                // Corner offsets per triangle vertex
                var ds: u32; var de: u32;
                switch (vert_in_quad) {
                    case 0u: { ds = 0u; de = 0u; }
                    case 1u: { ds = 1u; de = 0u; }
                    case 2u: { ds = 1u; de = 1u; }
                    case 3u: { ds = 0u; de = 0u; }
                    case 4u: { ds = 1u; de = 1u; }
                    case 5u, default: { ds = 0u; de = 1u; }
                }

                let ei = (e + de) % edges;
                let si = s + ds;

                local_pos  = ring_vertex(ei, si);
                local_norm = ring_normal(ei, si);
                uv         = ring_uv(ei, si);

            } else if (vid < side_verts + cap_verts) {
                // ── Bottom cap (slice 0) ──────────────────────────────────
                let cap_vid = vid - side_verts;
                let tri_id  = cap_vid / 3u;
                let vert_in_tri = cap_vid % 3u;

                let e0 = tri_id;
                let e1 = (tri_id + 1u) % edges;

                let centre = slice_centre(0u, edge_stride);

                switch (vert_in_tri) {
                    case 0u: {
                        local_pos  = centre;
                        local_norm = vec3<f32>(0.0, -1.0, 0.0);
                        uv         = vec2<f32>(0.5, 0.5);
                    }
                    case 1u: {
                        local_pos  = ring_vertex(e0, 0u);
                        local_norm = vec3<f32>(0.0, -1.0, 0.0);
                        let angle  = (2.0 * 3.14159265) * f32(e0) / f32(edges);
                        uv         = vec2<f32>(cos(angle) * 0.5 + 0.5, sin(angle) * 0.5 + 0.5);
                    }
                    case 2u, default: {
                        local_pos  = ring_vertex(e1, 0u);
                        local_norm = vec3<f32>(0.0, -1.0, 0.0);
                        let angle  = (2.0 * 3.14159265) * f32(e1) / f32(edges);
                        uv         = vec2<f32>(cos(angle) * 0.5 + 0.5, sin(angle) * 0.5 + 0.5);
                    }
                }

            } else {
                // ── Top cap (slice = slices) ──────────────────────────────
                let cap_vid = vid - side_verts - cap_verts;
                let tri_id  = cap_vid / 3u;
                let vert_in_tri = cap_vid % 3u;

                let e0 = tri_id;
                let e1 = (tri_id + 1u) % edges;
                let top_slice = slices;

                let centre = slice_centre(top_slice, edge_stride);

                switch (vert_in_tri) {
                    case 0u: {
                        local_pos  = centre;
                        local_norm = vec3<f32>(0.0, 1.0, 0.0);
                        uv         = vec2<f32>(0.5, 0.5);
                    }
                    case 1u: {
                        local_pos  = ring_vertex(e1, top_slice);
                        local_norm = vec3<f32>(0.0, 1.0, 0.0);
                        let angle  = (2.0 * 3.14159265) * f32(e1) / f32(edges);
                        uv         = vec2<f32>(cos(angle) * 0.5 + 0.5, sin(angle) * 0.5 + 0.5);
                    }
                    case 2u, default: {
                        local_pos  = ring_vertex(e0, top_slice);
                        local_norm = vec3<f32>(0.0, 1.0, 0.0);
                        let angle  = (2.0 * 3.14159265) * f32(e0) / f32(edges);
                        uv         = vec2<f32>(cos(angle) * 0.5 + 0.5, sin(angle) * 0.5 + 0.5);
                    }
                }
            }

            // ── Transform to clip space ───────────────────────────────────
            let world_pos4 = instance.model * vec4<f32>(local_pos, 1.0);
            let world_norm4 = instance.model * vec4<f32>(local_norm, 0.0);

            var out: VertexOutput;
            out.clip_pos  = frame.view_proj * world_pos4;
            out.world_pos = world_pos4.xyz;
            out.normal    = normalize(world_norm4.xyz);
            out.uv        = uv * instance.uv_scale + instance.uv_offset;
            return out;
        }

        // ── Fragment shader ───────────────────────────────────────────────────
        // Blinn-Phong lighting with per-shape material parameters.

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            let norm       = normalize(in.normal);
            let light_dir  = normalize(frame.light_pos - in.world_pos);
            let view_dir   = normalize(frame.camera_pos - in.world_pos);
            let half_vec   = normalize(light_dir + view_dir);

            let diff       = max(dot(norm, light_dir), 0.0);
            let spec_raw   = pow(max(dot(norm, half_vec), 0.0), instance.shininess);

            let ambient    = frame.ambient_color * instance.ambient;
            let diffuse    = frame.light_color * diff;
            let specular   = frame.light_color * spec_raw * instance.specular;

            let lighting   = ambient + diffuse + specular;
            let base_color = instance.color.rgb;

            return vec4<f32>(base_color * lighting, instance.color.a);
        }
        """;

    // ─────────────────────────────────────────────────────────────────────────
    // Fields
    // ─────────────────────────────────────────────────────────────────────────

    private readonly IWindow _window;

    private WebGPU?       _wgpu;
    private TextureFormat _swapChainFormat;

    private Instance* _instance;
    private Adapter*  _adapter;
    private Device*   _device;
    private Surface*  _surface;
    private Queue*    _queue;

    private RenderPipeline*  _pipeline;
    private BindGroupLayout* _frameBindGroupLayout;
    private BindGroupLayout* _geometryBindGroupLayout;
    private BindGroupLayout* _instanceBindGroupLayout;

    // Frame-level uniform buffer (camera + global lighting)
    private Silk.NET.WebGPU.Buffer* _frameUniformBuffer;
    private BindGroup*              _frameBindGroup;

    // Shape list and their GPU resources
    private readonly List<LegacyShape>      _shapes    = [];
    private readonly Dictionary<LegacyShape, ShapeGpuResources> _gpuResources = [];

    // Camera / lighting state
    private Vector3D<float> _cameraPosition = new(0f, 2f, 5f);
    private Vector3D<float> _lightPosition  = new(10f, 10f, 10f);
    private Vector3D<float> _lightColor     = new(1f, 1f, 1f);
    private Vector3D<float> _ambientColor   = new(0.15f, 0.15f, 0.15f);

    // Projection settings
    private const float FieldOfView    = 60f * (MathF.PI / 180f);
    private const float NearPlane      = 0.1f;
    private const float FarPlane       = 1000f;

    public bool IsReady { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────

    private sealed class ShapeGpuResources : IDisposable
    {
        public Silk.NET.WebGPU.Buffer* GeometryBuffer  { get; init; }
        public Silk.NET.WebGPU.Buffer* ShapeParamBuffer { get; init; }
        public Silk.NET.WebGPU.Buffer* InstanceBuffer  { get; init; }
        public BindGroup*              GeometryBindGroup { get; init; }
        public BindGroup*              InstanceBindGroup { get; init; }
        public uint                    VertexCount      { get; init; }

        private readonly WebGPU    _wgpu;
        private readonly Device*   _device;

        public ShapeGpuResources(WebGPU wgpu, Device* device)
        {
            _wgpu   = wgpu;
            _device = device;
        }

        public void Dispose()
        {
            if (GeometryBuffer   is not null) _wgpu.BufferDestroy(GeometryBuffer);
            if (ShapeParamBuffer is not null) _wgpu.BufferDestroy(ShapeParamBuffer);
            if (InstanceBuffer   is not null) _wgpu.BufferDestroy(InstanceBuffer);
            if (GeometryBindGroup  is not null) _wgpu.BindGroupRelease(GeometryBindGroup);
            if (InstanceBindGroup  is not null) _wgpu.BindGroupRelease(InstanceBindGroup);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public LegacyRenderer(IWindow window)
    {
        _window = window;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void AddShape(LegacyShape shape)
    {
        if (_shapes.Contains(shape)) return;
        _shapes.Add(shape);

        if (IsReady)
            CreateShapeGpuResources(shape);
    }

    public void RemoveShape(LegacyShape shape)
    {
        if (!_shapes.Remove(shape)) return;

        if (_gpuResources.Remove(shape, out var res))
            res.Dispose();
    }

    public void SetCameraPosition(Vector3D<float> position)    => _cameraPosition = position;
    public void SetCameraPosition(float x, float y, float z)   => _cameraPosition = new(x, y, z);

    public void SetLightPosition(Vector3D<float> position)     => _lightPosition  = position;
    public void SetLightColor(Vector3D<float> color)           => _lightColor     = color;
    public void SetAmbientColor(Vector3D<float> color)         => _ambientColor   = color;

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────

    public void Initialize()
    {
        _wgpu = WebGPU.GetApi();

        var instanceDesc = new InstanceDescriptor();
        _instance = _wgpu.CreateInstance(&instanceDesc);
        _surface  = _window.CreateWebGPUSurface(_wgpu, _instance);

        var adapterOptions = new RequestAdapterOptions
        {
            CompatibleSurface = _surface,
            PowerPreference   = PowerPreference.HighPerformance
        };

        _wgpu.InstanceRequestAdapter(
            _instance,
            &adapterOptions,
            PfnRequestAdapterCallback.From(OnAdapterReceived),
            null);
    }

    private void OnAdapterReceived(RequestAdapterStatus status, Adapter* adapter, byte* message, void* userdata)
    {
        if (status is not RequestAdapterStatus.Success || adapter is null)
            throw new Exception("WebGPU failed to allocate a physical hardware adapter.");

        _adapter = adapter;

        var deviceDesc = new DeviceDescriptor();
        _wgpu!.AdapterRequestDevice(
            adapter,
            &deviceDesc,
            PfnRequestDeviceCallback.From((devStatus, device, msg, data) =>
                OnDeviceReceived(devStatus, device)),
            null);
    }

    private void OnDeviceReceived(RequestDeviceStatus status, Device* device)
    {
        if (status is not RequestDeviceStatus.Success || device is null)
            throw new Exception("WebGPU failed to initialize the virtual execution device context.");

        _device = device;
        _queue  = _wgpu!.DeviceGetQueue(device);

        // ── Configure the swap chain ──────────────────────────────────────
        var caps = new SurfaceCapabilities();
        _wgpu.SurfaceGetCapabilities(_surface, _adapter, &caps);

        _swapChainFormat = (caps.FormatCount > 0 && caps.Formats is not null)
            ? *caps.Formats
            : TextureFormat.Bgra8Unorm;

        var surfaceCfg = new SurfaceConfiguration
        {
            Device      = _device,
            Format      = _swapChainFormat,
            Usage       = TextureUsage.RenderAttachment,
            PresentMode = PresentMode.Fifo,
            Width       = (uint)_window.FramebufferSize.X,
            Height      = (uint)_window.FramebufferSize.Y,
            AlphaMode   = CompositeAlphaMode.Opaque
        };

        _wgpu.SurfaceConfigure(_surface, &surfaceCfg);
        _wgpu.SurfaceCapabilitiesFreeMembers(caps);

        // ── Build pipeline ────────────────────────────────────────────────
        BuildPipeline();
        CreateFrameUniformBuffer();

        // ── Upload any shapes that were added before initialization ───────
        foreach (var shape in _shapes)
            CreateShapeGpuResources(shape);

        IsReady = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Render
    // ─────────────────────────────────────────────────────────────────────────

    public void Render(double deltaTime)
    {
        if (!IsReady) return;

        // ── Acquire current surface texture ───────────────────────────────
        var surfaceTexture = new SurfaceTexture();
        _wgpu!.SurfaceGetCurrentTexture(_surface, &surfaceTexture);

        if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
            return;

        var textureViewDesc = new TextureViewDescriptor
        {
            Format          = _swapChainFormat,
            Dimension       = TextureViewDimension.Dimension2D,
            MipLevelCount   = 1,
            ArrayLayerCount = 1,
            Aspect          = TextureAspect.All
        };
        var textureView = _wgpu.TextureCreateView(surfaceTexture.Texture, &textureViewDesc);

        // ── Update frame uniforms ─────────────────────────────────────────
        UpdateFrameUniforms((float)deltaTime);

        // ── Update dirty shapes ───────────────────────────────────────────
        foreach (var shape in _shapes)
        {
            if (!_gpuResources.TryGetValue(shape, out var res)) continue;

            if (shape.GeometryDirty)
                UploadGeometryBuffer(shape, res);

            if (shape.TransformDirty)
                UploadInstanceBuffer(shape, res);
        }

        // ── Begin render pass ─────────────────────────────────────────────
        var commandEncoderDesc = new CommandEncoderDescriptor();
        var encoder = _wgpu.DeviceCreateCommandEncoder(_device, &commandEncoderDesc);

        var colorAttachment = new RenderPassColorAttachment
        {
            View          = textureView,
            LoadOp        = LoadOp.Clear,
            StoreOp       = StoreOp.Store,
            ClearValue    = new Color { R = 0.05, G = 0.05, B = 0.08, A = 1.0 }
        };

        var renderPassDesc = new RenderPassDescriptor
        {
            ColorAttachmentCount = 1,
            ColorAttachments     = &colorAttachment
        };

        var renderPass = _wgpu.CommandEncoderBeginRenderPass(encoder, &renderPassDesc);
        _wgpu.RenderPassEncoderSetPipeline(renderPass, _pipeline);
        _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _frameBindGroup, 0, null);

        foreach (var shape in _shapes)
        {
            if (!_gpuResources.TryGetValue(shape, out var res)) continue;

            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 1, res.GeometryBindGroup, 0, null);
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 2, res.InstanceBindGroup, 0, null);
            _wgpu.RenderPassEncoderDraw(renderPass, res.VertexCount, 1, 0, 0);
        }

        _wgpu.RenderPassEncoderEnd(renderPass);

        var cmdBufferDesc = new CommandBufferDescriptor();
        var cmdBuffer = _wgpu.CommandEncoderFinish(encoder, &cmdBufferDesc);
        _wgpu.QueueSubmit(_queue, 1, &cmdBuffer);

        _wgpu.SurfacePresent(_surface);

        _wgpu.TextureViewRelease(textureView);
        _wgpu.CommandBufferRelease(cmdBuffer);
        _wgpu.CommandEncoderRelease(encoder);
        _wgpu.RenderPassEncoderRelease(renderPass);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pipeline construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildPipeline()
    {
        // ── Shader module ─────────────────────────────────────────────────
        var shaderSource = ShaderSource;
        ShaderModule* shaderModule;

        fixed (byte* src = System.Text.Encoding.UTF8.GetBytes(shaderSource + "\0"))
        {
            var wgslDesc = new ShaderModuleWGSLDescriptor
            {
                Code  = src,
                Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor }
            };

            var shaderDesc = new ShaderModuleDescriptor
            {
                NextInChain = (ChainedStruct*)&wgslDesc
            };

            shaderModule = _wgpu!.DeviceCreateShaderModule(_device, &shaderDesc);
        }

        // ── Bind group layouts ────────────────────────────────────────────

        // Group 0: frame uniforms
        var frameEntry = new BindGroupLayoutEntry
        {
            Binding    = 0,
            Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
            Buffer     = new BufferBindingLayout { Type = BufferBindingType.Uniform }
        };

        var frameLayoutDesc = new BindGroupLayoutDescriptor
        {
            EntryCount = 1,
            Entries    = &frameEntry
        };
        _frameBindGroupLayout = _wgpu!.DeviceCreateBindGroupLayout(_device, &frameLayoutDesc);

        // Group 1: geometry (storage buffer + shape params uniform)
        var geoEntries = stackalloc BindGroupLayoutEntry[2];
        geoEntries[0] = new BindGroupLayoutEntry
        {
            Binding    = 0,
            Visibility = ShaderStage.Vertex,
            Buffer     = new BufferBindingLayout
            {
                Type              = BufferBindingType.ReadOnlyStorage,
                MinBindingSize    = 0,
                HasDynamicOffset  = false
            }
        };
        geoEntries[1] = new BindGroupLayoutEntry
        {
            Binding    = 1,
            Visibility = ShaderStage.Vertex,
            Buffer     = new BufferBindingLayout { Type = BufferBindingType.Uniform }
        };

        var geoLayoutDesc = new BindGroupLayoutDescriptor
        {
            EntryCount = 2,
            Entries    = geoEntries
        };
        _geometryBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, &geoLayoutDesc);

        // Group 2: per-instance uniform
        var instEntry = new BindGroupLayoutEntry
        {
            Binding    = 0,
            Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
            Buffer     = new BufferBindingLayout { Type = BufferBindingType.Uniform }
        };

        var instLayoutDesc = new BindGroupLayoutDescriptor
        {
            EntryCount = 1,
            Entries    = &instEntry
        };
        _instanceBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, &instLayoutDesc);

        // ── Pipeline layout ───────────────────────────────────────────────
        var bindGroupLayouts = stackalloc BindGroupLayout*[3]
        {
            _frameBindGroupLayout,
            _geometryBindGroupLayout,
            _instanceBindGroupLayout
        };

        var pipelineLayoutDesc = new PipelineLayoutDescriptor
        {
            BindGroupLayoutCount = 3,
            BindGroupLayouts     = bindGroupLayouts
        };
        var pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, &pipelineLayoutDesc);

        // ── Blend & color target ──────────────────────────────────────────
        var blendState = new BlendState
        {
            Color = new BlendComponent
            {
                SrcFactor = BlendFactor.SrcAlpha,
                DstFactor = BlendFactor.OneMinusSrcAlpha,
                Operation = BlendOperation.Add
            },
            Alpha = new BlendComponent
            {
                SrcFactor = BlendFactor.One,
                DstFactor = BlendFactor.Zero,
                Operation = BlendOperation.Add
            }
        };

        var colorTarget = new ColorTargetState
        {
            Format    = _swapChainFormat,
            Blend     = &blendState,
            WriteMask = ColorWriteMask.All
        };

        // ── Entry point name bytes ────────────────────────────────────────
        byte[] vsBytes = System.Text.Encoding.UTF8.GetBytes("vs_main\0");
        byte[] fsBytes = System.Text.Encoding.UTF8.GetBytes("fs_main\0");

        fixed (byte* vsEntry = vsBytes)
        fixed (byte* fsEntry = fsBytes)
        {
            var fragmentState = new FragmentState
            {
                Module      = shaderModule,
                EntryPoint  = fsEntry,
                TargetCount = 1,
                Targets     = &colorTarget
            };

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout  = pipelineLayout,
                Vertex  = new VertexState
                {
                    Module      = shaderModule,
                    EntryPoint  = vsEntry,
                    BufferCount = 0,   // no vertex buffers — fully procedural
                    Buffers     = null
                },
                Fragment  = &fragmentState,
                Primitive = new PrimitiveState
                {
                    Topology         = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace        = FrontFace.Ccw,
                    CullMode         = CullMode.Back
                },
                Multisample = new MultisampleState
                {
                    Count                  = 1,
                    Mask                   = ~0u,
                    AlphaToCoverageEnabled = false
                },
                DepthStencil = null
            };

            _pipeline = _wgpu.DeviceCreateRenderPipeline(_device, &pipelineDesc);
        }

        _wgpu.ShaderModuleRelease(shaderModule);
        _wgpu.PipelineLayoutRelease(pipelineLayout);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Buffer helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void CreateFrameUniformBuffer()
    {
        // Layout (must match FrameUniforms in WGSL):
        //   view_proj     : mat4x4<f32>  = 64 bytes
        //   camera_pos    : vec3<f32>    = 12 bytes  (+4 pad)
        //   time          : f32          =  4 bytes
        //   light_pos     : vec3<f32>    = 12 bytes  (+4 pad)
        //   light_color   : vec3<f32>    = 12 bytes  (+4 pad)
        //   ambient_color : vec3<f32>    = 12 bytes  (+4 pad)
        // Total = 64 + 16 + 16 + 16 + 16 = 128 bytes

        const ulong size = 128;

        var bufferDesc = new BufferDescriptor
        {
            Size             = size,
            Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
            MappedAtCreation = false
        };

        _frameUniformBuffer = _wgpu!.DeviceCreateBuffer(_device, &bufferDesc);

        // ── Bind group ────────────────────────────────────────────────────
        var entry = new BindGroupEntry
        {
            Binding = 0,
            Buffer  = _frameUniformBuffer,
            Offset  = 0,
            Size    = size
        };

        var bgDesc = new BindGroupDescriptor
        {
            Layout     = _frameBindGroupLayout,
            EntryCount = 1,
            Entries    = &entry
        };

        _frameBindGroup = _wgpu.DeviceCreateBindGroup(_device, &bgDesc);
    }

    private void UpdateFrameUniforms(float time)
    {
        // View matrix: look-at (camera→origin)
        var eye    = _cameraPosition;
        var target = Vector3D<float>.Zero;
        var up     = Vector3D<float>.UnitY;
        var view   = Matrix4X4.CreateLookAt(eye, target, up);

        float aspect = _window.FramebufferSize.X / (float)_window.FramebufferSize.Y;
        var proj = Matrix4X4.CreatePerspectiveFieldOfView(FieldOfView, aspect, NearPlane, FarPlane);

        var viewProj = view * proj;

        Span<float> data = stackalloc float[32];
        int i = 0;

        // view_proj (column-major storage → transpose for row-major WGSL)
        data[i++] = viewProj.M11; data[i++] = viewProj.M12; data[i++] = viewProj.M13; data[i++] = viewProj.M14;
        data[i++] = viewProj.M21; data[i++] = viewProj.M22; data[i++] = viewProj.M23; data[i++] = viewProj.M24;
        data[i++] = viewProj.M31; data[i++] = viewProj.M32; data[i++] = viewProj.M33; data[i++] = viewProj.M34;
        data[i++] = viewProj.M41; data[i++] = viewProj.M42; data[i++] = viewProj.M43; data[i++] = viewProj.M44;

        data[i++] = eye.X; data[i++] = eye.Y; data[i++] = eye.Z; data[i++] = time;
        data[i++] = _lightPosition.X; data[i++] = _lightPosition.Y; data[i++] = _lightPosition.Z; data[i++] = 0f;
        data[i++] = _lightColor.X; data[i++] = _lightColor.Y; data[i++] = _lightColor.Z; data[i++] = 0f;
        data[i++] = _ambientColor.X; data[i++] = _ambientColor.Y; data[i++] = _ambientColor.Z; data[i++] = 0f;

        fixed (float* ptr = data)
            _wgpu!.QueueWriteBuffer(_queue, _frameUniformBuffer, 0, ptr, (nuint)(data.Length * sizeof(float)));
    }

    private void CreateShapeGpuResources(LegacyShape shape)
    {
        int edges      = shape.Edges;
        int slices     = shape.Slices;
        int edgeStride = ((edges + 3) / 4) * 4;

        // ── Geometry storage buffer ───────────────────────────────────────
        float[] geoData   = shape.BakeGeometryData();
        ulong   geoBytes  = (ulong)(geoData.Length * sizeof(float));

        var geoBufferDesc = new BufferDescriptor
        {
            Size             = geoBytes,
            Usage            = BufferUsage.Storage | BufferUsage.CopyDst,
            MappedAtCreation = false
        };
        var geoBuffer = _wgpu!.DeviceCreateBuffer(_device, &geoBufferDesc);
        fixed (float* ptr = geoData)
            _wgpu.QueueWriteBuffer(_queue, geoBuffer, 0, ptr, (nuint)geoBytes);

        // ── Shape params uniform ──────────────────────────────────────────
        // struct ShapeParams { edges, slices, edge_stride, _pad } = 4× u32 = 16 bytes
        const ulong paramBytes = 16;
        uint[] paramData = [(uint)edges, (uint)slices, (uint)edgeStride, 0u];

        var paramBufferDesc = new BufferDescriptor
        {
            Size             = paramBytes,
            Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
            MappedAtCreation = false
        };
        var paramBuffer = _wgpu.DeviceCreateBuffer(_device, &paramBufferDesc);
        fixed (uint* ptr = paramData)
            _wgpu.QueueWriteBuffer(_queue, paramBuffer, 0, ptr, (nuint)paramBytes);

        // ── Instance uniform buffer ───────────────────────────────────────
        float[] instData  = shape.BakeInstanceData();
        ulong   instBytes = (ulong)(instData.Length * sizeof(float));

        var instBufferDesc = new BufferDescriptor
        {
            Size             = instBytes,
            Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
            MappedAtCreation = false
        };
        var instBuffer = _wgpu.DeviceCreateBuffer(_device, &instBufferDesc);
        fixed (float* ptr = instData)
            _wgpu.QueueWriteBuffer(_queue, instBuffer, 0, ptr, (nuint)instBytes);

        // ── Geometry bind group (group 1) ─────────────────────────────────
        var geoEntries = stackalloc BindGroupEntry[2];
        geoEntries[0] = new BindGroupEntry { Binding = 0, Buffer = geoBuffer,   Offset = 0, Size = geoBytes   };
        geoEntries[1] = new BindGroupEntry { Binding = 1, Buffer = paramBuffer, Offset = 0, Size = paramBytes  };

        var geoBgDesc = new BindGroupDescriptor
        {
            Layout     = _geometryBindGroupLayout,
            EntryCount = 2,
            Entries    = geoEntries
        };
        var geoBg = _wgpu.DeviceCreateBindGroup(_device, &geoBgDesc);

        // ── Instance bind group (group 2) ─────────────────────────────────
        var instEntry = new BindGroupEntry
        {
            Binding = 0,
            Buffer  = instBuffer,
            Offset  = 0,
            Size    = instBytes
        };

        var instBgDesc = new BindGroupDescriptor
        {
            Layout     = _instanceBindGroupLayout,
            EntryCount = 1,
            Entries    = &instEntry
        };
        var instBg = _wgpu.DeviceCreateBindGroup(_device, &instBgDesc);

        // ── Vertex count ──────────────────────────────────────────────────
        uint sideVerts = (uint)(slices * edges * 6);
        uint capVerts  = (uint)(edges  * 3);
        uint totalVerts = sideVerts + capVerts * 2;

        var res = new ShapeGpuResources(_wgpu, _device)
        {
            GeometryBuffer    = geoBuffer,
            ShapeParamBuffer  = paramBuffer,
            InstanceBuffer    = instBuffer,
            GeometryBindGroup = geoBg,
            InstanceBindGroup = instBg,
            VertexCount       = totalVerts
        };

        _gpuResources[shape] = res;
    }

    private void UploadGeometryBuffer(LegacyShape shape, ShapeGpuResources res)
    {
        float[] data  = shape.BakeGeometryData();
        ulong   bytes = (ulong)(data.Length * sizeof(float));
        fixed (float* ptr = data)
            _wgpu!.QueueWriteBuffer(_queue, res.GeometryBuffer, 0, ptr, (nuint)bytes);
    }

    private void UploadInstanceBuffer(LegacyShape shape, ShapeGpuResources res)
    {
        float[] data  = shape.BakeInstanceData();
        ulong   bytes = (ulong)(data.Length * sizeof(float));
        fixed (float* ptr = data)
            _wgpu!.QueueWriteBuffer(_queue, res.InstanceBuffer, 0, ptr, (nuint)bytes);

        shape.ClearTransformDirty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Disposal
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var res in _gpuResources.Values)
            res.Dispose();
        _gpuResources.Clear();

        if (_wgpu is null) return;

        if (_frameBindGroup      is not null) _wgpu.BindGroupRelease(_frameBindGroup);
        if (_frameUniformBuffer  is not null) _wgpu.BufferDestroy(_frameUniformBuffer);
        if (_pipeline            is not null) _wgpu.RenderPipelineRelease(_pipeline);
        if (_frameBindGroupLayout    is not null) _wgpu.BindGroupLayoutRelease(_frameBindGroupLayout);
        if (_geometryBindGroupLayout is not null) _wgpu.BindGroupLayoutRelease(_geometryBindGroupLayout);
        if (_instanceBindGroupLayout is not null) _wgpu.BindGroupLayoutRelease(_instanceBindGroupLayout);
        if (_queue   is not null) _wgpu.QueueRelease(_queue);
        if (_device  is not null) _wgpu.DeviceRelease(_device);
        if (_adapter is not null) _wgpu.AdapterRelease(_adapter);
        if (_surface is not null) _wgpu.SurfaceRelease(_surface);
        if (_instance is not null) _wgpu.InstanceRelease(_instance);

        _wgpu.Dispose();
    }
}