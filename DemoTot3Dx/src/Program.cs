using Tot3Dx.Applications;
using Tot3Dx.Primitives;

var options = new DesktopApplicationOptions
{
    Title = "Tot3Dx Demo",
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