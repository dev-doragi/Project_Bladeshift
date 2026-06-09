using System;

public sealed class WeaponAimPresentation
{
    private WeaponAimCursor _aimCursor;
    private MouseWorldProxyFollower _mouseWorldProxyFollower;
    private Func<WeaponMode> _getMode;
    private Func<bool> _isCursorVisible;

    public void Initialize(
        WeaponAimCursor aimCursor,
        MouseWorldProxyFollower mouseWorldProxyFollower,
        Func<WeaponMode> getMode,
        Func<bool> isCursorVisible)
    {
        _aimCursor = aimCursor;
        _mouseWorldProxyFollower = mouseWorldProxyFollower;
        _getMode = getMode;
        _isCursorVisible = isCursorVisible;
    }

    public void ApplyModePolicy(bool resetCursorPosition)
    {
        if (_aimCursor == null || _getMode == null)
            return;

        bool suppressCursor = _getMode.Invoke() == WeaponMode.Melee;
        bool shouldResetToMouse = resetCursorPosition && !suppressCursor;
        _aimCursor.SetCursorSuppressedByMode(suppressCursor, shouldResetToMouse);

        if (_mouseWorldProxyFollower == null)
            return;

        if (suppressCursor)
            _mouseWorldProxyFollower.SnapToPlayerPosition();
        else if (resetCursorPosition)
            _mouseWorldProxyFollower.ResetByCurrentInputMode();
    }

    public void RefreshVisibility()
    {
        if (_aimCursor == null)
            return;

        ApplyModePolicy(false);
        _aimCursor.SetCursorVisible(_isCursorVisible == null || _isCursorVisible.Invoke());
    }
}
