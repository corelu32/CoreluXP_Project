using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace CoreluXP.Applications;

public sealed class DesktopApplication : IApplication
{
    public readonly struct InnerSilkContext(IWindow window)
    {
        private readonly IWindow _window = window;
        public IWindow Window => _window;
    }
    
    private readonly IWindow _window = null!;

    public event Action?         OnLoad;
    public event Action<double>? OnUpdate;
    public event Action?         OnClose;

    public InnerSilkContext SilkContext; 
    
    public DesktopApplication(string title, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Title     = title,
            Size      = new Vector2D<int>(width, height),
            API       = GraphicsAPI.None,
            IsVisible = false,
            Position  = WindowOptions.Default.Position
        };
        
        _window = Window.Create(options);
        _window.Load    += OnLoadHandler;
        _window.Render  += OnUpdateHandler;
        _window.Closing += OnCloseHandler;

        SilkContext = new(_window);
    }

    public void Run() => _window.Run();
    
    private void OnLoadHandler()
    {
        _window.Center();
        OnLoad?.Invoke();
        _window.IsVisible = true;
    }
    
    private void OnUpdateHandler(double dt)
        => OnUpdate?.Invoke(dt);
    
    private void OnCloseHandler()
    {
        OnClose?.Invoke();

        _window.Load    -= OnLoadHandler;
        _window.Render  -= OnUpdateHandler;
        _window.Closing -= OnCloseHandler;
    }
}