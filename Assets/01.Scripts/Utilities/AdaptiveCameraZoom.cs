using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(CinemachineCamera))]
public class AdaptiveCameraZoom : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Transform _weaponTransform;
    [SerializeField] private WeaponController _weaponController;

    [Header("Distance")]
    [SerializeField] private float _minDistance = 2f;
    [SerializeField] private float _maxDistance = 18f;

    [Header("Orthographic Size")]
    [SerializeField] private float _remoteControlZoomedInSize = 7f;
    [SerializeField] private float _swordZoomedInSize = 7f;
    [SerializeField] private float _zoomedOutSize = 11f;

    [Header("Smoothing")]
    [SerializeField] private bool _smoothZoom = true;
    [SerializeField] private float _zoomSmoothTime = 0.15f;
    [SerializeField] private float _zoomMaxSpeed = 50f;

    private float _zoomVelocity;

    private void Awake()
    {
        if (_cinemachineCamera == null)
            _cinemachineCamera = GetComponent<CinemachineCamera>();

        ResolveTargets();
    }

    private void LateUpdate()
    {
        ResolveTargets();

        if (_cinemachineCamera == null || _playerTransform == null || _weaponTransform == null)
            return;

        if (_weaponController != null &&
            (_weaponController.CurrentState == WeaponState.Thrusting ||
             _weaponController.CurrentState == WeaponState.PinningFlight ||
             _weaponController.CurrentState == WeaponState.Returning))
        {
            _zoomVelocity = 0f;
            return;
        }

        float distance = Vector2.Distance(_playerTransform.position, _weaponTransform.position);
        float targetSize = Mathf.Lerp(GetZoomedInSize(), _zoomedOutSize, GetDistanceT(distance));
        float currentSize = _cinemachineCamera.Lens.OrthographicSize;

        float nextSize = _smoothZoom && _zoomSmoothTime > 0f
            ? Mathf.SmoothDamp(currentSize, targetSize, ref _zoomVelocity, _zoomSmoothTime, Mathf.Max(0f, _zoomMaxSpeed), Time.deltaTime)
            : targetSize;

        _cinemachineCamera.Lens.OrthographicSize = Mathf.Max(0.01f, nextSize);
    }

    private float GetZoomedInSize()
    {
        if (_weaponController != null && _weaponController.CurrentMode == WeaponMode.Melee)
            return _swordZoomedInSize;

        return _remoteControlZoomedInSize;
    }

    private float GetDistanceT(float distance)
    {
        float minDistance = Mathf.Max(0f, _minDistance);
        float maxDistance = Mathf.Max(minDistance + 0.01f, _maxDistance);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minDistance, maxDistance, distance));
    }

    private void ResolveTargets()
    {
        if (_playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                _playerTransform = playerObject.transform;
        }

        if (_weaponTransform == null)
        {
            WeaponController weaponController = FindFirstObjectByType<WeaponController>();
            if (weaponController != null)
                _weaponTransform = weaponController.transform;
        }

        if (_weaponController == null && _weaponTransform != null)
            _weaponController = _weaponTransform.GetComponent<WeaponController>();
    }
}
