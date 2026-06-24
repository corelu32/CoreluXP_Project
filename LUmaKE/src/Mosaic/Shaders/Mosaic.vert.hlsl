struct ControlPoint
{
    float3 position;
    float4 color;
};

struct ControlGrid
{
    ControlPoint Points[16];
};

struct MosaicShape
{
    float TessellationLevel;
};

struct VertexOutput
{
    float4 position : SV_Position;
    float4 color    : COLOR0;
};

/***************************************************************************************************
    Evaluates a cubic Bernstein polynomial given progress parameter (t) and 
    four 3D-vector control points (c0-c3). This function yields one 3D position vector.
***************************************************************************************************/
float3 EvaluateBernsteinPolynomial(float3 c0, float3 c1, float3 c2, float3 c3, float t)
{
    // Inverted t (one minus t)
    float tv = 1.0f - t;

    // Precalculate powers of t
    float tPow0 = 1.0f;
    float tPow1 = t;
    float tPow2 = t * t;
    float tPow3 = t * t * t;

    // Precalculate powers of inverted t
    float tvPow0 = 1.0f;
    float tvPow1 = tv;
    float tvPow2 = tv * tv;
    float tvPow3 = tv * tv * tv;

    // Calculate the pure scalar Bernstein basis polynomials
    float b0 = 1.0f * tPow0 * tvPow3;      //      (1 - t)^3
    float b1 = 3.0f * tPow1 * tvPow2;      // 3t   (1 - t)^2
    float b2 = 3.0f * tPow2 * tvPow1;      // 3t^2 (1 - t)
    float b3 = 1.0f * tPow3 * tvPow0;      //  t^3 

    // Return the final blended 3D position vector
    return (b0 * c0) + (b1 * c1) + (b2 * c2) + (b3 * c3);
}

/***************************************************************************************************
    Evaluates a 4x4 bicubic Bézier patch at parameters (u, v).
***************************************************************************************************/
float3 EvaluateBezierPatch(ControlGrid grid, float u, float v)
{
    float3 temp_points[4];

    // Step 1: Interpolate across the 4 rows using the U parameter
    // The 16-point flat array is mapped as: Row 0 (0-3), Row 1 (4-7), Row 2 (8-11), Row 3 (12-15)
    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        int row = i * 4;
        temp_points[i] = EvaluateBernsteinPolynomial(
            grid.Points[row + 0].position,
            grid.Points[row + 1].position,
            grid.Points[row + 2].position,
            grid.Points[row + 3].position,
            u
        );
    }

    // Step 2: Interpolate the 4 resulting row points along the V direction
    float3 final_point = EvaluateBernsteinPolynomial(
        temp_points[0],
        temp_points[1],
        temp_points[2],
        temp_points[3],
        v
    );

    return final_point;
}

VertexOutput Main(uint vertexId : SV_VertexID, uint instId : SV_InstanceID)
{
    VertexOutput output;
    
    

    return output;
}