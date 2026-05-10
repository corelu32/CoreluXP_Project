using CoreluXP.Core;
using CoreluXP.Graphics;
using CoreluXP.Primitives;

Application app = new("Corelu XP DEMO", 800, 600, new SubsystemProfile(Subsystem.Video));
DebugText text = new();

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
    text.Text = $"Framerate {1 / delta}";
    app.Draw(text);
};

app.Run();