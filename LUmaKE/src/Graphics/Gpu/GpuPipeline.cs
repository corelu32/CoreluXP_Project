namespace LUmaKE.Graphics.Gpu;

/// <summary>
///   Supported texture and framebuffer formats across backends.
/// </summary>
public enum TextureFormat
{
    Rgba8Unorm,
    Bgra8Unorm,
    Rgba16Float,
    Depth24Stencil8,
    Depth32Float
}

public enum PrimitiveTopology
{
    Points,
    Lines,
    LineStrip,
    Triangles,
    TriangleStrip
}

/// <summary>
///   Describes how color or depth data is initialized when a pass begins.
/// </summary>
public enum LoadOperation
{
    /// <summary>
    ///   Clears the attachment to a uniform default color or depth value.
    /// </summary>
    Clear,
    
    /// <summary>
    ///   Preserves existing data already stored in the texture.
    /// </summary>
    Load,
    
    /// <summary>
    ///   Discards previous content for maximum performance (write-only).
    /// </summary>
    Ignore
}

/// <summary>
///   Structural layout properties for a single color attachment slot.
/// </summary>
public sealed class ColorTargetDescription
{
    public TextureFormat Format        { get; }
    public LoadOperation LoadOperation { get; }

    public ColorTargetDescription(
        TextureFormat format,
        LoadOperation loadOp = LoadOperation.Clear)
    {
        Format        = format;
        LoadOperation = loadOp;
    }
}

/// <summary>
///   Structural layout properties for the depth-stencil attachment slot.
/// </summary>
public sealed class DepthTargetDescription
{
    public TextureFormat Format            { get; }
    public LoadOperation LoadOperation     { get; }
    public bool          DepthWriteEnabled { get; }

    public DepthTargetDescription(TextureFormat format, LoadOperation loadOp = LoadOperation.Clear, bool depthWriteEnabled = true)
    {
        Format = format;
        LoadOperation = loadOp;
        DepthWriteEnabled = depthWriteEnabled;
    }
}

public sealed class GpuPipeline
{
    private readonly List<ColorTargetDescription> _colorTargets = [];

    public GpuShader     VertexShader   { get; }
    public GpuShader     FragmentShader { get; }
    public PrimitiveTopology Topology       { get; }
    public VertexLayout      Layout         { get; }
    
    /// <summary>
    ///   The collection of active color buffer destination formats.
    /// </summary>
    public IEnumerable<ColorTargetDescription> ColorTargets => _colorTargets;
    
    /// <summary>
    ///   The optional depth-stencil buffer configuration blueprint.
    /// </summary>
    public DepthTargetDescription? DepthTarget { get; }

    public GpuPipeline(
        GpuShader     vertexShader,
        GpuShader     fragmentShader,
        PrimitiveTopology topology,
        VertexLayout      layout,
        IEnumerable<ColorTargetDescription> colorTargets,
        DepthTargetDescription? depthTarget = null)
    {
        VertexShader   = vertexShader;
        FragmentShader = fragmentShader;
        Topology       = topology;
        Layout         = layout;
        DepthTarget    = depthTarget;
        
        _colorTargets.AddRange(colorTargets);
    }
}