using UnityEngine;

public class WeaponMeleeHoverFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Transform _meleeHoverPivot;
    [SerializeField] private Transform _meleeOrbitRoot;
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

    [Header("Aim Orbit")]
    [SerializeField] private bool _enableAimOrbit = true;
    [SerializeField] private float _maxOrbitAngle = 18f;
    [SerializeField] private float _orbitSmoothTime = 0.08f;
    [SerializeField] private bool _counterRotateHoldPoint = true;
    [SerializeField] private bool _invertOrbitAngle = false;
    [SerializeField] private bool _resetOrbitOnDisable = true;

    [Header("Attack Pose")]
    [SerializeField] private bool _snapDuringAttack = true;
    [SerializeField] private bool _disableBobDuringAttack = true;
    [SerializeField] private bool _freezeOrbitDuringAttack = true;

    [Header("Physics")]
    [SerializeField] private bool _disableColliderWhileFollowing = true;

    private Vector3 _followVelocity;

    private Quaternion _initialOrbitLocalRotation;
    private Quaternion _initialHoldPointLocalRotation;
    private bool _hasCachedOrbitRotation;
    private bool _hasCachedHoldPointRotation;

    private float _currentOrbitAngle;
    private float _orbitAngleVelocity;

    private Vector2 _attackLocalOffset;
    private float _attackLocalAngle;
    private bool _hasAttackPose;
    private Vector3 _attackAnchorPosition;
    private Quaternion _attackAnchorRotation;
    private bool _hasAttackAnchor;

    private RigidbodyType2D _cachedBodyType;
    private float _cachedGravityScale;
    private bool _cachedColliderEnabled;
    private bool _hasCachedPhysics;

    private bool _isFollowEnabled;
    private bool _isFollowLocked;

    public bool IsFollowEnabled => _isFollowEnabled;
    public bool IsFollowLocked => _isFollowLocked;
    public bool HasAttackPose => _hasAttackPose;
    public Transform MeleePivot => _meleeHoverPivot;
    public Transform MeleeOrbitRoot => _meleeOrbitRoot;
    public Transform MeleeHoverHoldPoint => _meleeHoverHoldPoint;
    public Vector3 CurrentTargetPosition { get; private set; }
    public Quaternion CurrentTargetRotation { get; private set; }
    public Vector2 CurrentForward => CurrentTargetRotation * Vector3.right;

    private void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();

        if (_collider == null)
            _collider = GetComponent<Collider2D>();

        CacheOrbitPose();
    }

    public void Initialize(PlayerController playerController, Rigidbody2D weaponRigidbody, Collider2D weaponCollider)
    {
        if (playerController != null)
            _playerController = playerController;

        if (weaponRigidbody != null)
            _rigidbody = weaponRigidbody;

        if (weaponCollider != null)
            _collider = weaponCollider;

        CacheOrbitPose();
    }

    private void LateUpdate()
    {
        if (_isFollowEnabled && (!_hasAttackPose || !_freezeOrbitDuringAttack))
            UpdateAimOrbit(false);

        TickFollow();
    }

    public void EnableFollow()
    {
        _isFollowEnabled = true;
        _isFollowLocked = false;
        _followVelocity = Vector3.zero;
        _orbitAngleVelocity = 0f;

        EnterFollowPhysics();
        CacheOrbitPose();
        UpdateAimOrbit(true);
        SnapToTargetPose();
    }

    public void DisableFollow()
    {
        _isFollowEnabled = false;
        _isFollowLocked = false;
        _followVelocity = Vector3.zero;
        _orbitAngleVelocity = 0f;
        ClearAttackPose();
        EndAttackAnchor();

        if (_resetOrbitOnDisable)
            ResetOrbitPose();

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

    public void SetAttackPose(Vector2 localOffset, float localAngle)
    {
        if (!_hasAttackPose)
            _followVelocity = Vector3.zero;

        _attackLocalOffset = localOffset;
        _attackLocalAngle = localAngle;
        _hasAttackPose = true;
    }

    public void BeginAttackAnchor()
    {
        if (_meleeHoverHoldPoint == null)
            return;

        _attackAnchorPosition = _meleeHoverHoldPoint.position;
        _attackAnchorRotation = _meleeHoverHoldPoint.rotation;
        _hasAttackAnchor = true;
    }

    public void EndAttackAnchor()
    {
        _hasAttackAnchor = false;
        _attackAnchorPosition = Vector3.zero;
        _attackAnchorRotation = Quaternion.identity;
    }

    public void ClearAttackPose()
    {
        _attackLocalOffset = Vector2.zero;
        _attackLocalAngle = 0f;
        _hasAttackPose = false;
        _followVelocity = Vector3.zero;
    }

    private void UpdateAimOrbit(bool immediate)
    {
        if (!_enableAimOrbit)
        {
            ApplyOrbitAngle(0f);
            return;
        }

        if (_playerController == null || _meleeOrbitRoot == null)
            return;

        CacheOrbitPose();

        Vector2 aimDirection = _playerController.AimDirection;
        if (aimDirection.sqrMagnitude <= 0.0001f)
            return;

        float targetAngle = Mathf.Clamp(aimDirection.normalized.y, -1f, 1f) * _maxOrbitAngle;

        if (_invertOrbitAngle)
            targetAngle = -targetAngle;

        if (immediate || _orbitSmoothTime <= 0f)
        {
            _currentOrbitAngle = targetAngle;
        }
        else
        {
            _currentOrbitAngle = Mathf.SmoothDampAngle(
                _currentOrbitAngle,
                targetAngle,
                ref _orbitAngleVelocity,
                _orbitSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
        }

        ApplyOrbitAngle(_currentOrbitAngle);
    }

    private void ApplyOrbitAngle(float angle)
    {
        if (_meleeOrbitRoot != null && _hasCachedOrbitRotation)
            _meleeOrbitRoot.localRotation = _initialOrbitLocalRotation * Quaternion.Euler(0f, 0f, angle);

        if (_meleeHoverHoldPoint == null || !_hasCachedHoldPointRotation)
            return;

        if (_counterRotateHoldPoint)
        {
            _meleeHoverHoldPoint.localRotation =
                _initialHoldPointLocalRotation * Quaternion.Euler(0f, 0f, -angle);
        }
        else
        {
            _meleeHoverHoldPoint.localRotation = _initialHoldPointLocalRotation;
        }
    }

    private void TickFollow()
    {
        if (!_isFollowEnabled || _isFollowLocked)
            return;

        if (_meleeHoverHoldPoint == null)
            return;

        CalculateTargetPose(out Vector3 targetPosition, out Quaternion targetRotation);

        bool snap = _snapFollow || (_hasAttackPose && _snapDuringAttack);

        if (snap)
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

        transform.rotation = targetRotation;

        CurrentTargetPosition = targetPosition;
        CurrentTargetRotation = targetRotation;

        StopPhysicsMotion();
    }

    private void SnapToTargetPose()
    {
        if (_meleeHoverHoldPoint == null)
            return;

        CalculateTargetPose(out Vector3 targetPosition, out Quaternion targetRotation);

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        CurrentTargetPosition = targetPosition;
        CurrentTargetRotation = targetRotation;

        StopPhysicsMotion();
    }

    private void CalculateTargetPose(out Vector3 targetPosition, out Quaternion targetRotation)
    {
        if (_hasAttackPose)
        {
            Vector3 anchorPosition = _hasAttackAnchor ? _attackAnchorPosition : _meleeHoverHoldPoint.position;
            Quaternion anchorRotation = _hasAttackAnchor ? _attackAnchorRotation : _meleeHoverHoldPoint.rotation;
            targetPosition = anchorPosition + (Vector3)_attackLocalOffset;

            if (!_disableBobDuringAttack)
                targetPosition += (Vector3)GetBobOffset();

            targetRotation = anchorRotation * Quaternion.Euler(0f, 0f, _attackLocalAngle);

            return;
        }

        targetPosition = _meleeHoverHoldPoint.position + (Vector3)GetBobOffset();
        targetRotation = _meleeHoverHoldPoint.rotation;
    }

    public bool TryEvaluateAttackWorldPose(Vector2 localOffset, float localAngle, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = transform.position;
        worldRotation = transform.rotation;

        if (_hasAttackAnchor)
        {
            worldPosition = _attackAnchorPosition + (Vector3)localOffset;
            worldRotation = _attackAnchorRotation * Quaternion.Euler(0f, 0f, localAngle);
            return true;
        }

        if (_meleeHoverHoldPoint == null)
            return false;

        worldPosition = _meleeHoverHoldPoint.position + (Vector3)localOffset;
        worldRotation = _meleeHoverHoldPoint.rotation * Quaternion.Euler(0f, 0f, localAngle);
        return true;
    }

    private float ResolveFacingSign()
    {
        if (_playerController == null)
            return 1f;

        return _playerController.FacingSign >= 0 ? 1f : -1f;
    }

    private void CacheOrbitPose()
    {
        if (_meleeOrbitRoot != null && !_hasCachedOrbitRotation)
        {
            _initialOrbitLocalRotation = _meleeOrbitRoot.localRotation;
            _hasCachedOrbitRotation = true;
        }

        if (_meleeHoverHoldPoint != null && !_hasCachedHoldPointRotation)
        {
            _initialHoldPointLocalRotation = _meleeHoverHoldPoint.localRotation;
            _hasCachedHoldPointRotation = true;
        }
    }

    private void ResetOrbitPose()
    {
        _currentOrbitAngle = 0f;
        _orbitAngleVelocity = 0f;

        if (_meleeOrbitRoot != null && _hasCachedOrbitRotation)
            _meleeOrbitRoot.localRotation = _initialOrbitLocalRotation;

        if (_meleeHoverHoldPoint != null && _hasCachedHoldPointRotation)
            _meleeHoverHoldPoint.localRotation = _initialHoldPointLocalRotation;
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

    private void StopPhysicsMotion()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
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
