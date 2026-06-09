using UnityEngine;

public sealed class WeaponModeService
{
    private readonly WeaponController _controller;

    public WeaponModeService(WeaponController controller)
    {
        _controller = controller;
    }

    public bool CanToggleMode()
    {
        if (_controller == null || _controller.ModeController == null)
            return false;
        if (_controller.IsActionInputBlocked)
            return false;
        if (_controller.PlayerTransform == null)
            return false;

        WeaponState state = _controller.CurrentState;
        if (state == WeaponState.Slashing ||
            state == WeaponState.Thrusting ||
            state == WeaponState.PinningFlight ||
            state == WeaponState.Pinned ||
            state == WeaponState.Returning)
            return false;

        float controlRadius = _controller.ControlRadius;
        if (controlRadius <= 0f)
            return false;

        float distance = Vector2.Distance(_controller.PlayerTransform.position, _controller.transform.position);
        return distance <= controlRadius;
    }
}
