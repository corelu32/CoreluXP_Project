using LUmaKE.Applications;
using LUmaKE.Graphics.Gpu;
using LUmaKE.Primitives;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var app = Application.Create(Platform.SDL, "LUmaKE Demo", 800, 600);

        var vertexShader = new GpuShader(
            """
            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv       : TEXCOORD0;
            };
            
            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv       : TEXCOORD0;
            };
            
            VertexOutput VertexMain(VertexInput input)
            {
                VertexOutput output;
                output.position = input.position; 
                output.uv = input.uv;
                return output;
            }
            """,
            "VertexMain",
            ShaderStage.Vertex,
            ShaderFormat.Hlsl);

        var fragmentShader = new GpuShader(
            """
            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv       : TEXCOORD0;
            };
            
            float4 FragmentMain(VertexOutput input) : SV_Target
            {
                return float4(1.0, 0.0, 0.0, 1.0); 
            }

            """,
            "FragmentMain",
            ShaderStage.Fragment,
            ShaderFormat.Hlsl);

        var vertexLayout = new VertexLayout();
        
        var pipeline = new GpuPipeline(
            vertexShader,
            fragmentShader,
            PrimitiveTopology.Triangles,
            vertexLayout,
            [ new ColorTargetDescription(TextureFormat.Rgba8Unorm) ]);

        app.OnStart += () =>
        {
            app.LoadPipeline(pipeline);
        };
        
        app.OnKeyDown += (keycode) =>
        {
            if (keycode is Keycode.Escape)
                app.Close();
        };
        
        app.Run();
    }
}