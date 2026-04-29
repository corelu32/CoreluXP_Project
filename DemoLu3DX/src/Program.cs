using Lu3DX;
using Lu3DX.Primitives;

var options = new ApplicationWindowOptions
{
    Title  = "Lu3DX Demo",
    Width  = 800,
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