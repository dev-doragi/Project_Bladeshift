using UnityEngine;

[RequireComponent(typeof(EnemyDirectAttacker))]
public class EnemyRangedAttackController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyProjectileAttackData _attackData;

    [Header("Target")]
    [SerializeField] private Transform _targetOverride;

    private EnemyDirectAttacker _directAttacker;
    private EnemyBase _enemyBase;
    private Transform _target;
    private float _cooldownTimer;

    private void Awake()
    {
        _directAttacker = GetComponent<EnemyDirectAttacker>();
        _enemyBase = GetComponent<EnemyBase>();

        if (_enemyBase == null)
            Debug.LogError("[EnemyRangedAttackController] EnemyBase component is missing.", this);
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);
    }

    private void Start()
    {
        if (_targetOverride != null)
            _target = _targetOverride;
    }

    private void Update()
    {
        if (_enemyBase == null || _enemyBase.IsDead || _enemyBase.IsCaptured)
            return;

        if (_attackData == null)
        {
            Debug.LogError("[EnemyRangedAttackController] attackData is null.", this);
            enabled = false;
            return;
        }

        if (_target == null)
            return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f)
            return;

        if (!IsTargetInRange())
            return;

        if (_directAttacker.TryPerformAttack(_target, _attackData))
            _cooldownTimer = _attackData.AttackCooldown;
    }

    private bool IsTargetInRange()
    {
        float range = _attackData.AttackRange;
        float sqrDistance = (_target.position - transform.position).sqrMagnitude;
        return sqrDistance <= range * range;
    }

    private void HandlePlayerSpawned(PlayerSpawnedEvent evt)
    {
        if (evt.Player == null)
            return;

        _target = evt.Player.transform;
    }
}
