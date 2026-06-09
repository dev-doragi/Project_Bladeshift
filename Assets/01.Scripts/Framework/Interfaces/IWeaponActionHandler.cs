public interface IWeaponActionHandler
{
    WeaponActionAvailability GetAvailability(WeaponActionCommand command);
    bool TryStartPrimaryAction(WeaponActionCommand command);
    bool TryStartSecondaryAction(WeaponActionCommand command);
}
