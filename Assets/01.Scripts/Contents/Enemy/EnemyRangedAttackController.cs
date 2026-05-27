using System;
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
    private EnemyController _enemyController;
    private Transform _target;
    private float _cooldownTimer;
    private float _initialAttackDelayTimer;
    private bool _isTargetInAttackRange;
    private bool _hasWaitedInitialAttackDelay;

    public event Action AttackPerformed;

    private void Awake()
    {
        _directAttacker = GetComponent<EnemyDirectAttacker>();
        _enemyBase = GetComponent<EnemyBase>();
        _enemyController = GetComponent<EnemyController>();

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
        {
            _target = _targetOverride;
            return;
        }

        if (PlayerController.ActivePlayer != null)
            _target = PlayerController.ActivePlayer.transform;
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

        bool isTargetInRange = CanAttackCurrentTarget() && IsTargetInRange();
        UpdateInitialAttackDelay(isTargetInRange);

        if (!isTargetInRange)
            return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f)
            return;

        if (!_hasWaitedInitialAttackDelay)
            return;

        if (_directAttacker.TryPerformAttack(_target, _attackData))
        {
            _cooldownTimer = _attackData.AttackCooldown;
            AttackPerformed?.Invoke();
        }
    }

    private bool IsTargetInRange()
    {
        float range = _attackData.AttackRange;
        float sqrDistance = (_target.position - transform.position).sqrMagnitude;
        return sqrDistance <= range * range;
    }

    private bool CanAttackCurrentTarget()
    {
        return _enemyController == null || _enemyController.IsTargetDetected;
    }

    private void UpdateInitialAttackDelay(bool isTargetInRange)
    {
        if (!isTargetInRange)
        {
            _isTargetInAttackRange = false;
            _hasWaitedInitialAttackDelay = false;
            _initialAttackDelayTimer = 0f;
            return;
        }

        if (!_isTargetInAttackRange)
        {
            _isTargetInAttackRange = true;
            _hasWaitedInitialAttackDelay = _attackData.InitialAttackDelay <= 0f;
            _initialAttackDelayTimer = _attackData.InitialAttackDelay;
        }

        if (_hasWaitedInitialAttackDelay)
            return;

        _initialAttackDelayTimer -= Time.deltaTime;
        if (_initialAttackDelayTimer <= 0f)
            _hasWaitedInitialAttackDelay = true;
    }

    private void HandlePlayerSpawned(PlayerSpawnedEvent evt)
    {
        if (evt.Player == null)
            return;

        _target = evt.Player.transform;
    }
}
