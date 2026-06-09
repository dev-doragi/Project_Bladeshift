using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAimResolver : MonoBehaviour, IAimSource
{
    [SerializeField] private WeaponAimCursor _weaponAimCursor;
    [SerializeField] private WeaponSensor _weaponSensor;
    [SerializeField] private WeaponModeController _weaponModeController;

    private Camera _mainCamera;
    private PlayerAimState _currentAimState = PlayerAimState.Default;

    public void Initialize(Camera mainCamera)
    {
        _mainCamera = mainCamera != null ? mainCamera : Camera.main;
        _currentAimState = PlayerAimState.Default;
    }

    public void Configure(WeaponAimCursor aimCursor, WeaponSensor weaponSensor, WeaponModeController modeController)
    {
        _weaponAimCursor = aimCursor;
        _weaponSensor = weaponSensor;
        _weaponModeController = modeController;
    }

    public PlayerAimState Resolve(Transform originTransform, Vector2 moveInput, int currentFacingSign)
    {
        if (originTransform == null || InputReader.Instance == null || InputReader.Instance.IsInputBlocked)
            return _currentAimState;

        PlayerAimState nextState = _currentAimState;
        nextState.FacingSign = currentFacingSign == 0 ? 1 : currentFacingSign;
        nextState.IsUsingGamepadAim = false;
        nextState.IsMeleeConstrained = _weaponModeController != null && _weaponModeController.CurrentMode == WeaponMode.Melee;

        if (nextState.IsMeleeConstrained)
        {
            ResolveMeleeAim(moveInput, ref nextState);
            _currentAimState = nextState;
            return _currentAimState;
        }

        Vector2 lookInput = InputReader.Instance.GetLookInput();
        if (InputReader.Instance.IsGamepadLookActive() && lookInput.sqrMagnitude > 0.0001f)
        {
            nextState.AimDirection = lookInput.normalized;
            nextState.AimWorldPosition = (Vector2)originTransform.position + nextState.AimDirection;
            nextState.FacingSign = nextState.AimDirection.x >= 0f ? 1 : -1;
            nextState.IsUsingGamepadAim = true;
            _currentAimState = nextState;
            return _currentAimState;
        }

        Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null)
            return _currentAimState;

        Vector2 origin = originTransform.position;
        Vector2 aimWorld = ResolveAimWorldPosition(cam);
        Vector2 direction = aimWorld - origin;
        if (direction.sqrMagnitude <= 0.0001f)
            return _currentAimState;

        nextState.AimDirection = direction.normalized;
        nextState.AimWorldPosition = aimWorld;
        nextState.FacingSign = nextState.AimDirection.x >= 0f ? 1 : -1;
        _currentAimState = nextState;
        return _currentAimState;
    }

    public PlayerAimState GetAimState()
    {
        return _currentAimState;
    }

    private Vector2 ResolveAimWorldPosition(Camera cam)
    {
        if (_weaponAimCursor != null && _weaponAimCursor.IsInitialized)
            return _weaponAimCursor.AimWorldPosition;

        if (_weaponSensor != null)
            return _weaponSensor.GetMouseWorldPosition();

        Vector2 screenPos = InputReader.Instance.GetMousePosition();
        return cam.ScreenToWorldPoint(screenPos);
    }

    private static void ResolveMeleeAim(Vector2 moveInput, ref PlayerAimState aimState)
    {
        Vector2 lookInput = InputReader.Instance.GetLookInput();
        float absX = Mathf.Abs(lookInput.x);
        if (InputReader.Instance.IsGamepadLookActive() && absX > 0.15f)
        {
            aimState.FacingSign = lookInput.x >= 0f ? 1 : -1;
        }
        else if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            aimState.FacingSign = moveInput.x >= 0f ? 1 : -1;
        }

        aimState.AimDirection = new Vector2(aimState.FacingSign, 0f);
        aimState.AimWorldPosition = aimState.AimDirection;
    }
}
