using UnityEngine;

public struct WeaponActionCommand
{
    public WeaponActionInputType ActionType;
    public InputCommandPhase Phase;
    public WeaponInputDevice Device;
    public int StartedFrame;
    public float StartedTime;

    public bool IsStarted => Phase == InputCommandPhase.Started || Phase == InputCommandPhase.Performed;
    public bool IsCanceled => Phase == InputCommandPhase.Canceled;

    public WeaponActionInputContext ToInputContext()
    {
        return new WeaponActionInputContext
        {
            ActionType = ActionType,
            Device = Device,
            StartedFrame = StartedFrame,
            StartedTime = StartedTime
        };
    }

    public static WeaponActionCommand Create(
        WeaponActionInputType actionType,
        InputCommandPhase phase,
        WeaponInputDevice device)
    {
        return new WeaponActionCommand
        {
            ActionType = actionType,
            Phase = phase,
            Device = device,
            StartedFrame = Time.frameCount,
            StartedTime = Time.unscaledTime
        };
    }
}
