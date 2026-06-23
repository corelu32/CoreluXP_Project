using LUmaKE.Graphics.Gpu;
using LUmaKE.Mosaic;
using LUmaKE.Primitives;

namespace Demo;

public static class Hello
{
    public static void Run()
    {
        var mosaic = new MosaicBuilder()
        {
            Platform = Platform.SDL,
            Title    = "LUmaKE Mosaic Demo",
            Size     = new(800, 600)
            
        }.Build();

        mosaic.Run();
    }
}