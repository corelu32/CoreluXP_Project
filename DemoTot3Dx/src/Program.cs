using Tot3Dx.Applications;
using Tot3Dx.Primitives;

var options = new DesktopApplicationOptions
{
    Title = "Tot3Dx Demo",
    Width = 800,
    Height = 600
};

var window = new DesktopApplication(options);

window.OnKeyDown = key =>
{
    switch (key)
    {
        case KeyCode.Escape:
            window.Stop();
            break;
    }
};

window.Run();