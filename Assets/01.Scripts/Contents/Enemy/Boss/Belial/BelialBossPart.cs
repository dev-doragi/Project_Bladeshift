using System.Collections;
using DG.Tweening;
using UnityEngine;

public enum BelialBossPartRole
{
    Head,
    LeftHand,
    RightHand
}

public sealed class BelialBossPart : EnemyBase
{
    [Header("Boss Part")]
    [SerializeField] private BelialBossPartRole _role;
    [SerializeField] private BelialBossCore _core;

    [Header("Hand Attack")]
    [SerializeField] private EnemyProjectileAttackData _attackData;
    [SerializeField] private Transform _targetOverride;
    [SerializeField, Min(0.1f)] private float _attackRange = 12f;
    [SerializeField, Min(0.05f)] private float _chargeDuration = 0.8f;
    [SerializeField, Min(0.01f)] private float _chargeRecoverDuration = 0.12f;
    [SerializeField, Min(0.05f)] private float _attackCooldown = 1.5f;
    [SerializeField] private Vector3 _chargeScaleMultiplier = new Vector3(1.18f, 1.18f, 1f);

    [Header("Hand Disable")]
    [SerializeField, Min(0.1f)] private float _disabledDuration = 6f;
    [SerializeField, Min(0.01f)] private float _disableTweenDuration = 0.18f;
    [SerializeField] private Vector3 _disabledScaleMultiplier = new Vector3(0.82f, 0.82f, 1f);

    [Header("Disable Shockwave")]
    [SerializeField, Min(0f)] private float _disableShockwaveRadius = 4f;
    [SerializeField, Min(0f)] private float _disableShockwaveGroggyDamage = 100f;
    [SerializeField] private LayerMask _disableShockwaveTargetLayer;

    private EnemyDirectAttacker _directAttacker;
    private Transform _target;
    private Vector3 _baseScale;
    private Tween _scaleTween;
    private Coroutine _attackRoutine;
    private Coroutine _recoverRoutine;
    private bool _isTemporarilyDisabled;
    private bool _isHeadVulnerable;
    private bool _isDestroyed;
    private bool _externalAttackLock;

    public BelialBossPartRole Role => _role;
    public bool IsTemporarilyDisabled => _isTemporarilyDisabled;
    public bool IsHeadVulnerable => _isHeadVulnerable;
    public override bool IsDead => _isDestroyed;

    protected override void Awake()
    {
        base.Awake();

        _baseScale = transform.localScale;
        _directAttacker = GetComponent<EnemyDirectAttacker>();

        if (_core == null)
            _core = GetComponentInParent<BelialBossCore>();

        _core?.RegisterPart(this);
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);

        ResolveTarget();

        if (IsHand() && _attackRoutine == null)
            _attackRoutine = StartCoroutine(AttackRoutine());
    }

    protected override void OnDisable()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.Unsubscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);

        StopPartRoutines();
        _scaleTween?.Kill();
        transform.localScale = _baseScale;

        base.OnDisable();
    }

    public void Bind(BelialBossCore core)
    {
        _core = core;
    }

    public void SetHeadVulnerable(bool vulnerable)
    {
        if (_role != BelialBossPartRole.Head)
            return;

        _isHeadVulnerable = vulnerable;
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(vulnerable ? _baseScale * 1.08f : _baseScale, 0.15f).SetEase(Ease.OutQuad);
    }

    public void SetExternalAttackLock(bool locked)
    {
        _externalAttackLock = locked;

        if (locked)
            CancelChargeVisual();
    }

    public override void TakeDamage(DamageData damageData)
    {
        if (_role == BelialBossPartRole.Head && !_isHeadVulnerable)
            return;

        if (IsHand() && _isTemporarilyDisabled)
            return;

        base.TakeDamage(damageData);

        if (IsHand() && IsDisableTrigger(damageData))
            DisableHand(damageData);
    }

    protected override void Die(Vector2 knockbackForce)
    {
        if (_role == BelialBossPartRole.Head)
        {
            DestroyHead();
            return;
        }

        DisableHand(new DamageData
        {
            Damage = 0f,
            AttackerTeam = TeamType.Player,
            HitPoint = transform.position,
            KnockbackForce = knockbackForce,
            AttackKind = WeaponAttackKind.None
        });
    }

    protected override void OnGroggyEntered(DamageData damageData)
    {
        CancelChargeVisual();
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            yield return null;

            if (!CanAttack())
                continue;

            ResolveTarget();
            if (_target == null || !IsTargetInRange())
                continue;

            yield return ChargeAndFireRoutine();
            yield return new WaitForSeconds(_attackCooldown);
        }
    }

    private IEnumerator ChargeAndFireRoutine()
    {
        CancelChargeVisual();

        Vector3 chargeScale = Vector3.Scale(_baseScale, _chargeScaleMultiplier);
        _scaleTween = transform.DOScale(chargeScale, _chargeDuration).SetEase(Ease.InOutSine);
        yield return _scaleTween.WaitForCompletion();

        if (CanAttack() && _target != null && IsTargetInRange())
            _directAttacker?.TryPerformAttack(_target, _attackData);

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_baseScale, _chargeRecoverDuration).SetEase(Ease.OutQuad);
    }

    private bool CanAttack()
    {
        if (!IsHand())
            return false;
        if (_externalAttackLock || _isTemporarilyDisabled)
            return false;
        if (_isGroggy || _isCaptured || _isPierced || _isDestroyed)
            return false;
        if (_attackData == null || _directAttacker == null)
            return false;

        return true;
    }

    private bool IsTargetInRange()
    {
        float sqrDistance = (_target.position - transform.position).sqrMagnitude;
        return sqrDistance <= _attackRange * _attackRange;
    }

    private void DisableHand(DamageData sourceDamage)
    {
        if (!IsHand() || _isTemporarilyDisabled)
            return;

        _isTemporarilyDisabled = true;
        _currentHealth = _maxHealth;

        StopGroggyRoutines();
        CancelChargeVisual();
        ApplyDisableShockwave(sourceDamage);

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(Vector3.Scale(_baseScale, _disabledScaleMultiplier), _disableTweenDuration).SetEase(Ease.OutQuad);

        _core?.NotifyHandDisabled(this);

        if (_recoverRoutine != null)
            StopCoroutine(_recoverRoutine);

        _recoverRoutine = StartCoroutine(RecoverHandRoutine());
    }

    private IEnumerator RecoverHandRoutine()
    {
        yield return new WaitForSeconds(_disabledDuration);

        if (_externalAttackLock)
        {
            _recoverRoutine = null;
            yield break;
        }

        _isTemporarilyDisabled = false;
        _currentHealth = _maxHealth;

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(_baseScale, _disableTweenDuration).SetEase(Ease.OutBack);

        _core?.NotifyHandRecovered(this);
        _recoverRoutine = null;
    }

    private void DestroyHead()
    {
        if (_isDestroyed)
            return;

        _isDestroyed = true;
        _currentHealth = 0f;

        StopGroggyRoutines();
        CancelChargeVisual();

        if (_collider != null)
            _collider.enabled = false;

        _core?.NotifyHeadDestroyed(this);

        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void ApplyDisableShockwave(DamageData sourceDamage)
    {
        if (_disableShockwaveRadius <= 0f || _disableShockwaveTargetLayer.value == 0)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _disableShockwaveRadius, _disableShockwaveTargetLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy == null)
                enemy = hit.GetComponentInParent<EnemyBase>();

            if (enemy == null || enemy == this || enemy.IsDead)
                continue;

            Vector2 dir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector2.right;

            enemy.TakeDamage(new DamageData
            {
                Damage = 0f,
                GroggyDamage = _disableShockwaveGroggyDamage,
                AttackerTeam = TeamType.Player,
                HitPoint = hit.ClosestPoint(transform.position),
                KnockbackForce = dir * 2f,
                IsPiercing = false,
                IsExecution = false,
                AttackKind = WeaponAttackKind.None
            });
        }

        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Medium });
    }

    private bool IsDisableTrigger(DamageData damageData)
    {
        return damageData.AttackKind == WeaponAttackKind.EmbeddedTearOut ||
               damageData.AttackKind == WeaponAttackKind.EmbeddedAttack;
    }

    private bool IsHand()
    {
        return _role == BelialBossPartRole.LeftHand || _role == BelialBossPartRole.RightHand;
    }

    private void CancelChargeVisual()
    {
        _scaleTween?.Kill();
        transform.localScale = _isTemporarilyDisabled
            ? Vector3.Scale(_baseScale, _disabledScaleMultiplier)
            : _baseScale;
    }

    private void ResolveTarget()
    {
        if (_targetOverride != null)
        {
            _target = _targetOverride;
            return;
        }

        if (PlayerController.ActivePlayer != null)
            _target = PlayerController.ActivePlayer.transform;
    }

    private void HandlePlayerSpawned(PlayerSpawnedEvent evt)
    {
        if (evt.Player != null)
            _target = evt.Player.transform;
    }

    private void StopPartRoutines()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_recoverRoutine != null)
        {
            StopCoroutine(_recoverRoutine);
            _recoverRoutine = null;
        }
    }
}
