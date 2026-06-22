
using LUmaKE.Graphics.Gpu;
using LUmaKE.Mathematics;
using LUmaKE.Primitives;

namespace LUmaKE.Applications;

public interface IApplication
{
    bool         Running         { get; }
    string       Title           { get; set; }
    Vector2<int> Position        { get; set; }
    Vector2<int> Size            { get; set; }
    double?      TargetFramerate { get; set; }
    bool         VSyncEnabled    { get; set; }
    
    void Close();
    void Run();

    event Action?          OnStart;
    event Action?          OnClose;
    event Action<Keycode>? OnKeyDown;
    event Action<double>?  OnUpdate;
    event Action<double>?  OnRender;
    
    void LoadPipeline     (GpuPipeline pipeline);
    void BindPipeline     (GpuPipeline pipeline);
    void UnloadPipeline   (GpuPipeline pipeline);
    bool IsPipelineLoaded (GpuPipeline pipeline);
    
    void LoadBuffer     (GpuBuffer buffer);
    void BindBuffer     (GpuBuffer buffer);
    void BindBuffers    (ICollection<GpuBuffer> buffers);
    void UnloadBuffer   (GpuBuffer buffer);
    bool IsBufferLoaded (GpuBuffer buffer);
}