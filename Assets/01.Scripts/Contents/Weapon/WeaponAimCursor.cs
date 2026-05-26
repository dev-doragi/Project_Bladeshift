using UnityEngine;

public class WeaponAimCursor : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private Transform _cursorVisual;
    [SerializeField] private float _gamepadCursorSpeed = 12f;
    [SerializeField] private float _gamepadAimDeadzone = 0.2f;
    [SerializeField] private bool _showCursorInGamepadModeOnly = true;

    public Vector2 CurrentWorldPosition { get; private set; }
    public bool IsGamepadCursorMode { get; private set; }
    public bool IsInitialized => _isInitialized;

    private Transform _playerTransform;
    private PlayerController _playerController;
    private Camera _camera;
    private float _controlRadius;
    private bool _isInitialized;
    private bool _hasValidCursorPosition;
    private Vector2 _fallbackGamepadStartPosition;

    public void Initialize(Transform playerTransform, Camera camera, float controlRadius)
    {
        _playerTransform = playerTransform;
        _playerController = _playerTransform != null ? _playerTransform.GetComponent<PlayerController>() : null;
        _camera = camera != null ? camera : Camera.main;
        _controlRadius = Mathf.Max(0f, controlRadius);
        IsGamepadCursorMode = false;
        ResolveCursorVisual();
        EnsureCursorVisualStyle();

        if (!TryGetMouseWorldPosition(out Vector2 mouseWorld))
        {
            mouseWorld = _playerTransform != null ? (Vector2)_playerTransform.position : Vector2.zero;
        }

        CurrentWorldPosition = mouseWorld;
        _fallbackGamepadStartPosition = CurrentWorldPosition;
        _hasValidCursorPosition = true;
        _isInitialized = true;
        UpdateCursorVisual();
    }

    public void SetFallbackGamepadStartPosition(Vector2 worldPosition)
    {
        _fallbackGamepadStartPosition = worldPosition;
    }

    public void Tick()
    {
        ManualUpdate();
    }

    public void ManualUpdate()
    {
        if (!_isInitialized)
            return;

        if (_playerTransform == null)
        {
            UpdateCursorVisual();
            return;
        }

        if (_camera == null)
            _camera = Camera.main;

        InputReader inputReader = InputReader.Instance;
        if (inputReader == null || _camera == null)
        {
            UpdateCursorVisual();
            return;
        }

        bool mouseMoved = inputReader.IsMouseAiming();
        Vector2 lookInput = inputReader.GetLookInput();
        float deadzone = Mathf.Max(0f, _gamepadAimDeadzone);
        bool hasGamepadLookInput = inputReader.IsLookInputFromGamepad() && lookInput.sqrMagnitude >= deadzone * deadzone;

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
                CurrentWorldPosition = ClampToControlRadius(CurrentWorldPosition);
            }
        }

        UpdateCursorVisual();
    }

    private void Update()
    {
        ManualUpdate();
    }

    private void EnsureGamepadModeStartPosition()
    {
        Vector2 origin = _playerTransform.position;
        float distanceFromOrigin = Vector2.Distance(CurrentWorldPosition, origin);
        float minDistance = Mathf.Max(0.1f, _controlRadius * 0.05f);

        if (_hasValidCursorPosition && distanceFromOrigin >= minDistance)
            return;

        Vector2 preferred = _fallbackGamepadStartPosition;
        Vector2 weaponWorldPosition = transform.position;
        if (Vector2.Distance(weaponWorldPosition, origin) >= minDistance)
        {
            preferred = weaponWorldPosition;
        }
        else if (Vector2.Distance(preferred, origin) < minDistance)
        {
            Vector2 aimDirection = _playerController != null && _playerController.AimDirection.sqrMagnitude > 0.0001f
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
        worldPosition = default;
        if (_camera == null || InputReader.Instance == null)
            return false;

        Vector2 screenPos = InputReader.Instance.GetMousePosition();
        if (screenPos.x < 0f || screenPos.y < 0f || screenPos.x > Screen.width || screenPos.y > Screen.height)
            return false;

        worldPosition = _camera.ScreenToWorldPoint(screenPos);
        return true;
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

    private void UpdateCursorVisual()
    {
        if (_cursorVisual == null)
            return;

        if (!_cursorVisual.gameObject.activeSelf)
            _cursorVisual.gameObject.SetActive(true);

        _cursorVisual.position = new Vector3(
            CurrentWorldPosition.x,
            CurrentWorldPosition.y,
            _cursorVisual.position.z
        );

        _cursorVisual.rotation = Quaternion.identity;
    }

    private void ResolveCursorVisual()
    {
        if (_cursorVisual != null)
            return;

        Transform existingCursor = transform.Find("AimCursor");
        if (existingCursor != null)
        {
            _cursorVisual = existingCursor;
            return;
        }

        GameObject cursorObject = new GameObject("AimCursor");
        cursorObject.transform.SetParent(transform, false);
        _cursorVisual = cursorObject.transform;
    }

    private void EnsureCursorVisualStyle()
    {
        if (_cursorVisual == null)
            return;

        SpriteRenderer cursorRenderer = _cursorVisual.GetComponent<SpriteRenderer>();
        if (cursorRenderer == null)
        {
            Debug.LogWarning("[WeaponAimCursor] CursorVisual has no SpriteRenderer.", this);
        }
    }
}
