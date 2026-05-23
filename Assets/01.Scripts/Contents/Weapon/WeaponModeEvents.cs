public struct WeaponModeToggleEvent { }

public struct WeaponModeChangeEvent
{
    public WeaponMode PreviousMode;
    public WeaponMode NewMode;
}
