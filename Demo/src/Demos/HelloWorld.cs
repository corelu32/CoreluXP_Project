using CoreluXP.Applications;

namespace Demo;

public static class HelloWorldDemo
{
    public static void Run()
    {
        DesktopApplication app = new("Tot3D | HELLO WORLD DEMO", 800, 600);
        
        app.OnLoad += () =>
        {
            
        };
        
        app.OnUpdate += (delta) =>
        {
            
        };
        
        app.Run();
    }
}