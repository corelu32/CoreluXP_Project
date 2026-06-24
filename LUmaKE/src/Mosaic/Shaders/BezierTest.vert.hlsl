
/***************************************************************************************************
    MODULE 1: DATA TYPES & GEOMETRY STRUCTURES
***************************************************************************************************/
struct ControlPoint
{
    float3 position;
    float4 color;
};

struct ControlGrid
{
    ControlPoint Points[16];
};

struct VertexOutput
{
    float4 position : SV_Position;
    float4 color    : COLOR0;
};


/***************************************************************************************************
    MODULE 2: MATHEMATICAL EVALUATORS (Bernstein & Bezier Logic)
***************************************************************************************************/
struct BezierEvaluator
{
    // Evaluates a 1D cubic Bernstein polynomial curve pass
    float3 EvaluateCurve(float3 c0, float3 c1, float3 c2, float3 c3, float t)
    {
        float tv = 1.0f - t;

        float tPow0 = 1.0f;
        float tPow1 = t;
        float tPow2 = t * t;
        float tPow3 = t * t * t;

        float tvPow0 = 1.0f;
        float tvPow1 = tv;
        float tvPow2 = tv * tv;
        float tvPow3 = tv * tv * tv;

        float b0 = 1.0f * tPow0 * tvPow3;
        float b1 = 3.0f * tPow1 * tvPow2;
        float b2 = 3.0f * tPow2 * tvPow1;
        float b3 = 1.0f * tPow3 * tvPow0;

        return (b0 * c0) + (b1 * c1) + (b2 * c2) + (b3 * c3);
    }

    // Evaluates the full 2D surface patch 
    float3 EvaluatePatch(ControlGrid grid, float u, float v)
    {
        float3 temp_points[4];

        [unroll]
        for (int i = 0; i < 4; ++i)
        {
            int row = i * 4;
            temp_points[i] = EvaluateCurve(
                grid.Points[row + 0].position,
                grid.Points[row + 1].position,
                grid.Points[row + 2].position,
                grid.Points[row + 3].position,
                u
            );
        }

        return EvaluateCurve(temp_points[0], temp_points[1], temp_points[2], temp_points[3], v);
    }
};


/***************************************************************************************************
    MODULE 3: PROJECTION & VIEWPORT TRANSFORMER
***************************************************************************************************/
struct ViewTransformer
{
    float ScaleFactor;
    float PitchAngle;
    float YawAngle;

    // Factory simulator to quickly set up transformation properties
    static ViewTransformer CreateDefault()
    {
        ViewTransformer vt;
        vt.ScaleFactor = 0.6f;
        vt.PitchAngle  = 0.7f;
        vt.YawAngle    = 0.6f;
        return vt;
    }

    // Applies a 3D Pitch/Yaw rotation and scales to clip space
    float3 ProjectToScreen(float3 position)
    {
        // Rotate around X-axis (Pitch)
        float cx = cos(PitchAngle), sx = sin(PitchAngle);
        float3 rX = float3(position.x, position.y * cx - position.z * sx, position.y * sx + position.z * cx);
        
        // Rotate around Y-axis (Yaw)
        float cy = cos(YawAngle), sy = sin(YawAngle);
        float3 rY = float3(rX.x * cy + rX.z * sy, rX.y, -rX.x * sy + rX.z * cy);
        
        return rY * ScaleFactor;
    }
};


/***************************************************************************************************
    MODULE 4: TOPOLOGY BUILDER (Vertex Stream ID Unpacker)
***************************************************************************************************/
struct MeshTopology
{
    uint Segments;
    
    // Decodes a streaming vertex ID into normalized (U, V) coordinates
    float2 UnpackVertexIDToUV(uint vertexId)
    {
        uint quadId = vertexId / 6; 
        uint vertexInQuad = vertexId % 6; 
        
        uint quadX = quadId % Segments;
        uint quadY = quadId / Segments;
        
        uint xOffset = 0;
        uint yOffset = 0;
        
        if (vertexInQuad == 1 || vertexInQuad == 4 || vertexInQuad == 5) { yOffset = 1; }
        if (vertexInQuad == 2 || vertexInQuad == 3 || vertexInQuad == 5) { xOffset = 1; }
        
        float u = (float)(quadX + xOffset) / (float)Segments;
        float v = (float)(quadY + yOffset) / (float)Segments;
        return float2(u, v);
    }
};


/***************************************************************************************************
    MODULE 5: PROCEDURAL ASSET GENERATOR (Grid Point Generators)
***************************************************************************************************/
struct SceneGenerator
{
    // Generates a wave patch with customizable X boundaries and wave dynamics
    ControlGrid BuildWaveGrid(float xMin, float xMax, float amp, float freqX, float freqY, float4 baseColor)
    {
        ControlGrid grid;
        
        [unroll]
        for (int r = 0; r < 4; ++r)
        {
            float yPos = -1.0f + ((float)r / 3.0f) * 2.0f;
            
            [unroll]
            for (int c = 0; c < 4; ++c)
            {
                int idx = r * 4 + c;
                float xPos = xMin + ((float)c / 3.0f) * (xMax - xMin); 
                
                float z = sin(xPos * freqX) * cos(yPos * freqY) * amp;
                
                grid.Points[idx].position = float3(xPos, yPos, z);
                grid.Points[idx].color = baseColor;
            }
        }
        return grid;
    }
};


/***************************************************************************************************
    MAIN ENTRY POINT
***************************************************************************************************/
VertexOutput Main(uint vertexId : SV_VertexID, uint instId : SV_InstanceID)
{
    MeshTopology topology;
    topology.Segments = 32;
    float2 uv = topology.UnpackVertexIDToUV(vertexId);
    
    SceneGenerator generator;
    ControlGrid leftPatch  = generator.BuildWaveGrid(-1.0f,  0.0f, 0.40f, 5.5f, 4.0f, float4(0.1f, 0.4f, 0.8f, 1.0f));
    ControlGrid rightPatch = generator.BuildWaveGrid( 0.0f,  1.0f, 0.40f, 5.5f, 4.0f, float4(0.1f, 0.7f, 0.5f, 1.0f));
    
    BezierEvaluator evaluator;
    float3 worldPos = (instId == 0) ? evaluator.EvaluatePatch(leftPatch, uv.x, uv.y) 
                                    : evaluator.EvaluatePatch(rightPatch, uv.x, uv.y);
    
    float4 patchColor = (instId == 0) ? leftPatch.Points[0].color : rightPatch.Points[0].color;
    
    ViewTransformer transformer = ViewTransformer::CreateDefault();
    float3 finalScreenPos = transformer.ProjectToScreen(worldPos);
    
    VertexOutput output;
    output.position = float4(finalScreenPos, 1.0f);
    
    float heightTint = (worldPos.z + 0.40f) / 0.80f;
    output.color = lerp(patchColor * 0.4f, patchColor * 1.3f, heightTint);

    return output;
}