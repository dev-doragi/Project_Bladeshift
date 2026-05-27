using UnityEngine;

public class WeaponAimCursor : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private Transform _cursorVisual;
    [SerializeField] private float _gamepadCursorSpeed = 12f;
    [SerializeField] private float _gamepadAimDeadzone = 0.2f;
    [SerializeField] private bool _showCursorInGamepadModeOnly = false;
    [SerializeField] private LayerMask _wallMask;

    public Vector2 CurrentWorldPosition { get; private set; }
    public Vector2 AimWorldPosition { get; private set; }
    public bool IsGamepadCursorMode { get; private set; }
    public bool IsInitialized => _isInitialized;

    private Transform _playerTransform;
    private PlayerController _playerController;
    private Camera _camera;
    private float _controlRadius;
    private bool _isInitialized;
    private bool _hasValidCursorPosition;
    private Vector2 _fallbackGamepadStartPosition;
    private bool _isCursorVisibleByUser = true;
    private bool _isSuppressedByMode;
    private Vector2 _lastPlayerPosition;

    private Vector2 _lastReachableCursorPosition;
    private bool _hasLastReachableCursorPosition;
    private int _lastUpdatedFrame = -1;

    public void Initialize(Transform playerTransform, Camera camera, float controlRadius)
    {
        _playerTransform = playerTransform;
        _playerController = _playerTransform != null ? _playerTransform.GetComponent<PlayerController>() : null;
        _camera = camera != null ? camera : Camera.main;
        _controlRadius = Mathf.Max(0f, controlRadius);

        IsGamepadCursorMode = false;
        _isSuppressedByMode = false;

        ResolveCursorVisual();
        EnsureCursorVisualStyle();

        ForceSyncToMousePositionOrFallback();

        _fallbackGamepadStartPosition = CurrentWorldPosition;
        _hasValidCursorPosition = true;
        _isInitialized = true;

        RefreshAimWorldPosition();
        UpdateCursorVisual();
    }

    public void SetWallMask(LayerMask wallMask)
    {
        _wallMask = wallMask;
    }

    public void SetFallbackGamepadStartPosition(Vector2 worldPosition)
    {
        _fallbackGamepadStartPosition = worldPosition;
    }

    public void SetCursorVisible(bool isVisible)
    {
        _isCursorVisibleByUser = isVisible;
        UpdateCursorVisual();
    }

    public void SetCursorSuppressedByMode(bool isSuppressed, bool resetToMousePosition)
    {
        _isSuppressedByMode = isSuppressed;

        if (isSuppressed)
        {
            IsGamepadCursorMode = false;
            SnapToPlayerPosition();
        }
        else
        {
            if (resetToMousePosition)
                ResetCursorByCurrentInputMode();
        }

        UpdateCursorVisual();
    }

    public void ForceSyncToMousePositionOrFallback()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (TryGetMouseWorldPosition(out Vector2 mouseWorld))
        {
            CurrentWorldPosition = mouseWorld;
        }
        else if (_playerTransform != null)
        {
            CurrentWorldPosition = _playerTransform.position;
        }
        else
        {
            CurrentWorldPosition = transform.position;
        }

        _fallbackGamepadStartPosition = CurrentWorldPosition;
        _hasValidCursorPosition = true;
        _lastPlayerPosition = _playerTransform != null ? (Vector2)_playerTransform.position : CurrentWorldPosition;
        RefreshAimWorldPosition();
        UpdateCursorTransformOnly();
    }

    public void ResetCursorPosition()
    {
        ForceSyncToMousePositionOrFallback();
    }

    public void ResetCursorByCurrentInputMode()
    {
        InputReader inputReader = InputReader.Instance;
        bool isGamepadMode = inputReader != null && inputReader.IsGamepadControlSchemeActive();

        if (isGamepadMode)
        {
            IsGamepadCursorMode = true;
            SnapToPlayerPosition();
            return;
        }

        IsGamepadCursorMode = false;
        ForceSyncToMousePositionOrFallback();
    }

    public void SnapToPlayerPosition()
    {
        if (_playerTransform != null)
        {
            CurrentWorldPosition = _playerTransform.position;
            _fallbackGamepadStartPosition = CurrentWorldPosition;
            _hasValidCursorPosition = true;
            _lastPlayerPosition = _playerTransform.position;
            RefreshAimWorldPosition();
            UpdateCursorTransformOnly();
        }
    }

    public void ResetToPlayerAimOffset(float normalizedRadius = 0.45f, float minDistance = 0.5f)
    {
        if (_playerTransform == null)
            return;

        float radius = Mathf.Max(0f, _controlRadius);
        float clampedRatio = Mathf.Clamp01(normalizedRadius);
        float distance = Mathf.Max(minDistance, radius * clampedRatio);

        Vector2 aimDirection = _playerController != null && _playerController.AimDirection.sqrMagnitude > 0.0001f
            ? _playerController.AimDirection.normalized
            : Vector2.right;

        Vector2 origin = _playerTransform.position;
        Vector2 desired = origin + aimDirection * distance;

        CurrentWorldPosition = ClampToControlRadius(desired);
        _fallbackGamepadStartPosition = CurrentWorldPosition;
        _hasValidCursorPosition = true;
        _lastPlayerPosition = origin;
        IsGamepadCursorMode = true;

        RefreshAimWorldPosition();
        UpdateCursorTransformOnly();
        UpdateCursorVisual();
    }

    public void Tick()
    {
        ManualUpdate();
    }

    public void ManualUpdate()
    {
        if (_lastUpdatedFrame == Time.frameCount)
            return;

        if (!_isInitialized)
            return;

        _lastUpdatedFrame = Time.frameCount;

        if (_playerTransform == null)
        {
            UpdateCursorVisual();
            return;
        }

        Vector2 currentPlayerPosition = _playerTransform.position;
        Vector2 playerDelta = currentPlayerPosition - _lastPlayerPosition;
        _lastPlayerPosition = currentPlayerPosition;

        if (_camera == null)
            _camera = Camera.main;

        InputReader inputReader = InputReader.Instance;
        if (inputReader == null || _camera == null)
        {
            UpdateCursorVisual();
            return;
        }

        Vector2 lookInput = inputReader.GetLookInput();
        float deadzone = Mathf.Max(0f, _gamepadAimDeadzone);
        bool hasGamepadLookInput =
            inputReader.IsLookInputFromGamepad() &&
            lookInput.sqrMagnitude >= deadzone * deadzone;

        if (_isSuppressedByMode)
        {
            UpdateCursorVisual();
            return;
        }

        bool mouseMoved = inputReader.IsMouseAiming();

        if (mouseMoved)
        {
            IsGamepadCursorMode = false;

            if (TryGetMouseWorldPosition(out Vector2 mouseWorld))
            {
                CurrentWorldPosition = mouseWorld;
                _hasValidCursorPosition = true;
            }
        }
        else if (hasGamepadLookInput)
        {
            if (!IsGamepadCursorMode)
            {
                IsGamepadCursorMode = true;
                EnsureGamepadModeStartPosition();
            }

            CurrentWorldPosition += playerDelta;
            CurrentWorldPosition += lookInput * Mathf.Max(0f, _gamepadCursorSpeed) * Time.unscaledDeltaTime;
            CurrentWorldPosition = ClampToControlRadius(CurrentWorldPosition);
            _hasValidCursorPosition = true;
        }
        else
        {
            if (!IsGamepadCursorMode)
            {
                if (TryGetMouseWorldPosition(out Vector2 mouseWorld))
                {
                    CurrentWorldPosition = mouseWorld;
                    _hasValidCursorPosition = true;
                }
            }
            else
            {
                CurrentWorldPosition += playerDelta;
                CurrentWorldPosition = ClampToControlRadius(CurrentWorldPosition);
            }
        }

        RefreshAimWorldPosition();
        UpdateCursorVisual();
    }

    private void EnsureGamepadModeStartPosition()
    {
        Vector2 origin = _playerTransform.position;
        float distanceFromOrigin = Vector2.Distance(CurrentWorldPosition, origin);
        float minDistance = Mathf.Max(0.1f, _controlRadius * 0.05f);

        if (_hasValidCursorPosition && distanceFromOrigin >= minDistance)
            return;

        Vector2 preferred = _fallbackGamepadStartPosition;

        if (Vector2.Distance(preferred, origin) < minDistance)
        {
            Vector2 aimDirection =
                _playerController != null && _playerController.AimDirection.sqrMagnitude > 0.0001f
                    ? _playerController.AimDirection.normalized
                    : Vector2.right;

            float startDistance = Mathf.Clamp(_controlRadius * 0.5f, 0.5f, Mathf.Max(0.5f, _controlRadius));
            preferred = origin + aimDirection * startDistance;
        }

        CurrentWorldPosition = ClampToControlRadius(preferred);
        _hasValidCursorPosition = true;
    }

    private bool TryGetMouseWorldPosition(out Vector2 worldPosition)
    {
        if (_camera == null || InputReader.Instance == null)
        {
            worldPosition = default;
            return false;
        }

        return PointerWorldPositionUtility.TryGetMouseWorldPosition(
            _camera,
            InputReader.Instance,
            ignorePointerOutsideScreen: true,
            out worldPosition);
    }

    private Vector2 ClampToControlRadius(Vector2 worldPosition)
    {
        if (_playerTransform == null)
            return worldPosition;

        if (_controlRadius <= 0f)
            return _playerTransform.position;

        Vector2 origin = _playerTransform.position;
        Vector2 offset = worldPosition - origin;
        float distance = offset.magnitude;

        if (distance <= _controlRadius)
            return worldPosition;

        return origin + offset.normalized * _controlRadius;
    }

    private Vector2 ConstrainToReachableWorldPosition(Vector2 desiredWorldPosition)
    {
        if (_playerTransform == null)
            return desiredWorldPosition;

        Vector2 solvedPosition = WeaponAimConstraintSolver.SolveReachablePosition(
            _playerTransform.position,
            desiredWorldPosition,
            _controlRadius,
            _wallMask,
            ref _lastReachableCursorPosition,
            _hasLastReachableCursorPosition
        );

        _hasLastReachableCursorPosition = true;
        return solvedPosition;
    }

    private void RefreshAimWorldPosition()
    {
        AimWorldPosition = ConstrainToReachableWorldPosition(CurrentWorldPosition);
    }

    public void ResetAimConstraintCache()
    {
        _hasLastReachableCursorPosition = false;

        Vector2 fallback = _playerTransform != null
            ? (Vector2)_playerTransform.position
            : CurrentWorldPosition;

        _lastReachableCursorPosition = fallback;
    }

    private void UpdateCursorVisual()
    {
        if (_cursorVisual == null)
            return;

        bool shouldShow = ShouldShowCursorVisual();
        ApplyVisualVisibility(shouldShow);

        UpdateCursorTransformOnly();
    }

    private void ApplyVisualVisibility(bool shouldShow)
    {
        if (_cursorVisual == null)
            return;

        // If visual target is this same GameObject, never SetActive(false):
        // that would disable this script and prevent automatic re-activation.
        if (_cursorVisual == transform)
        {
            SpriteRenderer selfRenderer = _cursorVisual.GetComponent<SpriteRenderer>();
            if (selfRenderer != null && selfRenderer.enabled != shouldShow)
                selfRenderer.enabled = shouldShow;
            return;
        }

        if (_cursorVisual.gameObject.activeSelf != shouldShow)
            _cursorVisual.gameObject.SetActive(shouldShow);
    }

    private void UpdateCursorTransformOnly()
    {
        Vector3 targetWorld = new Vector3(
            CurrentWorldPosition.x,
            CurrentWorldPosition.y,
            transform.position.z
        );

        transform.position = targetWorld;
        transform.rotation = Quaternion.identity;

        if (_cursorVisual != null)
            _cursorVisual.rotation = Quaternion.identity;
    }



    private void ResolveCursorVisual()
    {
        if (_cursorVisual == null)
        {
            SpriteRenderer selfRenderer = GetComponent<SpriteRenderer>();
            if (selfRenderer != null)
                _cursorVisual = transform;
        }

        if (_cursorVisual != null)
            return;

        Transform existingCursor = transform.Find("AimCursor");
        if (existingCursor != null)
        {
            _cursorVisual = existingCursor;
            return;
        }

        _cursorVisual = transform;
    }

    private void EnsureCursorVisualStyle()
    {
        if (_cursorVisual == null)
            return;

        SpriteRenderer cursorRenderer = _cursorVisual.GetComponent<SpriteRenderer>();
        if (cursorRenderer == null)
            Debug.LogWarning("[WeaponAimCursor] CursorVisual has no SpriteRenderer.", this);
    }

    private bool ShouldShowCursorVisual()
    {
        if (!_isCursorVisibleByUser)
            return false;

        if (_isSuppressedByMode)
            return false;

        if (_showCursorInGamepadModeOnly && !IsGamepadCursorMode)
            return false;

        return true;
    }
}
