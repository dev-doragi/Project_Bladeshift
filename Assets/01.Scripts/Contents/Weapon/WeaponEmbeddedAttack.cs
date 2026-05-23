using UnityEngine;

public class WeaponEmbeddedAttack : MonoBehaviour
{
    [Header("Pin Tuning")]
    [SerializeField, Min(0f)] private float _embeddedDepth = 0.35f;
    [SerializeField] private float _embeddedRotationOffset = 0f;

    [Header("Close Attack Motion")]
    [SerializeField] private float _embeddedFinisherRadiusMultiplier = 1.8f;
    [SerializeField] private float _embeddedFinisherOrbitDuration = 0.35f;
    [SerializeField] private float _embeddedFinisherHitStopDuration = 0.2f;
    [SerializeField] private ShakeIntensity _embeddedFinisherShakeIntensity = ShakeIntensity.Strong;

    private WeaponController _controller;
    private EnemyBase _embeddedEnemy;
    private bool _isEmbeddedFinisherRunning;

    public bool HasEmbeddedEnemy => _embeddedEnemy != null;
    public EnemyBase EmbeddedEnemy => _embeddedEnemy;

    public void Initialize(WeaponController controller)
    {
        _controller = controller != null ? controller : GetComponent<WeaponController>();
    }

    public bool BeginEmbeddedPin(EnemyBase enemy)
    {
        if (enemy == null || !enemy.IsGroggy || !enemy.UsesEmbeddedAttackMechanic)
            return false;

        ClearEmbeddedTarget();
        _embeddedEnemy = enemy;
        _embeddedEnemy.GroggyStateExited += OnEmbeddedEnemyGroggyExited;

        transform.SetParent(enemy.transform, true);
        SnapWeaponIntoEnemy(enemy);
        return true;
    }

    public bool TryHandlePinnedAction()
    {
        if (_controller == null || _embeddedEnemy == null)
            return false;

        if (_embeddedEnemy.IsDead)
        {
            ReturnToPlayer();
            return true;
        }

        if (_isEmbeddedFinisherRunning)
            return true;

        Transform playerTransform = _controller.PlayerTransform;
        if (playerTransform == null)
            return true;

        float distanceToEnemy = Vector2.Distance(playerTransform.position, _embeddedEnemy.transform.position);
        if (distanceToEnemy <= _embeddedEnemy.EmbeddedAttackRange)
        {
            TryExecuteCloseEmbeddedAttack();
            return true;
        }

        if (_controller.Sensor != null && _controller.Sensor.IsPlayerInRange(transform.position))
        {
            CompleteEmbeddedInteraction(_embeddedEnemy.EmbeddedTearOutDamage, WeaponAttackKind.EmbeddedTearOut);
            ReturnToPlayer();
            return true;
        }

        return true;
    }

    public void ReleaseWithoutDamage()
    {
        _isEmbeddedFinisherRunning = false;
        ClearEmbeddedTarget();
        transform.SetParent(null, true);
    }

    private void SnapWeaponIntoEnemy(EnemyBase enemy)
    {
        Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
        if (enemyCollider == null)
            return;

        Vector2 weaponPosition = transform.position;
        Vector2 enemyCenter = enemyCollider.bounds.center;
        Vector2 inwardDirection = enemyCenter - weaponPosition;
        if (inwardDirection.sqrMagnitude <= 0.0001f)
            inwardDirection = enemy.transform.right;

        inwardDirection.Normalize();
        Vector2 surfacePoint = enemyCollider.ClosestPoint(weaponPosition);
        Vector2 embeddedPoint = surfacePoint + (inwardDirection * _embeddedDepth);
        transform.position = embeddedPoint;

        float angle = Mathf.Atan2(inwardDirection.y, inwardDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + _embeddedRotationOffset);
    }

    private bool TryExecuteCloseEmbeddedAttack()
    {
        if (_controller == null || _embeddedEnemy == null)
            return false;

        WeaponSensor sensor = _controller.Sensor;
        WeaponCombat combat = _controller.Combat;
        WeaponMovement movement = _controller.Movement;
        if (sensor == null || combat == null || movement == null)
            return false;

        _isEmbeddedFinisherRunning = true;
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _embeddedFinisherHitStopDuration });
        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _embeddedFinisherShakeIntensity });

        Vector2 pivot = sensor.GetMouseWorldPosition();
        float radius = Vector2.Distance(pivot, transform.position);
        radius = Mathf.Max(radius, combat.SlashRadius * Mathf.Max(0.1f, _embeddedFinisherRadiusMultiplier));
        float damage = _embeddedEnemy.EmbeddedAttackDamage;

        movement.ExecuteOrbitFinisher(
            pivot,
            radius,
            Mathf.Max(0.01f, _embeddedFinisherOrbitDuration),
            () => CompleteEmbeddedInteraction(damage, WeaponAttackKind.EmbeddedAttack),
            () =>
            {
                _isEmbeddedFinisherRunning = false;
                ReturnToPlayer();
            });

        return true;
    }

    private void CompleteEmbeddedInteraction(float damage, WeaponAttackKind attackKind)
    {
        EnemyBase target = _embeddedEnemy;
        if (target == null)
            return;

        ApplyEmbeddedDamage(target, damage, attackKind);

        ClearEmbeddedTarget();
        if (!target.IsDead)
            target.ForceExitGroggy(false);
    }

    private void ApplyEmbeddedDamage(EnemyBase target, float damage, WeaponAttackKind attackKind)
    {
        if (target == null || target.IsDead)
            return;

        Vector2 knockbackDirection = _controller != null && _controller.PlayerTransform != null
            ? ((Vector2)target.transform.position - (Vector2)_controller.PlayerTransform.position).normalized
            : Vector2.zero;

        target.TakeDamage(new DamageData
        {
            Damage = damage,
            AttackerTeam = TeamType.Player,
            HitPoint = transform.position,
            KnockbackForce = knockbackDirection * 4f,
            IsPiercing = false,
            AttackKind = attackKind
        });
    }

    private void OnEmbeddedEnemyGroggyExited(EnemyBase enemy)
    {
        if (enemy != _embeddedEnemy)
            return;

        ReturnToPlayer();
    }

    private void ReturnToPlayer()
    {
        _isEmbeddedFinisherRunning = false;
        ClearEmbeddedTarget();
        transform.SetParent(null, true);

        if (_controller == null)
            return;

        _controller.StateMachine?.ClearPinSource();
        _controller.ChangeState(WeaponState.Returning);
        _controller.Movement?.ExecuteReturn(
            () => _controller.Sensor != null
                ? _controller.Sensor.GetClampedTargetPosition(_controller.WallAndEnvironmentLayer)
                : (Vector2)_controller.transform.position,
            _controller.ControlRadius,
            (currentPos, mousePos) => _controller.Sensor != null && _controller.Sensor.IsMouseHovering(currentPos, mousePos),
            _ => _controller.ChangeState(WeaponState.Controlled));
    }

    private void ClearEmbeddedTarget()
    {
        if (_embeddedEnemy != null)
            _embeddedEnemy.GroggyStateExited -= OnEmbeddedEnemyGroggyExited;

        _embeddedEnemy = null;
    }

    private void OnDisable()
    {
        ClearEmbeddedTarget();
    }
}
