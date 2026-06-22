namespace LUmaKE.Graphics.Gpu;

public enum ShaderStage { Vertex, Fragment, Compute }

public enum ShaderFormat { Glsl, Hlsl, Wgsl, GlslEs }

public sealed class GpuShader
{
    public string       Code       { get; }
    public string       EntryPoint { get; }
    public ShaderStage  Stage      { get; }
    public ShaderFormat Format     { get; }
    
    public GpuShader(
        string       code,
        string       entryPoint,
        ShaderStage  stage,
        ShaderFormat format)
    {
        Code       = code;
        EntryPoint = entryPoint;
        Stage      = stage;
        Format     = format;
    }
}