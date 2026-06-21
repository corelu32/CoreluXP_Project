using LUmaKE.Applications;
using LUmaKE.Primitives;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var app = Application.Create(Platform.SDL, "LUmaKE Demo", 800, 600);

        app.OnKeyDown += (keycode) =>
        {
            if (keycode is Keycode.Escape)
                app.Close();
        };
        
        app.Run();
    }
}