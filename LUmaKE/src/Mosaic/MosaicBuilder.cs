using LUmaKE.Mathematics;

namespace LUmaKE.Mosaic;

public enum Platform { SDL }

public sealed class MosaicBuilder
{
    public string?       Title    { get; set; }
    public Vector2<int>? Size     { get; set; }
    public Platform?     Platform { get; set; }
    
    public MosaicBuilder UsePlatform(Platform platform)
    {
        Platform = platform;
        return this;
    }

    public MosaicBuilder WithTitle(string title)
    {
        Title = title;
        return this;
    }

    public MosaicBuilder WithSize(Vector2<int> size)
    {
        Size = size;
        return this;
    }

    public IMosaic Build()
        => ResolvedPlatform switch
        {
             Mosaic.Platform => new SdlMosaic(ResolvedTitle, ResolvedSize.X, ResolvedSize.Y) 
        };
        
    private Mosaic.Platform ResolvedPlatform
        => Platform ?? throw new Exception("Platform was not configured.");

    private string ResolvedTitle
        => Title ?? "LUmaKE Mosaic";

    private Vector2<int> ResolvedSize
        => Size ?? new Vector2<int>(800, 600);
}