using LUmaKE.Core;
using LUmaKE.Graphics.Gpu;
using SDL3;

namespace LUmaKE.Platforms;

/// <summary>
///   Work with an SDL backend. Allows for registration of multiple application windows.
/// </summary>
public partial class SdlPlatform : IPlatform
{
    private readonly Dictionary<Window, SdlWindowContext> _windowContexts = [];

    public void Initialize()
    {
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
            throw new Exception($"Failed to initialize SDL: {SDL.GetError()}");
    }

    /// <summary>
    ///   Register an existing window object with the SDL platform.
    ///   This will populate various event handlers on the given window.
    /// </summary>
    public void RegisterWindow(Window window)
    {
        if (IsWindowRegistered(window))
            throw new Exception($"The window '{window.Title}' is already registered in this SDL context!");

        Action              onOpen           = ( ) => CreateSdlWindow(window);
        Action<double>      onPlatformUpdate = (d) => UpdateSdlWindow(window, d);
        Action<GpuPipeline> onNewGpuPipeline = (p) => AddGpuPipeline(window, p);
            
        window.OnOpen           += onOpen;
        window.OnPlatformUpdate += onPlatformUpdate;
        window.OnNewGpuPipeline += onNewGpuPipeline;
        
        window.OnClose += () =>
        {
            // Remove all event handlers upon close.
            window.OnOpen           -= onOpen;
            window.OnPlatformUpdate -= onPlatformUpdate;
            window.OnNewGpuPipeline -= onNewGpuPipeline;
        };
    }

    /// <summary>
    ///   Unregister a window upon close.
    /// </summary>
    public void UnregisterWindow(Window window)
    {
        if (!IsWindowRegistered(window))
            throw new Exception($"The window '{window.Title}' does not exist!");

        DestroySdlWindow(window);
    }

    /// <summary>
    ///   Check if a window has been registered with the SDL platform.
    /// </summary>
    public bool IsWindowRegistered(Window window)
        => _windowContexts.ContainsKey(window);
}