using CoreluXP.Core;
using CoreluXP.Primitives;

Application app = new("Corelu XP DEMO", 800, 600, new SubsystemProfile(Subsystem.Video));

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
    app.WriteDebug($"Framerate {1 / delta}");
};

app.Run();