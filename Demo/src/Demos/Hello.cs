using LUmaKE.Core;
using LUmaKE.Applications;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        IApplication app = Application.Create(Platform.SDL);
        Window window = new("Hello LUmaKE", 800, 600);
        
        app.AddWindow(window);
        window.Run();
        app.RemoveWindow(window);
    }
}