using CoreluXP.Core;
using CoreluXP.Graphics;
using CoreluXP.Primitives;

namespace Demo;

public static class HelloWorldDemo
{
    public static void Run()
    {
        Application app = new("Corelu XP | Hello World Demo", 800, 600, new SubsystemProfile(Subsystem.Video));
        DebugText text = new("Hello, world!", 350, 290);
        
        app.OnStart += () =>
        {
            app.SetTargetFramerate(60);
            app.EnableVSync(false);
        };
        
        app.OnKeyDown += (key) =>
        {
            if (key is KeyCode.Escape)
                app.Stop();
        };
        
        app.OnUpdate += (delta) =>
        {
            app.Draw(text);
        };
        
        app.Run();
    }
}