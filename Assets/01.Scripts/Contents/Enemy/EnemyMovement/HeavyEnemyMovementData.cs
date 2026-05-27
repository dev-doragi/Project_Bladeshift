using UnityEngine;

[CreateAssetMenu(fileName = "HeavyEnemyMovementData", menuName = "BladeShift/Enemy/Movement/Heavy")]
public class HeavyEnemyMovementData : GroundEnemyMovementData
{
    [Header("Charge")]
    [SerializeField, Min(0f)] private float _chargeCooldown = 4f;
    [SerializeField, Min(0.01f)] private float _chargeSpeed = 16f;
    [SerializeField, Min(0.01f)] private float _chargeDistance = 8f;
    [SerializeField] private Vector2 _chargeStartDistanceRange = new(0f, 8f);
    [SerializeField, Min(0f)] private float _chargeWarmupDuration = 0.75f;
    [SerializeField, Min(0.01f)] private float _preChargeDropSpeed = 24f;
    [SerializeField, Min(0.01f)] private float _preChargeGroundCheckDistance = 0.15f;
    [SerializeField] private ShakeIntensity _preChargeLandingShakeIntensity = ShakeIntensity.Medium;

    [Header("Charge Warning")]
    [SerializeField, Min(0.01f)] private float _chargeWarningWidth = 0.45f;
    [SerializeField, Min(0f)] private float _chargeWarningBlinkSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float _chargeWarningMaxAlpha = 0.58f;
    [SerializeField, Range(0f, 1f)] private float _chargeWarningMinAlpha = 0.12f;

    [Header("Charge Collision")]
    [SerializeField, Min(0f)] private float _platformHitKnockback = 12f;
    [SerializeField] private LayerMask _chargeBlockerLayer;

    [Header("Charge After Image")]
    [SerializeField] private Color _chargeAfterImageColor = new(1f, 1f, 1f, 0.5f);

    public float ChargeCooldown => _chargeCooldown;
    public float ChargeSpeed => _chargeSpeed;
    public float ChargeDistance => _chargeDistance;
    public Vector2 ChargeStartDistanceRange => _chargeStartDistanceRange;
    public float ChargeWarmupDuration => _chargeWarmupDuration;
    public float PreChargeDropSpeed => _preChargeDropSpeed;
    public float PreChargeGroundCheckDistance => _preChargeGroundCheckDistance;
    public ShakeIntensity PreChargeLandingShakeIntensity => _preChargeLandingShakeIntensity;
    public float ChargeWarningWidth => _chargeWarningWidth;
    public float ChargeWarningBlinkSpeed => _chargeWarningBlinkSpeed;
    public float ChargeWarningMaxAlpha => _chargeWarningMaxAlpha;
    public float ChargeWarningMinAlpha => _chargeWarningMinAlpha;
    public float PlatformHitKnockback => _platformHitKnockback;
    public LayerMask ChargeBlockerLayer => _chargeBlockerLayer;
    public Color ChargeAfterImageColor => _chargeAfterImageColor;

    protected override void OnValidate()
    {
        base.OnValidate();

        _chargeCooldown = Mathf.Max(0f, _chargeCooldown);
        _chargeSpeed = Mathf.Max(0.01f, _chargeSpeed);
        _chargeDistance = Mathf.Max(0.01f, _chargeDistance);
        _chargeStartDistanceRange.x = Mathf.Max(0f, _chargeStartDistanceRange.x);
        _chargeStartDistanceRange.y = Mathf.Max(_chargeStartDistanceRange.x, _chargeStartDistanceRange.y);
        _chargeWarmupDuration = Mathf.Max(0f, _chargeWarmupDuration);
        _preChargeDropSpeed = Mathf.Max(0.01f, _preChargeDropSpeed);
        _preChargeGroundCheckDistance = Mathf.Max(0.01f, _preChargeGroundCheckDistance);
        _chargeWarningWidth = Mathf.Max(0.01f, _chargeWarningWidth);
        _chargeWarningBlinkSpeed = Mathf.Max(0f, _chargeWarningBlinkSpeed);
        _platformHitKnockback = Mathf.Max(0f, _platformHitKnockback);
    }
}
