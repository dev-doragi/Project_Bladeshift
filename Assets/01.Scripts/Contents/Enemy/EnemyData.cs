using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "BladeShift/Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _enemyId = "Enemy";
    [SerializeField] private EnemyCategory _category = EnemyCategory.Normal;

    [Header("Stats")]
    [SerializeField] private float _maxHealth = 50f;
    [SerializeField] private float _weight = 1f;

    [Header("Capture")]
    [SerializeField] private bool _canBeCaptured = true;
    [SerializeField] private float _captureWeight = 1f;

    [Header("Execution")]
    [SerializeField] private bool _canBeExecuted = true;

    [Header("Groggy")]
    [SerializeField] private bool _usesGroggy = false;
    [SerializeField] private GroggyTriggerMode _groggyTriggerMode = GroggyTriggerMode.HealthThreshold;
    [SerializeField, Range(0.01f, 1f)] private float _groggyThresholdRatio = 0.1f;
    [SerializeField, Min(1f)] private float _maxGroggyGauge = 100f;
    [SerializeField] private bool _resetGroggyGaugeOnEnter = true;
    [SerializeField] private bool _resetGroggyGaugeOnExit = true;
    [SerializeField, Min(0f)] private float _groggyInvulnerableDuration = 0.75f;
    [SerializeField, Min(0f)] private float _groggyDuration = 3f;
    [SerializeField] private bool _usesGroggyRecovery = true;
    [SerializeField, Range(0.01f, 1f)] private float _groggyRecoveryTargetHealthRatio = 0.5f;
    [SerializeField, Min(0f)] private float _groggyRecoveryDuration = 5f;
    [SerializeField] private bool _canDieFromBasicAttackWhileGroggy = true;
    [SerializeField] private GroggyRightClickActionType _groggyRightClickAction = GroggyRightClickActionType.None;

    [Header("Piercing Attack")]
    [SerializeField] private PiercingAttackPolicy _piercingAttackPolicy = PiercingAttackPolicy.StickToEnemy;

    [Header("Embedded Attack")]
    [SerializeField] private bool _usesEmbeddedAttackMechanic = false;
    [SerializeField, Min(0f)] private float _embeddedTearOutDamage = 50f;
    [SerializeField, Min(0f)] private float _embeddedAttackDamage = 100f;
    [SerializeField, Min(0f)] private float _embeddedAttackRange = 2f;

    public string EnemyId => _enemyId;
    public EnemyCategory Category => _category;
    public float MaxHealth => _maxHealth;
    public float Weight => _weight;
    public bool CanBeCaptured => _canBeCaptured;
    public float CaptureWeight => _captureWeight;
    public bool CanBeExecuted => _canBeExecuted;
    public bool UsesGroggy => _usesGroggy;
    public GroggyTriggerMode GroggyTriggerMode => _groggyTriggerMode;
    public float GroggyThresholdRatio => _groggyThresholdRatio;
    public float MaxGroggyGauge => _maxGroggyGauge;
    public bool ResetGroggyGaugeOnEnter => _resetGroggyGaugeOnEnter;
    public bool ResetGroggyGaugeOnExit => _resetGroggyGaugeOnExit;
    public float GroggyInvulnerableDuration => _groggyInvulnerableDuration;
    public float GroggyDuration => _groggyDuration;
    public bool UsesGroggyRecovery => _usesGroggyRecovery;
    public float GroggyRecoveryTargetHealthRatio => _groggyRecoveryTargetHealthRatio;
    public float GroggyRecoveryDuration => _groggyRecoveryDuration;
    public bool CanDieFromBasicAttackWhileGroggy => _canDieFromBasicAttackWhileGroggy;
    public GroggyRightClickActionType GroggyRightClickAction => _groggyRightClickAction;
    public PiercingAttackPolicy PiercingAttackPolicy => _piercingAttackPolicy;
    public bool UsesEmbeddedAttackMechanic => _usesEmbeddedAttackMechanic;
    public float EmbeddedTearOutDamage => _embeddedTearOutDamage;
    public float EmbeddedAttackDamage => _embeddedAttackDamage;
    public float EmbeddedAttackRange => _embeddedAttackRange;

    public float GroggyThresholdHealth => _maxHealth * _groggyThresholdRatio;
    public float GroggyRecoveryTargetHealth => _maxHealth * _groggyRecoveryTargetHealthRatio;

    public bool ShouldPiercingAttackStick(bool isGroggy)
    {
        switch (_piercingAttackPolicy)
        {
            case PiercingAttackPolicy.StickToEnemy:
                return true;
            case PiercingAttackPolicy.PassThroughToWall:
                return false;
            case PiercingAttackPolicy.StickOnlyWhileGroggy:
                return isGroggy;
            default:
                return false;
        }
    }

    public bool ShouldPierceStick(bool isGroggy)
    {
        return ShouldPiercingAttackStick(isGroggy);
    }

    public bool ShouldPiercePassThrough(bool isGroggy)
    {
        return !ShouldPierceStick(isGroggy);
    }

    public bool CanBeCapturedByPierce(bool isGroggy)
    {
        if (!_canBeCaptured) return false;
        return ShouldPierceStick(isGroggy);
    }

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1f, _maxHealth);
        _weight = Mathf.Max(0f, _weight);
        _captureWeight = Mathf.Max(0f, _captureWeight);
        _maxGroggyGauge = Mathf.Max(1f, _maxGroggyGauge);
        _groggyInvulnerableDuration = Mathf.Clamp(_groggyInvulnerableDuration, 0.5f, 1f);
        _groggyDuration = Mathf.Max(0f, _groggyDuration);
        _groggyRecoveryDuration = Mathf.Max(0f, _groggyRecoveryDuration);
        _embeddedTearOutDamage = Mathf.Max(0f, _embeddedTearOutDamage);
        _embeddedAttackDamage = Mathf.Max(0f, _embeddedAttackDamage);
        _embeddedAttackRange = Mathf.Max(0f, _embeddedAttackRange);
    }
}
