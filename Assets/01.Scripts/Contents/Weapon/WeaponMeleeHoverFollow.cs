using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class WeaponMeleeHoverFollow : MonoBehaviour
{
    [FormerlySerializedAs("_meleePivot")]
    [SerializeField] private Transform _meleeHoverPivot;
    [FormerlySerializedAs("_meleeHoverAnchor")]
    [FormerlySerializedAs("_meleeHoldPoint")]
    [SerializeField] private Transform _meleeHoverHoldPoint;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Rigidbody2D _weaponRigidbody;
    [SerializeField] private Collider2D _weaponCollider;

    [Header("Hover Follow")]
    [SerializeField] private float _followSmoothTime = 0.08f;
    [SerializeField] private float _followMaxSpeed = 30f;

    [Header("Aim Pivot")]
    [SerializeField] private bool _rotatePivotToAim = true;
    [SerializeField] private float _aimRotationOffset = 0f;

    private Vector2 _followVelocity;
    private Vector3 _initialPivotLocalPosition;
    private Vector3 _initialPivotLocalScale;
    private Quaternion _initialHoldLocalRotation = Quaternion.identity;
    private bool _hasCachedInitialPivotLocalPosition;
    private bool _hasCachedInitialPivotLocalScale;
    private bool _hasCachedInitialHoldRotation;
    private bool _isFollowEnabled;
    private bool _isFollowLocked;

    public bool IsFollowEnabled => _isFollowEnabled;
    public bool IsFollowLocked => _isFollowLocked;
    public Transform MeleePivot => _meleeHoverPivot;
    public Transform MeleeHoverAnchor => _meleeHoverHoldPoint;
    public Transform MeleeHoverPivot => _meleeHoverPivot;
    public Transform MeleeHoverHoldPoint => _meleeHoverHoldPoint;
    public PlayerController PlayerController => _playerController;

    private void Awake()
    {
        _weaponRigidbody = _weaponRigidbody != null ? _weaponRigidbody : GetComponent<Rigidbody2D>();
        _weaponCollider = _weaponCollider != null ? _weaponCollider : GetComponent<Collider2D>();
        CacheInitialPivotLocalPosition();
        CacheInitialPivotLocalScale();
        CacheInitialHoldRotation();
    }

    public void Initialize(PlayerController playerController, Rigidbody2D weaponRigidbody, Collider2D weaponCollider)
    {
        _playerController = playerController != null ? playerController : _playerController;
        _weaponRigidbody = weaponRigidbody != null ? weaponRigidbody : GetComponent<Rigidbody2D>();
        _weaponCollider = weaponCollider != null ? weaponCollider : GetComponent<Collider2D>();
        CacheInitialPivotLocalPosition();
        CacheInitialPivotLocalScale();
        CacheInitialHoldRotation();
    }

    private void FixedUpdate()
    {
        TickHoverFollow();
    }

    private void LateUpdate()
    {
        ApplyPivotHorizontalFlip();

        if (!_isFollowEnabled || _isFollowLocked)
            return;

        UpdatePivotByAim();
    }

    public void EnableFollow()
    {
        _isFollowEnabled = true;
        _isFollowLocked = false;
        _followVelocity = Vector2.zero;
        CacheInitialHoldRotation();
        UpdatePivotByAim();
    }

    public void DisableFollow()
    {
        _isFollowEnabled = false;
        _isFollowLocked = false;
        _followVelocity = Vector2.zero;
    }

    public void Attach() => EnableFollow();
    public void Detach() => DisableFollow();

    public void SetFollowLocked(bool isLocked)
    {
        _isFollowLocked = isLocked;
        if (!isLocked)
            _followVelocity = Vector2.zero;
    }

    public void UpdatePivotByAim()
    {
        if (!_isFollowEnabled || _isFollowLocked)
            return;
        if (_meleeHoverPivot == null || _playerController == null)
            return;

        ApplyPivotHorizontalFlip();

        if (!_rotatePivotToAim)
        {
            KeepHoldPointInitialTilt();
            return;
        }

        Vector2 aimDirection = ResolveAimDirection();
        if (aimDirection.sqrMagnitude <= 0.0001f)
            return;

        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        float mirroredAngle = aimAngle;
        if (_playerController.FacingSign < 0)
            mirroredAngle = 180f - aimAngle;
        _meleeHoverPivot.rotation = Quaternion.Euler(0f, 0f, mirroredAngle + _aimRotationOffset);

        KeepHoldPointInitialTilt();
    }

    public void TickHoverFollow()
    {
        if (!_isFollowEnabled || _isFollowLocked)
            return;
        if (_weaponRigidbody == null || _meleeHoverHoldPoint == null)
            return;

        UpdatePivotByAim();

        Vector2 nextPosition = Vector2.SmoothDamp(
            _weaponRigidbody.position,
            _meleeHoverHoldPoint.position,
            ref _followVelocity,
            Mathf.Max(0.0001f, _followSmoothTime),
            Mathf.Max(0.001f, _followMaxSpeed),
            Time.fixedDeltaTime);

        _weaponRigidbody.MovePosition(nextPosition);
        _weaponRigidbody.MoveRotation(_meleeHoverHoldPoint.rotation.eulerAngles.z);
    }

    private void KeepHoldPointInitialTilt()
    {
        if (_meleeHoverHoldPoint == null)
            return;

        CacheInitialHoldRotation();
        if (!_hasCachedInitialHoldRotation)
            return;

        _meleeHoverHoldPoint.localRotation = _initialHoldLocalRotation;
    }

    private Vector2 ResolveAimDirection()
    {
        if (_playerController == null) return Vector2.right;
        Vector2 aimDirection = _playerController.AimDirection;
        if (aimDirection.sqrMagnitude <= 0.0001f) return Vector2.right;
        return aimDirection.normalized;
    }

    private void ApplyPivotHorizontalFlip()
    {
        if (_meleeHoverPivot == null)
            return;

        CacheInitialPivotLocalPosition();
        if (!_hasCachedInitialPivotLocalPosition)
            return;

        int facingSign = _playerController != null ? _playerController.FacingSign : 1;
        float pivotX = facingSign >= 0
            ? Mathf.Abs(_initialPivotLocalPosition.x)
            : -Mathf.Abs(_initialPivotLocalPosition.x);
        _meleeHoverPivot.localPosition = new Vector3(
            pivotX,
            _initialPivotLocalPosition.y,
            _initialPivotLocalPosition.z);

        CacheInitialPivotLocalScale();
        if (!_hasCachedInitialPivotLocalScale)
            return;

        Vector3 nextScale = _initialPivotLocalScale;
        nextScale.x = facingSign >= 0
            ? Mathf.Abs(_initialPivotLocalScale.x)
            : -Mathf.Abs(_initialPivotLocalScale.x);
        _meleeHoverPivot.localScale = nextScale;
    }

    private void CacheInitialPivotLocalPosition()
    {
        if (_hasCachedInitialPivotLocalPosition || _meleeHoverPivot == null)
            return;

        _initialPivotLocalPosition = _meleeHoverPivot.localPosition;
        _hasCachedInitialPivotLocalPosition = true;
    }

    private void CacheInitialPivotLocalScale()
    {
        if (_hasCachedInitialPivotLocalScale || _meleeHoverPivot == null)
            return;

        _initialPivotLocalScale = _meleeHoverPivot.localScale;
        _hasCachedInitialPivotLocalScale = true;
    }

    private void CacheInitialHoldRotation()
    {
        if (_hasCachedInitialHoldRotation || _meleeHoverHoldPoint == null)
            return;

        _initialHoldLocalRotation = _meleeHoverHoldPoint.localRotation;
        _hasCachedInitialHoldRotation = true;
    }
}
