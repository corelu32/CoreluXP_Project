using LUmosaiKE.Applications;
using LUmosaiKE.Graphics;
using LUmosaiKE.Primitives;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        MosaicCanvas canvas = new();
        
        DesktopApplication app = new()
        {
            Title = "Hello LUmosaiKE",
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
            app.Draw(canvas);
        };

        app.Run();
    }
}