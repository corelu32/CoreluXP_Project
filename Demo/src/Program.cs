using CoreluXP.Core;
using CoreluXP.Primitives;

var app = new Application("Corelu XP Demo", 800, 600, new(Subsystem.Video));
app.SetTargetFramerate(60);

app.OnKeyDown = key =>
{
    switch (key)
    {
        case KeyCode.Escape:
            app.Stop();
            break;
    }
};

app.OnUpdate = delta =>
{
    app.WriteDebug($"Framerate {1 / delta}");
};

app.Run();