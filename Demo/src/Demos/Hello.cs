using LuKe3DX.Applications;
using LuKe3DX.Primitives;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        DesktopApplication app = new()
        {
            Title = "Hello LuKe3DX",
            Width = 800,
            Height = 600,
            TargetFramerate = 60
        };

        app.OnKeyDown += (key) =>
        {
            if (key is Keycode.Escape)
                app.Stop();
        };

        app.OnUpdate += (delta) =>
        {
            
        };

        app.Run();
    }
}