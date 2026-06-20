using LUmosaiKE.Core;
using LUmosaiKE.Primitives;
using SDL3;

namespace LUmosaiKE.Platforms;

/// <summary>
///   IntPtr alias related to SDL handles.
/// </summary>
using SdlHandle = IntPtr;

/// <summary>
///   Work with an SDL backend. Allows for registration of multiple application windows.
/// </summary>
public sealed class SdlPlatform : IPlatform
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

        Action         onOpen           = ( ) => CreateSdlWindow(window);
        Action<double> onPlatformUpdate = (d) => UpdateSdlWindow(window, d);
            
        window.OnOpen           += onOpen;
        window.OnPlatformUpdate += onPlatformUpdate;
        
        window.OnClose += () =>
        {
            // Remove all event handlers and destroy the SDL window.
            window.OnOpen           -= onOpen;
            window.OnPlatformUpdate -= onPlatformUpdate;
            
            DestroySdlWindow(window);
        };
    }

    /// <summary>
    ///   Check if a window has been registered with the SDL platform.
    /// </summary>
    public bool IsWindowRegistered(Window window)
        => _windowContexts.ContainsKey(window);
        
    /// <summary>
    ///   Utility for resolving an SDL window context.
    /// </summary>
    private SdlWindowContext GetWindowContext(Window window)
        => IsWindowRegistered(window)
            ? _windowContexts[window]
            : throw new Exception($"The window '{window.Title}' is not registered in this SDL context.");

    /// <summary>
    ///   Create a new SDL window and associate with a core window object.
    /// </summary>
    private void CreateSdlWindow(Window window)
    {
        SdlHandle windowHandle = SDL.CreateWindow(
            window.Title,
            window.Size.X,
            window.Size.Y,
            SDL.WindowFlags.Hidden);
        
        if (windowHandle == SdlHandle.Zero)
            throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        SdlHandle gpuDeviceHandle = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.MSL | SDL.GPUShaderFormat.DXIL,
            debugMode : false,
            name      : null);

        if (gpuDeviceHandle == SdlHandle.Zero)
            throw new Exception($"Failed to create the GPU device: {SDL.GetError()}");

        if (!SDL.ClaimWindowForGPUDevice(gpuDeviceHandle, windowHandle))
            throw new Exception($"Failed to bind GPU device to window: {SDL.GetError()}");

        _windowContexts[window] = new(window, windowHandle, gpuDeviceHandle);
    }

    /// <summary>
    ///   Destroy a window that was registered on the SDL platform.
    /// </summary>
    private void UpdateSdlWindow(SdlWindowContext context, double delta)
    {
        PollEvents(context);

        // Signal window update.
        context.Window.SignalUpdate(delta);

        // Acquire a GPU command buffer.
        SdlHandle commandBuffer = SDL.AcquireGPUCommandBuffer(context.GpuDeviceHandle);
        if (commandBuffer == SdlHandle.Zero)
            return;

        // Acquire the swapchain texture for the current frame.
        if (SDL.AcquireGPUSwapchainTexture(
            commandBuffer,
            context.WindowHandle,
            out SdlHandle swapchainTexture,
            out uint width,
            out uint height))
        {
            if (swapchainTexture != SdlHandle.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                    LoadOp     = SDL.GPULoadOp.Clear,
                    StoreOp    = SDL.GPUStoreOp.Store,
                    Texture    = swapchainTexture
                };

                IntPtr renderPass;

                unsafe
                {
                    renderPass = SDL.BeginGPURenderPass(commandBuffer, (IntPtr)(&colorTargetInfo), 1, IntPtr.Zero);
                }

                // Signal the window's render event.
                context.Window.SignalRender(delta);

                // End the GPU render pass.
                SDL.EndGPURenderPass(renderPass);
            }
        }

        SDL.SubmitGPUCommandBuffer(commandBuffer);
    }

    private void UpdateSdlWindow(Window window, double delta)
        => UpdateSdlWindow(GetWindowContext(window), delta);
    
    /// <summary>
    ///   Destroy a window that was registered on the SDL platform.
    /// </summary>
    private void DestroySdlWindow(SdlWindowContext context)
    {
        SDL.ReleaseWindowFromGPUDevice(context.GpuDeviceHandle, context.WindowHandle);

        if (context.GpuDeviceHandle != SdlHandle.Zero)
            SDL.DestroyGPUDevice(context.GpuDeviceHandle);

        if (context.WindowHandle != SdlHandle.Zero)
            SDL.DestroyWindow(context.WindowHandle);

        _windowContexts.Remove(context.Window);
    }

    private void DestroySdlWindow(Window window)
        => DestroySdlWindow(GetWindowContext(window));

    /// <summary>
    ///   Poll SDL events.
    /// </summary>
    private void PollEvents(SdlWindowContext context)
    {
        while (SDL.PollEvent(out var @event))
        {
            switch ((SDL.EventType) @event.Type)
            {
                // ON QUIT
                case SDL.EventType.Quit:
                    context.Window.SignalClose();
                    break;

                // ON KEY DOWN
                case SDL.EventType.KeyDown:
                    context.Window.SignalKeyDown((Keycode)@event.Key.Key);
                    break;
            }
        }
    }
}

internal sealed class SdlWindowContext
{
    public Window    Window          { get; }
    public SdlHandle WindowHandle    { get; }
    public SdlHandle GpuDeviceHandle { get; }

    internal SdlWindowContext(
        Window    window,
        SdlHandle windowHandle,
        SdlHandle gpuDeviceHandle)
    {
        Window          = window;
        WindowHandle    = windowHandle;
        GpuDeviceHandle = gpuDeviceHandle;
    }
}