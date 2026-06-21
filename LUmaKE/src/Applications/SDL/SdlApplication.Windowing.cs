using LUmaKE.Core;
using SDL3;

namespace LUmaKE.Applications;

public partial class SdlApplication : IApplication
{
    /// <summary>
    ///   Utility for resolving an SDL window context.
    /// </summary>
    private SdlWindowContext GetWindowContext(Window window)
        => ContainsWindow(window)
            ? _windowContexts[window]
            : throw new Exception($"The window '{window.Title}' is not registered in this SDL context.");

    /// <summary>
    ///   Destroy a window that was registered on the SDL platform.
    /// </summary>
    private void UpdateSdlWindow(Window window, double delta)
        => UpdateSdlWindow(GetWindowContext(window), delta);
    
    /// <summary>
    ///   Destroy a window that was registered on the SDL platform.
    /// </summary>
    private void DestroySdlWindow(Window window)
        => DestroySdlWindow(GetWindowContext(window));
            
    /// <summary>
    ///   Create a new SDL window and associate with a core window object.
    /// </summary>
    private void CreateSdlWindow(Window window)
    {
        IntPtr windowHandle = SDL.CreateWindow(
            window.Title,
            window.Size.X,
            window.Size.Y,
            SDL.WindowFlags.Hidden);
        
        if (windowHandle == IntPtr.Zero)
            throw new Exception($"Failed to initialize the window and renderer: {SDL.GetError()}");

        IntPtr gpuDeviceHandle = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.MSL | SDL.GPUShaderFormat.DXIL,
            debugMode : false,
            name      : null);

        if (gpuDeviceHandle == IntPtr.Zero)
            throw new Exception($"Failed to create the GPU device: {SDL.GetError()}");

        if (!SDL.ClaimWindowForGPUDevice(gpuDeviceHandle, windowHandle))
            throw new Exception($"Failed to bind GPU device to window: {SDL.GetError()}");

        _windowContexts[window] = new(window, windowHandle, gpuDeviceHandle);
        SDL.ShowWindow(windowHandle);
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
        IntPtr commandBuffer = SDL.AcquireGPUCommandBuffer(context.GpuDeviceHandle);
        if (commandBuffer == IntPtr.Zero)
            return;

        // Acquire the swapchain texture for the current frame.
        if (SDL.AcquireGPUSwapchainTexture(
            commandBuffer,
            context.WindowHandle,
            out IntPtr swapchainTexture,
            out uint width,
            out uint height))
        {
            if (swapchainTexture != IntPtr.Zero)
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
    
    /// <summary>
    ///   Destroy a window that was registered on the SDL platform.
    /// </summary>
    private void DestroySdlWindow(SdlWindowContext context)
    {
        SDL.ReleaseWindowFromGPUDevice(context.GpuDeviceHandle, context.WindowHandle);

        if (context.GpuDeviceHandle != IntPtr.Zero)
            SDL.DestroyGPUDevice(context.GpuDeviceHandle);

        if (context.WindowHandle != IntPtr.Zero)
            SDL.DestroyWindow(context.WindowHandle);

        _windowContexts.Remove(context.Window);
    }
}

internal sealed class SdlWindowContext
{
    public Window Window          { get; }
    public IntPtr WindowHandle    { get; }
    public IntPtr GpuDeviceHandle { get; }

    internal SdlWindowContext(
        Window window,
        IntPtr windowHandle,
        IntPtr gpuDeviceHandle)
    {
        Window          = window;
        WindowHandle    = windowHandle;
        GpuDeviceHandle = gpuDeviceHandle;
    }
}