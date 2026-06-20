namespace LUmosaiKE.Mathematics;

public static class UnitConversions
{
    public const float NsPerSecond = 1_000_000_000.0f;
    
    public static float SecondsToNs(float seconds)
        => seconds / NsPerSecond;
}