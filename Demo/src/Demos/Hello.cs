using LUmosaiKE.Core;
using LUmosaiKE.Platforms;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var window = new Window("Hello LUmosaiKE");
        var platform = new SdlPlatform();
        platform.RegisterWindow(window);
        window.Run();
        platform.UnregisterWindow(window);
    }
}