namespace CoreluXP.Primitives;

/// <summary>
///   Represents a subsystem.
/// </summary>

public enum Subsystem : uint
{
    Audio    = 0x10u,
    Video    = 0x20u,
    Joystick = 0x200u,
    Haptic   = 0x1000u,
    Gamepad  = 0x2000u,
    Events   = 0x4000u,
    Sensor   = 0x8000u,
    Camera   = 0x10000u
}

/// <summary>
///   Represents a profile of subsystems.
/// </summary>

public readonly struct SubsystemProfile
{
    public readonly Subsystem _flags;

    public uint AsUint32  () => (uint)_flags;
    public bool IsEnabled (Subsystem subsystem) => _flags.HasFlag(subsystem);
    
    public SubsystemProfile(params Subsystem[] subsystems)
    {
        foreach (var system in subsystems)
            _flags |= system;
    }
}