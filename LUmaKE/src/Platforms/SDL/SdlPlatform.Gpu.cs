using LUmaKE.Core;
using LUmaKE.Graphics.Gpu;

namespace LUmaKE.Platforms;

public partial class SdlPlatform : IPlatform
{
    private void AddGpuPipeline(SdlWindowContext context, GpuPipeline pipeline)
    {
        
    }

    private void AddGpuPipeline(Window window, GpuPipeline pipeline)
        => AddGpuPipeline(GetWindowContext(window), pipeline);
}