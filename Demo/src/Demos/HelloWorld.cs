using CoreluXP.Applications;
using CoreluXP.Graphics;
using CoreluXP.Renderers;
using Silk.NET.Maths;

namespace Demo;

public static class HelloWorldDemo
{
    public static void Run()
    {
        DesktopApplication app = new("Corelu XP | Legacy Shape Demo", 800, 600);
        LegacyRenderer renderer = new(app.SilkContext.Window);

        double len = 0.2;
        
        var cylinder = new LegacyShape(
            edges: 120,
            slices: 24,
            pathFunction: s =>
            new Vector3D<double>(0, s / (double)12 - 0.5, 0),
            profileRadius: (e, s) => (Math.Floor(s*0.15)*0.7+0.1 + e*0.01) * 0.3);
                
    
        cylinder.SetPosition(0f, -2.6f, 0f)
                .SetColor(0.2f, 0.6f, 1.0f)
                .SetShininess(2);
    
        var spiral = new LegacyShape(
            edges: 3,
            slices: 20,
            pathFunction: s => new Vector3D<double>(
                Math.Cos(s * 0.5) * len,
                s / 5.0,
                Math.Sin(s * 0.5) * len),
            profileRadius: (e, s) => 0.02*s+0.2);
        
        spiral.SetPosition(0f, -1.3f, 0f);
        spiral.SetShininess(3);
        spiral.SetColor(0.2f, 0.7f, 0.7f, 0.5f);
    
        renderer.AddShape(cylinder);
        renderer.AddShape(spiral);
        renderer.SetCameraPosition(0f, 3f, 6f);
    
        app.OnLoad += renderer.Initialize;
    
        double totalTime = 0.0;
    
        app.OnUpdate += delta =>
        {
            totalTime += delta;
    
            cylinder.SetRotationEuler(0f, (float)totalTime, 0f);
            spiral.SetRotationEuler(0f, (float)totalTime*8.0f, 0f);
    
            renderer.Render(delta);
            len += 0.01;
        };
    
        app.Run();
    }
}