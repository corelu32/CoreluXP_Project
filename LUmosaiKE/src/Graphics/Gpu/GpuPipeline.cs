namespace LUmosaiKE.Graphics.Gpu;

public enum PrimitiveType
{
    Points,
    Lines,
    LineStrip,
    Triangles,
    TriangleStrip
}

public sealed class GpuPipeline
{
    public ShaderProgram VertexShader   { get; }
    public ShaderProgram FragmentShader { get; }
    public PrimitiveType PrimitiveType  { get; }
    
    public GpuPipeline(
        ShaderProgram vertexShader,
        ShaderProgram fragmentShader,
        PrimitiveType primitiveType)
    {
        VertexShader   = vertexShader;
        FragmentShader = fragmentShader;
        PrimitiveType  = primitiveType;
    }
}