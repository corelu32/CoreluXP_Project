namespace LUmaKE.Graphics.Gpu;

/// <summary>
///   The input classification of a vertex buffer description.
/// </summary>
public enum InputClassification
{
    /// <summary>
    ///   Describes data that updates per individual vertex.
    /// </summary>
    PerVertex,
    
    /// <summary>
    ///   Describes data that updates per instance or object copy.
    /// </summary>
    PerInstance
}

/// <summary>
///   Compatible data types for a vertex attribute.
/// </summary>
public enum AttributeType
{
    /// <summary>
    ///   An 8-bit unsigned integer.
    /// </summary>
    Byte,
    
    /// <summary>
    ///   A 32-bit floating-point number.
    /// </summary>
    Float,
    
    /// <summary>
    ///   A 32-bit signed integer.
    /// </summary>
    Int
}

/// <summary>
///   Maintains a list of vertex buffer descriptions and their associated attributes.
/// </summary>
public sealed class VertexLayout
{
    private readonly List<VertexBufferDescription> _descriptions = [];

    /// <summary>
    ///   Enumerate the descriptions.
    /// </summary>
    public IEnumerable<VertexBufferDescription> Descriptions => _descriptions;
    
    /// <summary>
    ///   Number of defined descriptions.
    /// </summary>
    public int DescriptionCount => _descriptions.Count;

    /// <summary>
    ///   Adds a new vertex buffer description to the layout.
    /// </summary>
    public VertexBufferDescription CreateDescription(InputClassification classification)
    {
        VertexBufferDescription description = new(classification, DescriptionCount);
        _descriptions.Add(description);
        return description;
    }
}

/// <summary>
///   Represents a vertex buffer description containing structural information and layout attributes.
/// </summary>
public sealed class VertexBufferDescription
{
    private List<VertexAttribute> _attributes = [];
    
    /// <summary>
    ///   Slot number for the description.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    ///   The total size of all attribute bytes in one vertex row.
    /// </summary>
    public int Stride { get; private set; } = 0;

    /// <summary>
    ///   The list of child vertex attributes associated with this description slot.
    /// </summary>
    public IEnumerable<VertexAttribute> Attributes => _attributes;

    /// <summary>
    ///   The input classification for the vertex buffer description.
    /// </summary>
    public InputClassification InputClassification { get; }

    internal VertexBufferDescription(InputClassification classification, int slot)
    {
        InputClassification = classification;
        Slot = slot;
    }

    /// <summary>
    ///   Adds an attribute to this vertex buffer description and updates the layout stride.
    /// </summary>
    public void CreateAttribute(string semantic, AttributeType type, int dimensions)
    {
        VertexAttribute attribute = new(
            description: this,
            semantic:    semantic,
            offset:      Stride,
            type:        type,
            dimensions:  dimensions);

        _attributes.Add(attribute);
        Stride += GetTypeSize(type) * dimensions;
    }

    private static int GetTypeSize(AttributeType type) => type switch
    {
        AttributeType.Byte  => 1,
        AttributeType.Float => 4,
        AttributeType.Int   => 4,
        _ => 0
    };
}

/// <summary>
///   Represents a single vertex component variable mapping inside a buffer layout slot.
/// </summary>
public sealed class VertexAttribute
{
    /// <summary>
    ///   The associated description for this vertex attribute.
    /// </summary>
    public VertexBufferDescription ParentDescription { get; }

    /// <summary>
    ///   Retrieve the parent description's slot index.
    /// </summary>
    public int SlotIndex => ParentDescription.Slot;
    
    /// <summary>
    ///   The string semantic label identifying the purpose of the attribute.
    /// </summary>
    public string Semantic { get; }

    /// <summary>
    ///   The underlying primitive data type of the attribute.
    /// </summary>
    public AttributeType Type { get; }

    /// <summary>
    ///   The number of component elements in the attribute variable (e.g., 3 for float3).
    /// </summary>
    public int Dimensions { get; }

    /// <summary>
    ///   The specific byte start offset inside a single vertex layout structure.
    /// </summary>
    public int Offset { get; }

    internal VertexAttribute(
        VertexBufferDescription description,
        string        semantic,
        int           offset,
        AttributeType type,
        int           dimensions)
    {
        ParentDescription = description;
        Semantic   = semantic;
        Type       = type;
        Dimensions = dimensions;
        Offset     = offset;
    }
}