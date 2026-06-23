struct FragmentInput
{
    float4 position : SV_Position;
    float4 color    : COLOR0;
};

float4 main(FragmentInput input) : SV_Target0
{
    return input.color;
}