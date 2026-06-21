using LUmaKE.Primitives;

namespace LUmaKE.Graphics.Gpu;

[Flags]
public enum BufferUsage
{
    /// <summary>
    ///   The buffer can be bound to a vertex input slot to feed vertex attributes.
    /// </summary>
    Vertex = 1 << 0,

    /// <summary>
    ///   The buffer contains vertex index rendering arrays (uint16 or uint32 indices).
    /// </summary>
    Index = 1 << 1,

    /// <summary>
    ///   Standard uniform variables (Constant buffer data blocks).
    /// </summary>
    Uniform = 1 << 2,

    /// <summary>
    ///   Large read-only arrays accessed by index in graphics/vertex stages.
    /// </summary>
    StorageRead = 1 << 3,

    /// <summary>
    ///   Read-Write layout arrays utilized by Compute shaders or pipelines.
    /// </summary>
    StorageReadWrite = 1 << 4,

    /// <summary>
    ///   Contains draw or dispatch execution argument parameters generated on the GPU.
    /// </summary>
    Indirect = 1 << 5
}

public sealed class GpuBuffer
{
    public BufferUsage  Usage  { get; }
    public BinaryBuffer Buffer { get; }

    public GpuBuffer(BufferUsage usage, BinaryBuffer buffer)
    {
        Usage  = usage;
        Buffer = buffer;
    }
}