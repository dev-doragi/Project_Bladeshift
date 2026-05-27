using UnityEngine.InputSystem;

public static class InputDeviceTracker
{
    public static WeaponInputDevice CurrentDevice { get; private set; } = WeaponInputDevice.MouseKeyboard;

    public static bool IsGamepad => CurrentDevice == WeaponInputDevice.Gamepad;
    public static bool IsMouseKeyboard => CurrentDevice == WeaponInputDevice.MouseKeyboard;

    public static void SetFromControl(InputControl control)
    {
        if (control == null || control.device == null)
            return;

        if (control.device is Gamepad)
        {
            CurrentDevice = WeaponInputDevice.Gamepad;
            return;
        }

        if (control.device is Keyboard || control.device is Mouse)
        {
            CurrentDevice = WeaponInputDevice.MouseKeyboard;
        }
    }

    public static void SetFromContext(WeaponActionInputContext context)
    {
        if (context.Device == WeaponInputDevice.Unknown)
            return;

        CurrentDevice = context.Device;
    }
}