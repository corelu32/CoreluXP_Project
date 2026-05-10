using CoreluXP.Core;
using CoreluXP.Primitives;

namespace Demo;

public static class Demo
{
    private readonly static Application App = new("Corelu XP DEMO", 800, 600, new SubsystemProfile(Subsystem.Video));
    public static void Main() => App.Run();
    
    [OnStart]
    public static void OnStart()
    {
        App.SetTargetFramerate(60);
        App.EnableVSync(false);
    }

    [OnKeyDown]
    public static void OnKeyDown(KeyCode key)
    {
        if (key is KeyCode.Escape)
            App.Stop();
    }

    [OnUpdate]
    public static void OnUpdate(float delta)
    {
        App.WriteDebug($"Framerate {1/delta}");
    }
}