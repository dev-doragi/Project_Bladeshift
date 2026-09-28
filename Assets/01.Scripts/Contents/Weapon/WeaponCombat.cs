using System.Collections.Generic;
using UnityEngine;

public class WeaponCombat : MonoBehaviour
{
    [Header("Mouse Swing")]
    [SerializeField] private float _minimumSwingSpeed = 6f;
    [SerializeField] private float _maximumSwingSpeed = 35f;
    [SerializeField] private float _minimumSwingDamage = 5f;
    [SerializeField] private float _maximumSwingDamage = 20f;
    [SerializeField] private float _minimumSwingGroggyDamage = 8f;
    [SerializeField] private float _maximumSwingGroggyDamage = 25f;
    [SerializeField] private float _swingHitCooldown = 0.25f;
    [SerializeField, Min(0f)] private float _minimumDroneAttackDistance = 1f;

    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _projectileLayer;
    [SerializeField] private float _knockbackPower = 15f;
    [SerializeField] private float _slashDamage = 15f;
    [SerializeField] private float _spinSlashGroggyDamage = 3f;
    [SerializeField] private float _slashRadius = 3.5f;
    [SerializeField] private float _tickDamageInterval = 0.2f;
    [SerializeField] private float _pinDamage = 30f;
    [SerializeField] private float _thrustPierceGroggyDamage = 20f;
    [SerializeField] private float _spinSpeed = 720f;
    [SerializeField] private float _pinSpeed = 24f;

    [Header("Finisher Wall Correction")]
    [SerializeField] private float _finisherWallProbeDistance = 3f;
    [SerializeField] private float _finisherWallSkinWidth = 0.25f;
    [SerializeField, Range(0.05f, 1f)] private float _finisherBlockedForceRatio = 0.2f;

    private float _lastTickTime;
    private readonly Dictionary<IDamageable, float> _lastSwingHitTimes = new Dictionary<IDamageable, float>();
    private readonly HashSet<EnemyDirectProjectile> _deflectedProjectiles = new HashSet<EnemyDirectProjectile>();
    private BoxCollider2D _bladeCollider;
    private WeaponController _controller;
    private Vector2 _droneAttackSegmentStart;

    private void Awake()
    {
        _bladeCollider = GetComponent<BoxCollider2D>();
        _controller = GetComponent<WeaponController>();
    }

    public void ResetDroneAttackSegment(Vector2 startPosition)
    {
        _droneAttackSegmentStart = startPosition;
    }

    public void ProcessMouseSwing(Vector2 start, Vector2 end, float deltaTime)
    {
        if (_controller == null || _bladeCollider == null || deltaTime <= 0f)
            return;

        bool isControlled = _controller.CurrentState == WeaponState.Controlled;
        bool isCombat = _controller.CurrentState == WeaponState.Slashing;
        bool isCarryingEnemy = _controller.CurrentState == WeaponState.Pinned &&
                               _controller.Capture != null && _controller.Capture.HasCapturedEnemy;
        if (_controller.CurrentMode != WeaponMode.Remote)
            return;

        if (_controller.UseDroneRemoteControl && !isCombat)
            return;

        if (!_controller.UseDroneRemoteControl && !isControlled && !isCarryingEnemy)
            return;

        Vector2 movement = end - start;
        float distance = movement.magnitude;
        float speed = distance / deltaTime;
        if (speed < _minimumSwingSpeed || distance <= 0.0001f)
            return;

        float speedRange = Mathf.Max(0.01f, _maximumSwingSpeed - _minimumSwingSpeed);
        float speedRatio = Mathf.Clamp01((speed - _minimumSwingSpeed) / speedRange);
        float damage = Mathf.Lerp(_minimumSwingDamage, _maximumSwingDamage, speedRatio);
        float groggyDamage = Mathf.Lerp(_minimumSwingGroggyDamage, _maximumSwingGroggyDamage, speedRatio);
        Vector2 direction = movement / distance;
        Vector3 scale = transform.lossyScale;
        Vector2 boxSize = new Vector2(
            Mathf.Abs(_bladeCollider.size.x * scale.x),
            Mathf.Abs(_bladeCollider.size.y * scale.y));
        Vector2 castStart = start + (Vector2)transform.TransformVector(_bladeCollider.offset);
        int targetLayers = _enemyLayer.value | _projectileLayer.value;
        float bladeAngle = transform.eulerAngles.z;
        if (_controller.UseDroneRemoteControl)
            bladeAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            castStart, boxSize, bladeAngle, direction, distance, targetLayers);

        _deflectedProjectiles.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if ((_projectileLayer.value & (1 << hitCollider.gameObject.layer)) != 0)
            {
                EnemyDirectProjectile projectile = hitCollider.GetComponentInParent<EnemyDirectProjectile>();
                if (projectile != null && _deflectedProjectiles.Add(projectile))
                    projectile.Deflect(direction);
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.Team != TeamType.Enemy || damageable.IsDead)
                continue;

            EnemyBase enemy = hitCollider.GetComponentInParent<EnemyBase>();
            if (enemy != null && enemy.IsCaptured)
                continue;

            if (_controller.UseDroneRemoteControl)
            {
                Vector2 hitPosition = start + direction * hits[i].distance;
                if (Vector2.Distance(_droneAttackSegmentStart, hitPosition) < _minimumDroneAttackDistance)
                    continue;
            }

            if (_lastSwingHitTimes.TryGetValue(damageable, out float lastHitTime) &&
                Time.time < lastHitTime + _swingHitCooldown)
                continue;

            _lastSwingHitTimes[damageable] = Time.time;

            damageable.TakeDamage(new DamageData
            {
                Damage = damage,
                GroggyDamage = groggyDamage,
                AttackerTeam = TeamType.Player,
                HitPoint = hitCollider.ClosestPoint(hits[i].point),
                KnockbackForce = direction * _knockbackPower,
                IsPiercing = false,
                AttackKind = WeaponAttackKind.None
            });

        }
    }

    public float SlashRadius => _slashRadius;
    public float SpinSpeed => _spinSpeed;
    public float PinSpeed => _pinSpeed;
    public LayerMask EnemyLayer => _enemyLayer;

    public void PerformSlashDamage(Vector3 position, float radius, float damage, float angleZ)
    {
        Vector2 boxSize = new Vector2(radius * 2.5f, radius * 1.5f);
        Collider2D[] targets = Physics2D.OverlapBoxAll(position, boxSize, angleZ, _enemyLayer);
        foreach (var col in targets)
        {
            if (col.TryGetComponent<IDamageable>(out var damageable))
            {
                Vector2 hitPoint = col.ClosestPoint(position);
                Vector2 knockbackDirection = ((Vector2)col.transform.position - (Vector2)position).normalized;
                damageable.TakeDamage(new DamageData
                {
                    Damage = damage,
                    GroggyDamage = _spinSlashGroggyDamage,
                    AttackerTeam = TeamType.Player,
                    HitPoint = hitPoint,
                    KnockbackForce = knockbackDirection * _knockbackPower,
                    IsPiercing = false,
                    AttackKind = WeaponAttackKind.SpinSlash
                });
            }
        }
    }

    public void TryTickSpinDamage(Vector3 position, float angleZ)
    {
        if (Time.time < _lastTickTime + _tickDamageInterval) return;

        PerformSlashDamage(position, _slashRadius, _slashDamage, angleZ);
        _lastTickTime = Time.time;
    }

    public void DefendProjectiles(Vector3 position)
    {
        float radius = _slashRadius * 1.2f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, _projectileLayer);
        if (hits == null || hits.Length == 0) return;

        HashSet<GameObject> despawned = new HashSet<GameObject>();
        foreach (var col in hits)
        {
            if (col == null) continue;

            GameObject projectile = col.gameObject;
            if (!despawned.Add(projectile)) continue;

            if (PoolManager.Instance != null)
                PoolManager.Instance.Despawn(projectile);
            else
                Object.Destroy(projectile);
        }
    }

    public void ResetTickTimer()
    {
        _lastTickTime = Time.time - _tickDamageInterval;
    }

    public bool PerformPinDamage(Transform targetTransform, Vector3 position, Vector2 direction, HashSet<IDamageable> hitTargets)
    {
        if (targetTransform == null) return false;
        if (!targetTransform.TryGetComponent<IDamageable>(out var damageable)) return false;
        if (damageable.IsDead) return false;
        if (hitTargets != null && !hitTargets.Add(damageable)) return !damageable.IsDead;

        Collider2D targetCollider = targetTransform.GetComponent<Collider2D>();
        if (targetCollider == null) return false;

        Vector2 knockbackDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        Vector2 hitPoint = targetCollider.ClosestPoint(position);
        damageable.TakeDamage(new DamageData
        {
            Damage = _pinDamage,
            GroggyDamage = _thrustPierceGroggyDamage,
            AttackerTeam = TeamType.Player,
            HitPoint = hitPoint,
            KnockbackForce = knockbackDirection * (_knockbackPower * 3f),
            IsPiercing = true,
            AttackKind = WeaponAttackKind.ThrustPierce
        });

        return !damageable.IsDead;
    }

    public void PerformMeleeDamage(Vector2 center, float radius, float damage, LayerMask targetLayer, Vector2 forward)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, Mathf.Max(0f, radius), targetLayer);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        Vector2 knockbackDirection = forward.sqrMagnitude > 0f ? forward.normalized : Vector2.right;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (!hit.TryGetComponent<IDamageable>(out var damageable)) continue;
            if (damageable.IsDead) continue;
            if (!damagedTargets.Add(damageable)) continue;

            damageable.TakeDamage(new DamageData
            {
                Damage = damage,
                AttackerTeam = TeamType.Player,
                HitPoint = hit.ClosestPoint(center),
                KnockbackForce = knockbackDirection * _knockbackPower,
                IsPiercing = false,
                AttackKind = WeaponAttackKind.None
            });
        }
    }

        public void PerformExplosiveFinisher(Vector2 center, Vector2 forward, EnemyBase pinnedTarget)
    {
        float radius = 7f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, _enemyLayer);
        bool pinnedTargetProcessed = false;

        foreach (var col in hits)
        {
            if (!col.TryGetComponent<EnemyBase>(out var enemy) || enemy.IsDead)
                continue;

            if (enemy == pinnedTarget)
            {
                if (enemy.CanExecuteCaptureFinisher())
                    enemy.ExecuteDeath(forward.normalized * _knockbackPower * 15f);

                pinnedTargetProcessed = true;
                continue;
            }

            Vector2 dir = ((Vector2)enemy.transform.position - center).normalized;
            enemy.TakeDamage(new DamageData
            {
                Damage = _slashDamage,
                AttackerTeam = TeamType.Player,
                HitPoint = col.ClosestPoint(center),
                KnockbackForce = dir * _knockbackPower * 5f,
                IsPiercing = false,
                AttackKind = WeaponAttackKind.None
            });
        }

        if (!pinnedTargetProcessed && pinnedTarget != null && !pinnedTarget.IsDead && pinnedTarget.CanExecuteCaptureFinisher())
        {
            Vector2 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            pinnedTarget.ExecuteDeath(safeForward * _knockbackPower * 15f);
        }
    }
        public void PerformSpinFinisher(Vector3 position, List<Transform> pinnedTargets, LayerMask wallMask)
    {
        float radius = 7f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, _enemyLayer);
        HashSet<EnemyBase> processedPinnedEnemies = new HashSet<EnemyBase>();

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (!col.TryGetComponent<EnemyBase>(out var other) || other.IsDead)
                continue;

            Vector2 baseDir = ((Vector2)col.transform.position - (Vector2)position).normalized;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = Vector2.right;

            float angleOffset = Random.Range(-8f, 8f) * Mathf.Deg2Rad;
            Vector2 knockbackDir = new Vector2(
                baseDir.x * Mathf.Cos(angleOffset) - baseDir.y * Mathf.Sin(angleOffset),
                baseDir.x * Mathf.Sin(angleOffset) + baseDir.y * Mathf.Cos(angleOffset)
            ).normalized;

            bool isPinnedTarget = pinnedTargets != null && pinnedTargets.Contains(col.transform);
            float forceMultiplier = isPinnedTarget ? 10f : 5f;

            Vector2 correctedKnockback = CalculateFinisherKnockback(
                col,
                knockbackDir,
                forceMultiplier,
                wallMask
            );

            if (isPinnedTarget)
            {
                if (other.CanExecuteCaptureFinisher())
                {
                    other.ExecuteDeath(correctedKnockback);
                    processedPinnedEnemies.Add(other);
                }

                continue;
            }

            other.TakeDamage(new DamageData
            {
                Damage = _slashDamage,
                AttackerTeam = TeamType.Player,
                HitPoint = col.ClosestPoint(position),
                KnockbackForce = correctedKnockback,
                IsPiercing = false,
                AttackKind = WeaponAttackKind.None
            });
        }

        if (pinnedTargets == null || pinnedTargets.Count == 0)
            return;

        for (int i = 0; i < pinnedTargets.Count; i++)
        {
            Transform pinnedTransform = pinnedTargets[i];
            if (pinnedTransform == null) continue;
            if (!pinnedTransform.TryGetComponent<EnemyBase>(out var pinnedEnemy)) continue;
            if (pinnedEnemy.IsDead) continue;
            if (processedPinnedEnemies.Contains(pinnedEnemy)) continue;
            if (!pinnedEnemy.CanExecuteCaptureFinisher()) continue;

            Vector2 baseDir = ((Vector2)pinnedTransform.position - (Vector2)position).normalized;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = Vector2.right;

            Vector2 correctedKnockback = CalculateFinisherKnockback(
                pinnedTransform.GetComponent<Collider2D>(),
                baseDir,
                10f,
                wallMask
            );

            pinnedEnemy.ExecuteDeath(correctedKnockback);
        }
    }

    private Vector2 CalculateFinisherKnockback(
    Collider2D targetCollider,
    Vector2 knockbackDirection,
    float forceMultiplier,
    LayerMask wallMask)
    {
        Vector2 safeDirection = knockbackDirection.sqrMagnitude > 0.0001f
            ? knockbackDirection.normalized
            : Vector2.right;

        float baseForce = _knockbackPower * Mathf.Max(0f, forceMultiplier);

        if (targetCollider == null || wallMask.value == 0)
            return safeDirection * baseForce;

        Bounds bounds = targetCollider.bounds;
        Vector2 castOrigin = bounds.center;
        float castRadius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.y));
        float probeDistance = Mathf.Max(_finisherWallSkinWidth, _finisherWallProbeDistance);

        RaycastHit2D wallHit = Physics2D.CircleCast(
            castOrigin,
            castRadius,
            safeDirection,
            probeDistance,
            wallMask
        );

        if (wallHit.collider == null)
            return safeDirection * baseForce;

        float allowedDistance = Mathf.Max(0f, wallHit.distance - _finisherWallSkinWidth);
        float distanceRatio = Mathf.Clamp01(allowedDistance / probeDistance);
        float forceRatio = Mathf.Lerp(_finisherBlockedForceRatio, 1f, distanceRatio);

        Vector2 tangent = new Vector2(-wallHit.normal.y, wallHit.normal.x);
        if (Vector2.Dot(tangent, safeDirection) < 0f)
            tangent = -tangent;

        Vector2 correctedDirection = Vector2.Lerp(tangent, safeDirection, distanceRatio);
        if (correctedDirection.sqrMagnitude <= 0.0001f)
            correctedDirection = tangent;

        return correctedDirection.normalized * (baseForce * forceRatio);
    }
}

