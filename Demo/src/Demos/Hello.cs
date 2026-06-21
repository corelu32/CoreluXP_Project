using LUmaKE.Core;
using LUmaKE.Applications;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var window = new Window("Hello LUmaKE");
        var platform = new SdlApplication();
        platform.AddWindow(window);
        window.Run();
        platform.RemoveWindow(window);
    }
}