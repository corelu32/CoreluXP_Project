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
            #version 450
            
            const vec3 positions[3] = vec3[3](
                vec3( 0.0,  0.5, 0.0),
                vec3( 0.5, -0.5, 0.0),
                vec3(-0.5, -0.5, 0.0)
            );
            
            void main() {
                gl_Position = vec4(positions[gl_VertexIndex], 1.0);
            }
            """,
            "main",
            ShaderStage.Vertex,
            ShaderFormat.Glsl);
        
        var fragmentShader = new GpuShader(
            """
            #version 450
            
            layout(location = 0) out vec4 fragColor;
            
            void main() {
                fragColor = vec4(1.0, 0.0, 0.0, 1.0);
            }
            """,
            "main",
            ShaderStage.Fragment,
            ShaderFormat.Glsl);

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

        app.OnClose += () =>
        {
            app.UnloadPipeline(pipeline);
        };

        app.OnUpdate += (delta) =>
        {
            app.BindPipeline(pipeline);
            app.DrawPrimitives(0, 3, 0, 1);
        };
        
        app.OnKeyDown += (keycode) =>
        {
            if (keycode is Keycode.Escape)
                app.Close();
        };
        
        app.Run();
    }
}