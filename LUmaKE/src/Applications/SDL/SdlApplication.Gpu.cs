using LUmaKE.Core;
using LUmaKE.Graphics.Gpu;

namespace LUmaKE.Applications;

public partial class SdlApplication : IApplication
{
    private void AddGpuPipeline(SdlWindowContext context, GpuPipeline pipeline)
    {
        
    }

    private void AddGpuPipeline(Window window, GpuPipeline pipeline)
        => AddGpuPipeline(GetWindowContext(window), pipeline);
}