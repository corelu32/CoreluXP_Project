using Tot3Dx;
using Tot3Dx.Primitives;

var options = new ApplicationWindowOptions
{
    Title = "Tot3Dx Demo",
    Width = 800,
    Height = 600
};

var window = new ApplicationWindow(options);

window.OnKeyDown = (key) =>
{
    switch (key)
    {
        case KeyCode.Escape:
            window.Stop();
            break;
    }
};

window.Run();