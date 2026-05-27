using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MouseWorldProxyFollower : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private WeaponAimCursor _aimCursor;
    [SerializeField] private bool _usePlayerControlRadius = true;
    [SerializeField] private float _cameraInfluenceRadius = 20f;
    [SerializeField] private float _recoverDeadZoneRadius = 5f;
    [SerializeField] private bool _smoothDeadZoneBlend = true;
    [SerializeField] private bool _smoothProxyMotion = true;
    [SerializeField] private float _proxySmoothTime = 0.08f;
    [SerializeField] private float _proxyMaxSpeed = 100f;
    [SerializeField] private bool _ignorePointerOutsideScreen = true;

    private PlayerController _playerController;
    private Vector3 _proxyVelocity;

    public void SnapToPlayerPosition()
    {
        ResolvePlayer();
        if (_playerTransform == null)
            return;

        Vector3 playerPos = _playerTransform.position;
        playerPos.z = transform.position.z;
        transform.position = playerPos;
        _proxyVelocity = Vector3.zero;
    }

    public void ResetByCurrentInputMode()
    {
        ResolvePlayer();
        ResolveAimCursor();

        if (_playerTransform == null)
            return;

        InputReader inputReader = InputReader.Instance;
        bool isGamepadMode = inputReader != null && inputReader.IsGamepadControlSchemeActive();
        if (isGamepadMode)
        {
            SnapToPlayerPosition();
            return;
        }

        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam == null)
        {
            SnapToPlayerPosition();
            return;
        }

        if (_aimCursor != null && _aimCursor.IsInitialized)
        {
            Vector3 aimWorld = _aimCursor.CurrentWorldPosition;
            aimWorld.z = transform.position.z;
            transform.position = GetProxyPosition(aimWorld);
            _proxyVelocity = Vector3.zero;
            return;
        }

        if (inputReader == null)
        {
            SnapToPlayerPosition();
            return;
        }

        Vector2 screenPosition = inputReader.GetMousePosition();
        if (_ignorePointerOutsideScreen && IsOutsideScreen(screenPosition))
        {
            SnapToPlayerPosition();
            return;
        }

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = transform.position.z;
        transform.position = GetProxyPosition(worldPosition);
        _proxyVelocity = Vector3.zero;
    }

    private void Awake()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        ResolvePlayer();
        ResolveAimCursor();
    }

    private void Update()
    {
        if (InputReader.Instance == null)
            return;

        ResolvePlayer();
        ResolveAimCursor();

        if (_aimCursor != null && _aimCursor.IsInitialized)
        {
            Vector3 aimWorld = _aimCursor.CurrentWorldPosition;
            aimWorld.z = transform.position.z;

            if (_playerTransform == null)
            {
                MoveProxy(aimWorld);
                return;
            }

            MoveProxy(GetProxyPosition(aimWorld));
            return;
        }

        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam == null)
            return;

        Vector2 screenPosition = InputReader.Instance.GetMousePosition();
        if (_ignorePointerOutsideScreen && IsOutsideScreen(screenPosition))
            return;

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition.z = transform.position.z;

        if (_playerTransform == null)
        {
            MoveProxy(worldPosition);
            return;
        }

        MoveProxy(GetProxyPosition(worldPosition));
    }

    private Vector3 GetProxyPosition(Vector3 mouseWorldPosition)
    {
        Vector3 playerPosition = _playerTransform.position;
        playerPosition.z = transform.position.z;

        Vector2 offset = mouseWorldPosition - playerPosition;
        float distance = offset.magnitude;
        float recoverDeadZoneRadius = Mathf.Max(0f, _recoverDeadZoneRadius);

        if (distance <= recoverDeadZoneRadius)
            return playerPosition;

        float influenceRadius = GetCameraInfluenceRadius();
        Vector3 clampedMousePosition = mouseWorldPosition;

        if (influenceRadius > 0f && distance > influenceRadius)
        {
            Vector2 clampedOffset = offset.normalized * influenceRadius;
            clampedMousePosition = new Vector3(playerPosition.x + clampedOffset.x, playerPosition.y + clampedOffset.y, transform.position.z);
        }

        if (!_smoothDeadZoneBlend || influenceRadius <= recoverDeadZoneRadius)
            return clampedMousePosition;

        float blend = Mathf.InverseLerp(recoverDeadZoneRadius, influenceRadius, distance);
        blend = Mathf.SmoothStep(0f, 1f, blend);
        return Vector3.Lerp(playerPosition, clampedMousePosition, blend);
    }

    private void MoveProxy(Vector3 targetPosition)
    {
        if (!_smoothProxyMotion || _proxySmoothTime <= 0f)
        {
            transform.position = targetPosition;
            _proxyVelocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _proxyVelocity,
            _proxySmoothTime,
            Mathf.Max(0f, _proxyMaxSpeed),
            Time.unscaledDeltaTime);
    }

    private float GetCameraInfluenceRadius()
    {
        if (_usePlayerControlRadius && _playerController != null)
            return Mathf.Max(0f, _playerController.ControlRadius);

        return Mathf.Max(0f, _cameraInfluenceRadius);
    }

    private void ResolvePlayer()
    {
        if (_playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                _playerTransform = playerObject.transform;
        }

        if (_playerController == null && _playerTransform != null)
            _playerController = _playerTransform.GetComponent<PlayerController>();
    }

    private void ResolveAimCursor()
    {
        if (_aimCursor == null)
            _aimCursor = FindObjectOfType<WeaponAimCursor>();
    }

    private static bool IsOutsideScreen(Vector2 screenPosition)
    {
        return screenPosition.x < 0f ||
               screenPosition.y < 0f ||
               screenPosition.x > Screen.width ||
               screenPosition.y > Screen.height;
    }
}
