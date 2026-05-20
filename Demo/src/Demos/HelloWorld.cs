using CoreluXP.Applications;
using CoreluXP.Graphics;
using CoreluXP.Renderers;
using Silk.NET.Maths;

namespace Demo;

public static class HelloWorldDemo
{
    public static void Run()
    {
        DesktopApplication app = new("CoreluXP | HELLO WORLD DEMO", 800, 600);
        LegacyRenderer renderer = new(app.SilkContext.Window);

        double len = 0.2;
        
        var cylinder = new LegacyShape(
            edges: 16,
            slices: 24);
                
    
        cylinder.SetPosition(-2f, 0f, 0f)
                .SetColor(0.2f, 0.6f, 1.0f)
                .SetShininess(0);
    
        var spiral = new LegacyShape(
            edges: 12,
            slices: 20,
            pathFunction: s => new Vector3D<double>(
                Math.Cos(s * 0.5) * len,
                s / 5.0,
                Math.Sin(s * 0.5) * len),
            profileRadius: (e, s) => 0.3);
        
        spiral.SetPosition(2f, -2f, 0f);
        spiral.SetShininess(3);
    
        renderer.AddShape(cylinder);
        renderer.AddShape(spiral);
        renderer.SetCameraPosition(0f, 3f, 6f);
    
        app.OnLoad += renderer.Initialize;
    
        double totalTime = 0.0;
    
        app.OnUpdate += delta =>
        {
            totalTime += delta;
    
            cylinder.SetRotationEuler((float)totalTime, 0f, 0f);
            spiral.SetRotationEuler(0f, (float)totalTime, 0f);
    
            renderer.Render(delta);
            len += 0.01;
        };
    
        app.Run();
    }
}