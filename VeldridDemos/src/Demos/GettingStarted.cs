using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;

namespace GettingStarted;

struct Vertex(Vector2 position, RgbaFloat color)
{
    public Vector2 Position = position;
    public RgbaFloat Color = color;
    public const uint SizeInBytes = 24;
}

public class Demo
{
    private static GraphicsDevice _graphicsDevice = null!;
    private static CommandList    _commandList    = null!;
    private static DeviceBuffer   _vertexBuffer   = null!;
    private static DeviceBuffer   _indexBuffer    = null!;
    private static Shader         _vertexShader   = null!;
    private static Shader         _fragmentShader = null!;
    private static Pipeline       _pipeline       = null!;

    public static void Run()
    {
        WindowCreateInfo windowInfo = new()
        {
            WindowTitle = "Getting Started Demo",
            WindowWidth = 960,
            WindowHeight = 540
        };

        Sdl2Window window = VeldridStartup.CreateWindow(ref windowInfo);

        GraphicsDeviceOptions graphicsOptions = new()
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };

        _graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, graphicsOptions);

        Initialize();

        while (window.Exists)
        {
            window.PumpEvents();
            Draw();
        }

        Dispose();
    }

    private static void Initialize()
    {
        ResourceFactory factory = _graphicsDevice.ResourceFactory;

        // Create the vertices.
        Vertex[] vertices = [
            new( position: new(-0.75f, +0.75f), color: new(1, 0, 0, 0) ),
            new( position: new(+0.75f, +0.75f), color: new(1, 1, 0, 1) ),
            new( position: new(-0.75f, -0.75f), color: new(0, 1, 1, 1) ),
            new( position: new(+0.75f, -0.75f), color: new(0, 0, 1, 1) )
        ];

        // Create the indices (order to draw the vertices).
        ushort[] indices = [0, 1, 2, 3];

        // Create buffers accessible to the GPU.
        _vertexBuffer = factory.CreateBuffer( new BufferDescription((uint) vertices.Length * Vertex.SizeInBytes, BufferUsage.VertexBuffer) );
        _indexBuffer  = factory.CreateBuffer( new BufferDescription((uint) indices.Length  * sizeof(ushort),     BufferUsage.IndexBuffer)  );

        // Populate the buffers.
        _graphicsDevice.UpdateBuffer(_vertexBuffer, 0, vertices);
        _graphicsDevice.UpdateBuffer(_indexBuffer, 0, indices);

        // Define the vertex layout (layout of the verticies buffer).
        // These will correspond to the vertex shader's "IN" variables.
        VertexLayoutDescription vertexLayout = new(
            
            // location = 0
            new VertexElementDescription(
                "Position",
                VertexElementSemantic.TextureCoordinate,
                VertexElementFormat.Float2),

            // location = 1
            new VertexElementDescription(
                "Color",
                VertexElementSemantic.TextureCoordinate,
                VertexElementFormat.Float4)
        );

        ShaderDescription vertexShaderDesc = new(
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(VertexShaderCode),
            "main");
        
        ShaderDescription fragmentShaderDesc = new(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(FragmentShaderCode),
            "main");
        
        var shaders     = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
        _vertexShader   = shaders[0];
        _fragmentShader = shaders[1];

        GraphicsPipelineDescription pipelineDesc = new()
        {
            BlendState        = BlendStateDescription.SingleOverrideBlend,
            PrimitiveTopology = PrimitiveTopology.TriangleStrip,
            ResourceLayouts   = [],
            Outputs           = _graphicsDevice.SwapchainFramebuffer.OutputDescription,

            DepthStencilState = new(
                depthTestEnabled: true,
                depthWriteEnabled: true,
                comparisonKind: ComparisonKind.LessEqual),
            
            RasterizerState = new(
                cullMode: FaceCullMode.Back,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false),

            ShaderSet = new(
                vertexLayouts: [vertexLayout],
                shaders: shaders)
        };

        _pipeline = factory.CreateGraphicsPipeline(pipelineDesc);
        _commandList = factory.CreateCommandList();
    }

    private static void Draw()
    {
        _commandList.Begin();
        _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Black);

        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _commandList.SetPipeline(_pipeline);
        _commandList.DrawIndexed(
            indexCount:    4,
            instanceCount: 1,
            indexStart:    0,
            vertexOffset:  0,
            instanceStart: 0);
        
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
        _graphicsDevice.SwapBuffers();
    }

    private static void Dispose()
    {
        _pipeline.Dispose();
        _vertexShader.Dispose();
        _fragmentShader.Dispose();
        _commandList.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _graphicsDevice.Dispose();
    }



    private const string VertexShaderCode = @"
#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}
";



    private const string FragmentShaderCode = @"
#version 450

layout(location = 0) in  vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}
";
}