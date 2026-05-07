using CoreluXP.Applications;
using CoreluXP.Primitives;

var options = new DesktopApplicationOptions
{
    Title = "Corelu XP Demo",
    Width = 800,
    Height = 600
};

var app = new DesktopApplication(options);

app.OnKeyDown = key =>
{
    switch (key)
    {
        case KeyCode.Escape:
            app.Stop();
            break;
    }
};

app.Run();