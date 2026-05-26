using UnityEngine;

public class WeaponSensor : MonoBehaviour
{
    [Header("Range Tuning")]
    [SerializeField] private float _acquireRadiusRatio = 1f;
    [SerializeField] private float _releaseRadiusRatio = 1.2f;
    [SerializeField] private float _mouseCaptureRadius = 1f;
    [SerializeField] private float _mouseCaptureMaintainRadius = 1.5f;
    [SerializeField] private float _lineOfSightMargin = 0.35f;
    [SerializeField] private WeaponAimCursor _aimCursor;

    private Transform _playerTransform;
    private Camera _mainCamera;
    private float _controlRadius;
    private Vector2 _lastValidMousePos;

    private Vector2 _lastReachableTargetPosition;
    private bool _hasLastReachableTargetPosition;

    public void Initialize(Transform playerTransform, Camera mainCamera, float controlRadius)
    {
        _playerTransform = playerTransform;
        _mainCamera = mainCamera != null ? mainCamera : Camera.main;
        _controlRadius = controlRadius;
        if (_aimCursor == null)
            _aimCursor = GetComponentInChildren<WeaponAimCursor>(true);
        _lastValidMousePos = _playerTransform != null ? (Vector2)_playerTransform.position : Vector2.zero;

        ResetAimConstraintCache();
    }

    public void Configure(Transform playerTransform, Camera mainCamera, float controlRadius, float mouseCaptureRadius, float mouseCaptureMaintainRadius, float breakMouseSpeed)
    {
        _mouseCaptureRadius = mouseCaptureRadius;
        _mouseCaptureMaintainRadius = mouseCaptureMaintainRadius > 0f ? mouseCaptureMaintainRadius : (_mouseCaptureRadius * 1.5f);
        Initialize(playerTransform, mainCamera, controlRadius);
    }

    public Vector2 GetMouseWorldPosition()
    {
        if (_playerTransform == null)
            return _lastValidMousePos;

        if (_aimCursor != null && _aimCursor.IsInitialized)
        {
            _lastValidMousePos = _aimCursor.CurrentWorldPosition;
            return _lastValidMousePos;
        }

        InputReader inputReader = InputReader.Instance;
        if (_mainCamera == null || inputReader == null)
            return _lastValidMousePos;

        Vector2 screenPos = inputReader.GetMousePosition();
        if (screenPos.x < 0f || screenPos.y < 0f || screenPos.x > Screen.width || screenPos.y > Screen.height)
            return _lastValidMousePos;

        Vector2 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);
        _lastValidMousePos = worldPos;
        return worldPos;
    }

    public void SetAimCursor(WeaponAimCursor aimCursor)
    {
        _aimCursor = aimCursor;
    }

    public bool IsPlayerInRange(Vector3 weaponPosition)
    {
        if (_playerTransform == null) return false;
        return Vector2.Distance(weaponPosition, _playerTransform.position) <= _controlRadius;
    }

    public bool HasLineOfSight(Vector3 weaponPosition, LayerMask wallMask)
    {
        if (_playerTransform == null) return false;

        Vector2 start = _playerTransform.position;
        Vector2 end = weaponPosition;
        Vector2 direction = end - start;
        float distance = direction.magnitude;

        if (distance <= 0.01f) return true;

        float checkDistance = Mathf.Max(0f, distance - Mathf.Max(0f, _lineOfSightMargin));
        if (checkDistance <= 0f) return true;

        RaycastHit2D hit = Physics2D.Raycast(start, direction.normalized, checkDistance, wallMask);
        return hit.collider == null;
    }

    public Vector2 GetClampedTargetPosition(LayerMask wallMask)
    {
        if (_playerTransform == null)
            return GetMouseWorldPosition();

        Vector2 solvedPosition = WeaponAimConstraintSolver.SolveReachablePosition(
            _playerTransform.position,
            GetMouseWorldPosition(),
            _controlRadius,
            wallMask,
            ref _lastReachableTargetPosition,
            _hasLastReachableTargetPosition
        );

        _hasLastReachableTargetPosition = true;
        return solvedPosition;
    }

    public void ResetAimConstraintCache()
    {
        _hasLastReachableTargetPosition = false;

        Vector2 fallback = _playerTransform != null
            ? (Vector2)_playerTransform.position
            : _lastValidMousePos;

        _lastReachableTargetPosition = fallback;
    }

    public bool IsMouseInRange(Vector2 mousePos, bool alreadyControlled)
    {
        if (_playerTransform == null) return false;

        float ratio = alreadyControlled ? Mathf.Max(1f, _releaseRadiusRatio) : Mathf.Max(0f, _acquireRadiusRatio);
        float threshold = _controlRadius * ratio;
        return Vector2.Distance(mousePos, _playerTransform.position) <= threshold;
    }

    public bool IsMouseHovering(Vector3 weaponPosition, Vector2 mousePos)
    {
        return Vector2.Distance(weaponPosition, mousePos) <= _mouseCaptureRadius;
    }

    public bool IsMouseMaintainingControl(Vector3 weaponPosition, Vector2 mousePos)
    {
        return Vector2.Distance(weaponPosition, mousePos) <= _mouseCaptureMaintainRadius;
    }

    public bool ShouldAcquireControl(Vector3 weaponPosition, Vector2 mousePos, LayerMask wallMask)
    {
        return IsPlayerInRange(weaponPosition) && IsMouseInRange(mousePos, false) && HasLineOfSight(weaponPosition, wallMask);
    }

    public Transform GetPlayerTransform()
    {
        return _playerTransform;
    }

    public bool ShouldReleaseControl(Vector3 weaponPosition, Vector2 mousePos, LayerMask wallMask)
    {
        return !IsPlayerInRange(weaponPosition) || !IsMouseInRange(mousePos, true) || !HasLineOfSight(weaponPosition, wallMask);
    }
}
