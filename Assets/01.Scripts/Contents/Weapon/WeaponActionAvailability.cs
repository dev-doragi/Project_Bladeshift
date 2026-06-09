public struct WeaponActionAvailability
{
    public bool IsAvailable;
    public string BlockReason;
    public WeaponMode Mode;
    public WeaponState State;

    public static WeaponActionAvailability Available(WeaponMode mode, WeaponState state)
    {
        return new WeaponActionAvailability
        {
            IsAvailable = true,
            BlockReason = string.Empty,
            Mode = mode,
            State = state
        };
    }

    public static WeaponActionAvailability Blocked(WeaponMode mode, WeaponState state, string reason)
    {
        return new WeaponActionAvailability
        {
            IsAvailable = false,
            BlockReason = reason,
            Mode = mode,
            State = state
        };
    }
}
