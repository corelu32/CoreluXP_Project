using Silk.NET.Maths;

namespace Prototype.V2.Graphics;

/// <summary>
/// Defines a procedurally generated mesh as a swept, segmented cylinder.
/// No vertex buffer is used — all geometry is derived in the WGSL shader
/// from the slice/edge tables uploaded as uniform data.
/// </summary>
public sealed class LegacyShape
{
    // ── Procedural definition ────────────────────────────────────────────────

    /// <summary>Number of edges (sides) on each circular cross-section.</summary>
    public int Edges { get; }

    /// <summary>Number of equally-spaced height segments.</summary>
    public int Slices { get; }

    /// <summary>
    /// Returns the 3-D centre of a given slice in local space.
    /// Index 0 = bottom, Index <see cref="Slices"/> = top.
    /// Default: straight vertical cylinder, slices spread over Y ∈ [−0.5, 0.5].
    /// </summary>
    public Func<int, Vector3D<double>> PathFunction { get; }

    /// <summary>
    /// Returns the radial distance from the slice centre to a specific edge
    /// vertex on that slice.  Accepts (edgeIndex, sliceIndex).
    /// Default: uniform radius of 1.
    /// </summary>
    public Func<int, int, double> ProfileRadius { get; }

    // ── Per-instance transform ───────────────────────────────────────────────

    private Vector3D<float>    _position = Vector3D<float>.Zero;
    private Quaternion<float>  _rotation = Quaternion<float>.Identity;
    private Vector3D<float>    _scale    = new(1f, 1f, 1f);

    // ── Visual properties ────────────────────────────────────────────────────

    private Vector4D<float> _color     = new(1f, 1f, 1f, 1f);
    private Vector2D<float> _uvScale   = new(1f, 1f);
    private Vector2D<float> _uvOffset  = new(0f, 0f);

    // ── Lighting ─────────────────────────────────────────────────────────────

    private float _shininess  = 32f;
    private float _specular   = 0.5f;
    private float _ambient    = 0.1f;

    // ── Dirty tracking ───────────────────────────────────────────────────────

    internal bool GeometryDirty  { get; private set; } = true;
    internal bool TransformDirty { get; private set; } = true;

    // ─────────────────────────────────────────────────────────────────────────
    // Construction
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a LegacyShape with fully custom path and profile functions.
    /// </summary>
    /// <param name="edges">Number of sides per cross-section (minimum 3).</param>
    /// <param name="slices">Number of height segments (minimum 1).</param>
    /// <param name="pathFunction">
    ///     Maps slice index → world-space centre offset.
    ///     Slice 0 is the bottom ring; slice <paramref name="slices"/> is the cap.
    /// </param>
    /// <param name="profileRadius">
    ///     Maps (edgeIndex, sliceIndex) → radial distance from the slice centre.
    /// </param>
    public LegacyShape(
        int edges,
        int slices,
        Func<int, Vector3D<double>>  pathFunction,
        Func<int, int, double>       profileRadius)
    {
        if (edges  < 3) throw new ArgumentOutOfRangeException(nameof(edges),  "A shape needs at least 3 edges.");
        if (slices < 1) throw new ArgumentOutOfRangeException(nameof(slices), "A shape needs at least 1 slice.");

        Edges         = edges;
        Slices        = slices;
        PathFunction  = pathFunction;
        ProfileRadius = profileRadius;
    }

    /// <summary>
    /// Convenience constructor: straight vertical cylinder, uniform radius.
    /// </summary>
    public LegacyShape(int edges = 16, int slices = 1, double radius = 1.0)
        : this(
            edges,
            slices,
            sliceIndex =>
            {
                double t = slices == 1
                    ? 0.0
                    : sliceIndex / (double)slices;
                return new Vector3D<double>(0, t - 0.5, 0);
            },
            (_, _) => radius)
    { }

    // ─────────────────────────────────────────────────────────────────────────
    // Transform fluent API
    // ─────────────────────────────────────────────────────────────────────────

    public LegacyShape SetPosition(Vector3D<float> position)
    {
        _position      = position;
        TransformDirty = true;
        return this;
    }

    public LegacyShape SetPosition(float x, float y, float z)
        => SetPosition(new Vector3D<float>(x, y, z));

    public LegacyShape SetRotation(Quaternion<float> rotation)
    {
        _rotation      = rotation;
        TransformDirty = true;
        return this;
    }

    /// <summary>Rotation from Euler angles in radians (pitch, yaw, roll).</summary>
    public LegacyShape SetRotationEuler(float pitch, float yaw, float roll)
    {
        _rotation = Quaternion<float>.CreateFromYawPitchRoll(yaw, pitch, roll);
        TransformDirty = true;
        return this;
    }

    public LegacyShape SetScale(Vector3D<float> scale)
    {
        _scale         = scale;
        TransformDirty = true;
        return this;
    }

    public LegacyShape SetScale(float uniform)
        => SetScale(new Vector3D<float>(uniform, uniform, uniform));

    public LegacyShape SetScale(float x, float y, float z)
        => SetScale(new Vector3D<float>(x, y, z));

    // ─────────────────────────────────────────────────────────────────────────
    // Visual / material fluent API
    // ─────────────────────────────────────────────────────────────────────────

    public LegacyShape SetColor(Vector4D<float> color)          { _color = color; TransformDirty = true; return this; }
    public LegacyShape SetColor(float r, float g, float b, float a = 1f) => SetColor(new Vector4D<float>(r, g, b, a));

    public LegacyShape SetUVScale(Vector2D<float> scale)        { _uvScale = scale; TransformDirty = true; return this; }
    public LegacyShape SetUVOffset(Vector2D<float> offset)      { _uvOffset = offset; TransformDirty = true; return this; }

    public LegacyShape SetShininess(float shininess)            { _shininess = shininess; TransformDirty = true; return this; }
    public LegacyShape SetSpecular(float specular)              { _specular  = specular;  TransformDirty = true; return this; }
    public LegacyShape SetAmbient(float ambient)                { _ambient   = ambient;   TransformDirty = true; return this; }

    // ─────────────────────────────────────────────────────────────────────────
    // Read-only accessors (used by the renderer)
    // ─────────────────────────────────────────────────────────────────────────

    public Vector3D<float>   Position  => _position;
    public Quaternion<float> Rotation  => _rotation;
    public Vector3D<float>   Scale     => _scale;
    public Vector4D<float>   Color     => _color;
    public Vector2D<float>   UVScale   => _uvScale;
    public Vector2D<float>   UVOffset  => _uvOffset;
    public float             Shininess => _shininess;
    public float             Specular  => _specular;
    public float             Ambient   => _ambient;

    // ─────────────────────────────────────────────────────────────────────────
    // GPU data baking
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bakes the procedural path and profile into a flat float array for upload
    /// to the per-shape geometry storage buffer.
    ///
    /// Layout per slice (in floats):
    ///   [0..2]  centre.xyz  (padded to vec4 → 4 floats)
    ///   [4..4+Edges-1]  radius per edge
    ///   padded to 4-float alignment
    ///
    /// Total floats = (Slices+1) × stride,
    /// where stride = 4 + ceil(Edges / 4) × 4.
    /// </summary>
    internal float[] BakeGeometryData()
    {
        int edgeStride   = ((Edges + 3) / 4) * 4;   // pad to vec4 boundary
        int floatsPerSlice = 4 + edgeStride;
        int totalSlices  = Slices + 1;               // bottom ring … top cap

        var data = new float[totalSlices * floatsPerSlice];

        for (int s = 0; s <= Slices; s++)
        {
            var centre = PathFunction(s);
            int baseIdx = s * floatsPerSlice;

            data[baseIdx + 0] = (float)centre.X;
            data[baseIdx + 1] = (float)centre.Y;
            data[baseIdx + 2] = (float)centre.Z;
            data[baseIdx + 3] = 0f;  // padding

            for (int e = 0; e < Edges; e++)
                data[baseIdx + 4 + e] = (float)ProfileRadius(e, s);
        }

        GeometryDirty = false;
        return data;
    }

    /// <summary>
    /// Bakes transform + material into a flat float array for the per-instance
    /// uniform buffer.  Layout (std140-friendly, 64-byte block):
    ///   model matrix  (16 floats)
    ///   color         (4 floats)
    ///   uvScale       (2 floats)
    ///   uvOffset      (2 floats)
    ///   shininess     (1 float)
    ///   specular      (1 float)
    ///   ambient       (1 float)
    ///   _pad          (1 float)
    /// </summary>
    internal float[] BakeInstanceData()
    {
        var model = BuildModelMatrix();

        return
        [
            // Row-major model matrix
            model.M11, model.M12, model.M13, model.M14,
            model.M21, model.M22, model.M23, model.M24,
            model.M31, model.M32, model.M33, model.M34,
            model.M41, model.M42, model.M43, model.M44,
            // Color
            _color.X, _color.Y, _color.Z, _color.W,
            // UV
            _uvScale.X, _uvScale.Y,
            _uvOffset.X, _uvOffset.Y,
            // Material
            _shininess, _specular, _ambient,
            0f  // pad
        ];
    }

    internal void ClearTransformDirty() => TransformDirty = false;

    private Matrix4X4<float> BuildModelMatrix()
    {
        var t = Matrix4X4.CreateTranslation(_position);
        var r = Matrix4X4.CreateFromQuaternion(_rotation);
        var s = Matrix4X4.CreateScale(_scale);
        return s * r * t;
    }
}