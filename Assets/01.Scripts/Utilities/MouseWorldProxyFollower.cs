using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MouseWorldProxyFollower : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Transform _playerTransform;
    [Header("Gamepad Source")]
    [SerializeField] private WeaponAimCursor _aimCursor;

    [Header("Proxy Shape")]
    [SerializeField] private bool _usePlayerControlRadius = true;
    [SerializeField] private float _cameraInfluenceRadius = 20f;
    [SerializeField] private float _recoverDeadZoneRadius = 5f;
    [SerializeField] private bool _smoothDeadZoneBlend = true;

    [Header("Motion")]
    [SerializeField] private bool _smoothProxyMotion = true;
    [SerializeField] private bool _smoothMouseProxyMotion = false;
    [SerializeField] private float _proxySmoothTime = 0.08f;
    [SerializeField] private float _proxyMaxSpeed = 100f;

    [Header("Input")]
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

        if (_playerTransform == null)
            return;

        InputReader inputReader = InputReader.Instance;
        bool isGamepadMode = inputReader != null && inputReader.IsGamepadControlSchemeActive();
        if (isGamepadMode)
        {
            ResolveAimCursor();
            if (_aimCursor != null && _aimCursor.IsInitialized)
            {
                Vector3 aimWorld = _aimCursor.AimWorldPosition;
                aimWorld.z = transform.position.z;
                transform.position = GetProxyPosition(aimWorld);
                _proxyVelocity = Vector3.zero;
                return;
            }

            SnapToPlayerPosition();
            return;
        }

        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam == null)
        {
            SnapToPlayerPosition();
            return;
        }

        if (inputReader == null)
        {
            SnapToPlayerPosition();
            return;
        }

        if (!PointerWorldPositionUtility.TryGetMouseWorldPosition(
                cam,
                inputReader,
                _ignorePointerOutsideScreen,
                out Vector2 mouseWorld))
        {
            SnapToPlayerPosition();
            return;
        }

        Vector3 worldPosition = new Vector3(mouseWorld.x, mouseWorld.y, transform.position.z);
        transform.position = GetProxyPosition(worldPosition);
        _proxyVelocity = Vector3.zero;
    }

    private void Awake()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        ResolvePlayer();
    }

    private void Update()
    {
        InputReader inputReader = InputReader.Instance;
        if (inputReader == null)
            return;

        ResolvePlayer();
        bool isGamepadMode = inputReader.IsGamepadControlSchemeActive();

        if (isGamepadMode)
        {
            ResolveAimCursor();
            if (_aimCursor == null || !_aimCursor.IsInitialized)
            {
                if (_playerTransform != null)
                    MoveProxy(GetProxyPosition(_playerTransform.position), shouldSmooth: _smoothProxyMotion);
                return;
            }

            Vector3 aimWorld = _aimCursor.AimWorldPosition;
            aimWorld.z = transform.position.z;

            if (_playerTransform == null)
            {
                MoveProxy(aimWorld, shouldSmooth: _smoothProxyMotion);
                return;
            }

            MoveProxy(GetProxyPosition(aimWorld), shouldSmooth: _smoothProxyMotion);
            return;
        }

        Camera cam = _targetCamera != null ? _targetCamera : Camera.main;
        if (cam == null)
            return;

        if (!PointerWorldPositionUtility.TryGetMouseWorldPosition(
                cam,
                inputReader,
                _ignorePointerOutsideScreen,
                out Vector2 mouseWorld))
        {
            return;
        }

        Vector3 worldPosition = new Vector3(mouseWorld.x, mouseWorld.y, transform.position.z);

        if (_playerTransform == null)
        {
            MoveProxy(worldPosition, shouldSmooth: _smoothProxyMotion && _smoothMouseProxyMotion);
            return;
        }

        MoveProxy(GetProxyPosition(worldPosition), shouldSmooth: _smoothProxyMotion && _smoothMouseProxyMotion);
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

    private void MoveProxy(Vector3 targetPosition, bool shouldSmooth)
    {
        if (!shouldSmooth || _proxySmoothTime <= 0f)
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
}
