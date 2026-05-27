public enum WeaponInputDevice
{
    Unknown = 0,
    MouseKeyboard = 1,
    Gamepad = 2
}

public enum WeaponActionInputType
{
    Primary = 0,
    Secondary = 1
}

public struct WeaponActionInputContext
{
    public WeaponActionInputType ActionType;
    public WeaponInputDevice Device;
    public int StartedFrame;
    public float StartedTime;

    public bool IsGamepad => Device == WeaponInputDevice.Gamepad;
    public bool IsMouseKeyboard => Device == WeaponInputDevice.MouseKeyboard;
}

