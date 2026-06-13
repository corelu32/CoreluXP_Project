
using System;

#if ENABLE_PROTOTYPE_DEMOS

using Prototype.V1.Core;
using Prototype.V1.Graphics;
using Prototype.V1.Primitives;

#endif

namespace Demo;

public static class ProtoV1
{
    public static void Run()
    {

#if ENABLE_PROTOTYPE_DEMOS
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
#else
        Console.WriteLine("Prototypes are disabled.");
#endif

    }
}