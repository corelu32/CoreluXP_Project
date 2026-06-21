using LUmaKE.Core;
using LUmaKE.Platforms;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var window = new Window("Hello LUmaKE");
        var platform = new SdlPlatform();
        platform.RegisterWindow(window);
        window.Run();
        platform.UnregisterWindow(window);
    }
}