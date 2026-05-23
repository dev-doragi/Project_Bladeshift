using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class WeaponMeleeAttachment : MonoBehaviour
{
    [SerializeField] private Transform _meleePivot;
    [SerializeField] private Transform _meleeHoldPoint;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Rigidbody2D _weaponRigidbody;
    [SerializeField] private Collider2D _weaponCollider;

    private Vector3 _originalPivotLocalPosition;
    private Vector3 _originalPivotLocalScale = Vector3.one;
    private bool _hasOriginalPivotValues;
    private bool _isAttached;

    public bool IsAttached => _isAttached;
    public Transform MeleePivot => _meleePivot;
    public Transform MeleeHoldPoint => _meleeHoldPoint;
    public PlayerController PlayerController => _playerController;

    private void Awake()
    {
        _weaponRigidbody = _weaponRigidbody != null ? _weaponRigidbody : GetComponent<Rigidbody2D>();
        _weaponCollider = _weaponCollider != null ? _weaponCollider : GetComponent<Collider2D>();
        CachePivotDefaults();
    }

    public void Initialize(PlayerController playerController, Rigidbody2D weaponRigidbody, Collider2D weaponCollider)
    {
        _playerController = playerController != null ? playerController : _playerController;
        _weaponRigidbody = weaponRigidbody != null ? weaponRigidbody : GetComponent<Rigidbody2D>();
        _weaponCollider = weaponCollider != null ? weaponCollider : GetComponent<Collider2D>();
        CachePivotDefaults();
    }

    private void LateUpdate()
    {
        if (!_isAttached) return;

        UpdateFacing();
        SyncToHoldPoint();
    }

    public void Attach()
    {
        Transform holdPoint = GetHoldPoint();
        if (holdPoint == null) return;

        _isAttached = true;
        UpdateFacing();

        transform.SetParent(holdPoint, false);
        SyncToHoldPoint();
    }

    public void Detach()
    {
        if (!_isAttached) return;

        _isAttached = false;
        transform.SetParent(null, true);
    }

    private void SyncToHoldPoint()
    {
        Transform holdPoint = GetHoldPoint();
        if (holdPoint == null) return;

        if (transform.parent == holdPoint)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return;
        }

        transform.position = holdPoint.position;
        transform.rotation = holdPoint.rotation;
    }

    private void UpdateFacing()
    {
        if (_meleePivot == null || _playerController == null) return;
        CachePivotDefaults();

        int facingSign = _playerController.FacingSign >= 0 ? 1 : -1;

        Vector3 position = _originalPivotLocalPosition;
        position.x = Mathf.Abs(position.x) * facingSign;
        _meleePivot.localPosition = position;

        Vector3 scale = _originalPivotLocalScale;
        scale.x = Mathf.Abs(scale.x) * facingSign;
        _meleePivot.localScale = scale;
    }

    private Transform GetHoldPoint()
    {
        if (_meleeHoldPoint != null) return _meleeHoldPoint;
        return _meleePivot;
    }

    private void CachePivotDefaults()
    {
        if (_hasOriginalPivotValues || _meleePivot == null) return;

        _originalPivotLocalPosition = _meleePivot.localPosition;
        _originalPivotLocalScale = _meleePivot.localScale;
        _hasOriginalPivotValues = true;
    }
}
