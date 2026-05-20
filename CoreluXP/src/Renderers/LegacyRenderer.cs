using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace CoreluXP.Renderers;

public unsafe sealed class LegacyRenderer
{
    private readonly IWindow _window;
    
    private WebGPU?       _webGpu;
    private TextureFormat _swapChainFormat;
    
    private Instance* _instance;
    private Device*   _device;
    private Surface*  _surface;
    
    public bool IsReady { get; private set; }

    public LegacyRenderer(IWindow window)
    {
        _window = window;
    }

    public void Initialize()
    {
        _webGpu = WebGPU.GetApi();
        
        var instanceDesc = new InstanceDescriptor();
        _instance = _webGpu.CreateInstance(&instanceDesc);
        _surface  = _window.CreateWebGPUSurface(_webGpu, _instance);

        var adapterOptions = new RequestAdapterOptions
        {
            CompatibleSurface = _surface,
            PowerPreference   = PowerPreference.HighPerformance
        };
        
        _webGpu!.InstanceRequestAdapter(
            _instance,
            &adapterOptions,
            PfnRequestAdapterCallback.From(OnAdapterReceived),
            null);
    }

    private void OnAdapterReceived(RequestAdapterStatus status, Adapter* adapter, byte* message, void* userdata)
    {
        if (status is not RequestAdapterStatus.Success || adapter is null)
            throw new Exception("WebGPU failed to allocate a physical hardware adapter.");

        var deviceDescriptor = new DeviceDescriptor();
        
        _webGpu!.AdapterRequestDevice(
            adapter,
            &deviceDescriptor,
            PfnRequestDeviceCallback.From((devStatus, device, msg, data) => 
                OnDeviceReceived(devStatus, device, adapter)),
            null);
    }

    private void OnDeviceReceived(RequestDeviceStatus status, Device* device, Adapter* adapter)
    {
        if (status is not RequestDeviceStatus.Success || device is null)
            throw new Exception("WebGPU failed to initialize the virtual execution device context.");

        _device = device;
        
        var capabilities = new SurfaceCapabilities();
        _webGpu!.SurfaceGetCapabilities(_surface, adapter, &capabilities);

        _swapChainFormat = (capabilities.FormatCount > 0 && capabilities.Formats is not null)
            ? *capabilities.Formats
            : TextureFormat.Bgra8Unorm;
        
        var surfaceConfig = new SurfaceConfiguration
        {
            Device      = _device,
            Format      = _swapChainFormat,
            Usage       = TextureUsage.RenderAttachment,
            PresentMode = PresentMode.Fifo,
            Width       = (uint)_window.FramebufferSize.X,
            Height      = (uint)_window.FramebufferSize.Y,
            AlphaMode   = CompositeAlphaMode.Opaque
        };

        _webGpu.SurfaceConfigure(_surface, &surfaceConfig);
        _webGpu.SurfaceCapabilitiesFreeMembers(capabilities);

        IsReady = true;
    }
    
    public void Render(double deltaTime)
    {
        if (!IsReady) return;
    }
}

