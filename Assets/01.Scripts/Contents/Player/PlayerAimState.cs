using UnityEngine;

public struct PlayerAimState
{
    public Vector2 AimDirection;
    public Vector2 AimWorldPosition;
    public int FacingSign;
    public bool IsUsingGamepadAim;
    public bool IsMeleeConstrained;

    public static PlayerAimState Default => new PlayerAimState
    {
        AimDirection = Vector2.right,
        AimWorldPosition = Vector2.right,
        FacingSign = 1,
        IsUsingGamepadAim = false,
        IsMeleeConstrained = false
    };
}
