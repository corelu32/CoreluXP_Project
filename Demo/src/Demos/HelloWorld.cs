using CoreluXP.Applications;
using CoreluXP.Renderers;

namespace Demo;

public static class HelloWorldDemo
{
    public static void Run()
    {
        DesktopApplication app = new("Tot3D | HELLO WORLD DEMO", 800, 600);
        LegacyRenderer renderer = new(app.SilkContext.Window);
        
        app.OnLoad   += renderer.Initialize;
        app.OnUpdate += renderer.Render;
        
        app.Run();
    }
}