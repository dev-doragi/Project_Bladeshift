using UnityEngine;

public class WeaponMeleeHoverFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Transform _meleeHoverPivot;
    [SerializeField] private Transform _meleeHoverHoldPoint;
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private Collider2D _collider;

    [Header("Follow")]
    [SerializeField] private bool _snapFollow = true;
    [SerializeField] private float _followSmoothTime = 0.08f;
    [SerializeField] private float _followMaxSpeed = 30f;

    [Header("Hover Bob")]
    [SerializeField] private float _bobAmplitude = 0.08f;
    [SerializeField] private float _bobFrequency = 4f;

    [Header("Pivot")]
    [SerializeField] private bool _rotatePivotToAim = false;
    [SerializeField] private float _pivotRotationOffset = 0f;

    [Header("Physics")]
    [SerializeField] private bool _disableColliderWhileFollowing = true;

    private Vector3 _followVelocity;

    private RigidbodyType2D _cachedBodyType;
    private float _cachedGravityScale;
    private bool _cachedColliderEnabled;
    private bool _hasCachedPhysics;

    private bool _isFollowEnabled;
    private bool _isFollowLocked;

    public bool IsFollowEnabled => _isFollowEnabled;
    public bool IsFollowLocked => _isFollowLocked;
    public Transform MeleePivot => _meleeHoverPivot;
    public Transform MeleeHoverHoldPoint => _meleeHoverHoldPoint;

    private void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();

        if (_collider == null)
            _collider = GetComponent<Collider2D>();
    }

    public void Initialize(PlayerController playerController, Rigidbody2D weaponRigidbody, Collider2D weaponCollider)
    {
        if (playerController != null)
            _playerController = playerController;

        if (weaponRigidbody != null)
            _rigidbody = weaponRigidbody;

        if (weaponCollider != null)
            _collider = weaponCollider;
    }

    private void LateUpdate()
    {
        if (_isFollowEnabled)
            UpdateHoverPivot();

        TickFollow();
    }

    public void EnableFollow()
    {
        _isFollowEnabled = true;
        _isFollowLocked = false;
        _followVelocity = Vector3.zero;

        EnterFollowPhysics();
        UpdateHoverPivot();
        SnapToHoldPoint();
    }

    public void DisableFollow()
    {
        _isFollowEnabled = false;
        _isFollowLocked = false;
        _followVelocity = Vector3.zero;

        ExitFollowPhysics();
    }

    public void Attach()
    {
        EnableFollow();
    }

    public void Detach()
    {
        DisableFollow();
    }

    public void SetFollowLocked(bool locked)
    {
        _isFollowLocked = locked;

        if (!locked)
            _followVelocity = Vector3.zero;
    }

    private void UpdateHoverPivot()
    {
        if (_isFollowLocked)
            return;

        if (_playerController == null || _meleeHoverPivot == null)
            return;

        RotatePivotToAim();
    }

    private void RotatePivotToAim()
    {
        if (!_rotatePivotToAim)
            return;

        Vector2 aimDirection = _playerController.AimDirection;
        if (aimDirection.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        _meleeHoverPivot.rotation = Quaternion.Euler(0f, 0f, angle + _pivotRotationOffset);
    }

    private void TickFollow()
    {
        if (!_isFollowEnabled || _isFollowLocked)
            return;

        if (_meleeHoverHoldPoint == null)
            return;

        Vector3 targetPosition = _meleeHoverHoldPoint.position + (Vector3)GetBobOffset();

        if (_snapFollow)
        {
            transform.position = targetPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref _followVelocity,
                Mathf.Max(0.0001f, _followSmoothTime),
                Mathf.Max(0.001f, _followMaxSpeed),
                Time.deltaTime
            );
        }

        transform.rotation = _meleeHoverHoldPoint.rotation;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }
    }

    private void SnapToHoldPoint()
    {
        if (_meleeHoverHoldPoint == null)
            return;

        transform.position = _meleeHoverHoldPoint.position + (Vector3)GetBobOffset();
        transform.rotation = _meleeHoverHoldPoint.rotation;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }
    }

    private void EnterFollowPhysics()
    {
        if (_rigidbody != null)
        {
            if (!_hasCachedPhysics)
            {
                _cachedBodyType = _rigidbody.bodyType;
                _cachedGravityScale = _rigidbody.gravityScale;
            }

            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.gravityScale = 0f;
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        if (_collider != null && _disableColliderWhileFollowing)
        {
            if (!_hasCachedPhysics)
                _cachedColliderEnabled = _collider.enabled;

            _collider.enabled = false;
        }

        _hasCachedPhysics = true;
    }

    private void ExitFollowPhysics()
    {
        if (!_hasCachedPhysics)
            return;

        if (_rigidbody != null)
        {
            _rigidbody.bodyType = _cachedBodyType;
            _rigidbody.gravityScale = _cachedGravityScale;
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }

        if (_collider != null && _disableColliderWhileFollowing)
            _collider.enabled = _cachedColliderEnabled;
    }

    private Vector2 GetBobOffset()
    {
        if (_bobAmplitude <= 0f || _bobFrequency <= 0f)
            return Vector2.zero;

        float phase = Time.time * _bobFrequency;

        return new Vector2(
            Mathf.Cos(phase * 0.7f),
            Mathf.Sin(phase)
        ) * _bobAmplitude;
    }
}