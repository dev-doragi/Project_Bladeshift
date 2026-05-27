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
    [Header("Part")]
    [SerializeField] private BelialBossPartRole _role;
    [SerializeField] private BelialBossCore _core;
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private SpriteRenderer[] _renderers;
    [SerializeField] private Collider2D _hitCollider;

    [Header("Ranged")]
    [SerializeField] private EnemyDirectAttacker _directAttacker;
    [SerializeField] private EnemyProjectileAttackData _attackData;
    [SerializeField] private Transform _projectileMuzzle;

    [Header("Idle Bob")]
    [SerializeField, Min(0f)] private float _bobAmplitude = 0.12f;
    [SerializeField, Min(0.1f)] private float _bobDuration = 1.2f;

    [Header("Contact")]
    [SerializeField, Min(0)] private int _sweepContactDamage = 1;
    [SerializeField, Min(0.05f)] private float _sweepContactDamageInterval = 0.25f;

    [Header("Visual FX")]
    [SerializeField] private Color _hitFlashColor = new Color(1f, 0.9f, 0.9f, 1f);
    [SerializeField, Min(0.01f)] private float _hitFlashDuration = 0.08f;
    [SerializeField, Min(0f)] private float _finisherDisableDelay = 0.2f;

    [Header("Sweep Telegraph")]
    [SerializeField] private GameObject _sweepGhostPrefab;
    [SerializeField] private Color _sweepGhostColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Ease _sweepGhostEase = Ease.OutQuad;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;
    private bool _cachedPose;
    private Vector3 _disabledLocalPosition;
    private Quaternion _disabledLocalRotation;
    private Vector3 _disabledLocalScale;
    private bool _hasDisabledPose;

    private Tween _moveTween;
    private Tween _rotateTween;
    private Tween _bobTween;
    private bool _attackLocked;
    private bool _disabledForBoss;
    private bool _contactDamageEnabled;
    private bool _battleActive;
    private float _lastContactDamageTime = -999f;

    private Color[] _baseRendererColors;
    private Color _visualTint = Color.white;
    private float _visualAlpha = 1f;
    private Coroutine _hitFlashRoutine;
    private Coroutine _finisherDisableRoutine;
    private bool _isPendingFinisherDisable;
    private bool _frozenForBossGroggy;
    private bool _frozenForSelfGroggy;
    private int _scriptedMotionDepth;
    private RigidbodyType2D _scriptedMotionOriginalBodyType;
    private bool _hasScriptedMotionOriginalBodyType;
    private Vector3 _frozenLocalPosition;
    private Quaternion _frozenLocalRotation;
    private Vector3 _frozenLocalScale;
    private RigidbodyConstraints2D _cachedConstraints;
    private RigidbodyType2D _cachedBodyType;
    private bool _hasCachedRigidbodyState;

    public BelialBossPartRole Role => _role;
    public bool IsDisabledForBoss => _disabledForBoss;

    public override bool CanBeCaptured => false;
    public override float CaptureWeight => 9999f;

    protected override void Awake()
    {
        base.Awake();

        if (_visualRoot == null)
            _visualRoot = transform;

        if (_hitCollider == null)
            _hitCollider = GetComponent<Collider2D>();

        if (_directAttacker == null)
            _directAttacker = GetComponent<EnemyDirectAttacker>();

        if (_core == null)
            _core = GetComponentInParent<BelialBossCore>();

        CacheRendererBaseColors();
        ApplyVisualStyle();
    }

    public void Bind(BelialBossCore core)
    {
        _core = core;
    }

    public void CacheInitialPose()
    {
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialLocalScale = transform.localScale;
        _cachedPose = true;
        _disabledLocalPosition = _initialLocalPosition;
        _disabledLocalRotation = _initialLocalRotation;
        _disabledLocalScale = _initialLocalScale;
        _hasDisabledPose = true;
    }

    public IEnumerator MoveToAnchor(Transform anchor, float duration)
    {
        if (_disabledForBoss || anchor == null)
            yield break;

        StopIdleBob();
        KillMotionTweens();
        BeginScriptedMotion();

        _moveTween = transform.DOLocalMove(anchor.localPosition, duration).SetEase(Ease.InOutSine).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(anchor.localRotation, duration).SetEase(Ease.InOutSine).SetTarget(this);
        yield return _moveTween.WaitForCompletion();
        EndScriptedMotion();
    }

    public IEnumerator ReturnToInitialPose(float duration)
    {
        if (_disabledForBoss || !_cachedPose)
            yield break;

        KillMotionTweens();
        BeginScriptedMotion();

        _moveTween = transform.DOLocalMove(_initialLocalPosition, duration).SetEase(Ease.InOutSine).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(_initialLocalRotation, duration).SetEase(Ease.InOutSine).SetTarget(this);
        transform.localScale = _initialLocalScale;
        yield return _moveTween.WaitForCompletion();
        EndScriptedMotion();
    }

    public void StartIdleBob()
    {
        if (_disabledForBoss || _role == BelialBossPartRole.Head)
            return;

        if (!_cachedPose)
            CacheInitialPose();

        if (_bobTween != null && _bobTween.IsActive())
            return;

        transform.localPosition = _initialLocalPosition;
        _bobTween = transform
            .DOLocalMoveY(_initialLocalPosition.y + _bobAmplitude, _bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    public void StopIdleBob()
    {
        if (_bobTween != null)
        {
            _bobTween.Kill();
            _bobTween = null;
        }
    }

    public IEnumerator ChargeAndFire(Transform target, float chargeDuration)
    {
        if (_disabledForBoss || _attackLocked || _role == BelialBossPartRole.Head)
            yield break;

        if (_directAttacker == null)
        {
            Debug.LogError($"[BelialBossPart:{name}] ChargeAndFire failed: EnemyDirectAttacker is null.", this);
            yield break;
        }

        if (_attackData == null)
        {
            Debug.LogError($"[BelialBossPart:{name}] ChargeAndFire failed: AttackData is null.", this);
            yield break;
        }

        if (target == null)
        {
            Debug.LogError($"[BelialBossPart:{name}] ChargeAndFire failed: target is null.", this);
            yield break;
        }

        float safeDuration = Mathf.Max(0.01f, chargeDuration);
        Vector3 chargeScale = _initialLocalScale * 1.1f;
        Tween t = transform.DOScale(chargeScale, safeDuration).SetEase(Ease.InOutSine).SetTarget(this);
        yield return t.WaitForCompletion();

        if (!_disabledForBoss && !_attackLocked)
        {
            bool fired = _directAttacker.TryPerformAttack(target, _attackData);
            if (!fired)
                Debug.LogError($"[BelialBossPart:{name}] TryPerformAttack returned false. Check AttackData.ProjectilePrefab and PoolManager registration.", this);
        }

        transform.DOScale(_initialLocalScale, 0.1f).SetEase(Ease.OutSine).SetTarget(this);
    }

    public IEnumerator SweepTo(Transform targetAnchor, float duration)
    {
        if (_disabledForBoss || targetAnchor == null || _role == BelialBossPartRole.Head)
            yield break;

        StopIdleBob();
        KillMotionTweens();
        BeginScriptedMotion();

        _moveTween = transform.DOLocalMove(targetAnchor.localPosition, duration).SetEase(Ease.Linear).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(targetAnchor.localRotation, duration).SetEase(Ease.Linear).SetTarget(this);
        yield return _moveTween.WaitForCompletion();
        EndScriptedMotion();
    }

    public IEnumerator PlaySweepTelegraph(Transform targetAnchor, float duration, float delay)
    {
        float safeDuration = Mathf.Max(0.01f, duration);

        if (targetAnchor == null)
            yield break;

        if (_sweepGhostPrefab == null)
        {
            Debug.LogWarning($"[BelialBossPart:{name}] Sweep telegraph skipped: ghost prefab is null.", this);
            yield return new WaitForSeconds(safeDuration + Mathf.Max(0f, delay));
            yield break;
        }

        GameObject ghost = Instantiate(_sweepGhostPrefab, transform.position, transform.rotation);
        if (ghost == null)
        {
            yield return new WaitForSeconds(safeDuration + Mathf.Max(0f, delay));
            yield break;
        }

        RemoveGhostGameplayComponents(ghost);

        SpriteRenderer[] ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            if (ghostRenderers[i] == null)
                continue;

            Color c = _sweepGhostColor;
            ghostRenderers[i].color = c;
        }

        Quaternion targetRotation = targetAnchor.rotation;
        Sequence seq = DOTween.Sequence();
        seq.Join(ghost.transform.DOMove(targetAnchor.position, safeDuration).SetEase(_sweepGhostEase));
        seq.Join(ghost.transform.DORotateQuaternion(targetRotation, safeDuration).SetEase(_sweepGhostEase));

        if (ghostRenderers.Length > 0)
        {
            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                SpriteRenderer sr = ghostRenderers[i];
                if (sr == null)
                    continue;
                seq.Join(sr.DOFade(0f, safeDuration).SetEase(Ease.Linear));
            }
        }

        yield return seq.WaitForCompletion();

        if (ghost != null)
            Destroy(ghost);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);
    }

    public void SetAttackLocked(bool locked)
    {
        _attackLocked = locked;
    }

    public void SetBattleActive(bool active)
    {
        _battleActive = active;

        if (_hitCollider != null)
            _hitCollider.enabled = active && !_disabledForBoss;

        if (!active)
            _contactDamageEnabled = false;
    }

    public void DisableByFinisher(DamageData sourceDamage)
    {
        if (!IsHand())
            return;
        if (_disabledForBoss || _isPendingFinisherDisable)
            return;

        _isPendingFinisherDisable = true;
        _attackLocked = true;
        _contactDamageEnabled = false;
        StopIdleBob();
        KillMotionTweens();
        DOTween.Kill(this);

        if (_finisherDisableRoutine != null)
            StopCoroutine(_finisherDisableRoutine);
        _finisherDisableRoutine = StartCoroutine(FinisherDisableRoutine(sourceDamage));
    }

    public void RestoreFromBossGroggy()
    {
        if (!IsHand())
            return;

        _frozenForBossGroggy = false;
        _frozenForSelfGroggy = false;
        _scriptedMotionDepth = 0;
        if (!_frozenForSelfGroggy)
            RestoreRigidbodyFromFreeze();

        _disabledForBoss = false;
        _attackLocked = false;

        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(true);

        if (_hitCollider != null)
            _hitCollider.enabled = _battleActive;

        StopGroggyRoutines();
        _currentHealth = _maxHealth;
        _currentGroggyGauge = 0f;
        _isCaptured = false;
        _isPierced = false;

        if (_cachedPose)
        {
            if (_hasDisabledPose)
            {
                transform.localPosition = _disabledLocalPosition;
                transform.localRotation = _disabledLocalRotation;
                transform.localScale = _disabledLocalScale;
            }
            else
            {
                transform.localPosition = _initialLocalPosition;
                transform.localRotation = _initialLocalRotation;
                transform.localScale = _initialLocalScale;
            }
        }

        ApplyVisualStyle();
        StartIdleBob();
    }

    public void FreezeForBossGroggy()
    {
        if (!IsHand())
            return;

        _frozenForBossGroggy = true;
        _frozenLocalPosition = transform.localPosition;
        _frozenLocalRotation = transform.localRotation;
        _frozenLocalScale = transform.localScale;

        _attackLocked = true;
        _contactDamageEnabled = false;
        StopIdleBob();
        KillMotionTweens();
        DOTween.Kill(this);

        ApplyRigidbodyFreeze();
    }

    protected override void OnGroggyEntered(DamageData damageData)
    {
        base.OnGroggyEntered(damageData);

        if (!IsHand())
            return;

        _frozenForSelfGroggy = true;
        _attackLocked = true;
        _contactDamageEnabled = false;
        StopIdleBob();
        KillMotionTweens();
        DOTween.Kill(this);

        _frozenLocalPosition = transform.localPosition;
        _frozenLocalRotation = transform.localRotation;
        _frozenLocalScale = transform.localScale;
        ApplyRigidbodyFreeze();
    }

    protected override void OnGroggyExited()
    {
        base.OnGroggyExited();

        if (!IsHand())
            return;

        _frozenForSelfGroggy = false;
        if (!_frozenForBossGroggy)
            RestoreRigidbodyFromFreeze();
    }

    private void ApplyRigidbodyFreeze()
    {
        if (_rb != null)
        {
            if (!_hasCachedRigidbodyState)
            {
                _cachedConstraints = _rb.constraints;
                _cachedBodyType = _rb.bodyType;
                _hasCachedRigidbodyState = true;
            }

            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void RestoreRigidbodyFromFreeze()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (_hasCachedRigidbodyState)
            {
                _rb.bodyType = _cachedBodyType;
                _rb.constraints = _cachedConstraints;
            }
            else
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.freezeRotation = true;
            }
        }

        _hasCachedRigidbodyState = false;
    }

    public void SetContactDamageEnabled(bool enabled)
    {
        _contactDamageEnabled = enabled;
    }

    public void SetContactDamage(int damage, float interval)
    {
        _sweepContactDamage = Mathf.Max(0, damage);
        _sweepContactDamageInterval = Mathf.Max(0.05f, interval);
    }

    public void SetVisualTint(Color tint)
    {
        _visualTint = tint;
        ApplyVisualStyle();
    }

    public void SetVisualAlpha(float alpha)
    {
        _visualAlpha = Mathf.Clamp01(alpha);
        ApplyVisualStyle();
    }

    public override void TakeDamage(DamageData damageData)
    {
        if (_frozenForBossGroggy)
            return;

        if (!_battleActive)
            return;

        if (_disabledForBoss)
            return;

        if (_role == BelialBossPartRole.Head)
        {
            if (_core != null && !_core.CanHeadTakeDamage())
                return;

            base.TakeDamage(damageData);
            PlayUnifiedHitFlash();
            return;
        }

        base.TakeDamage(damageData);
        PlayUnifiedHitFlash();

        if (damageData.AttackKind == WeaponAttackKind.EmbeddedAttack || damageData.AttackKind == WeaponAttackKind.EmbeddedTearOut)
            DisableByFinisher(damageData);
    }

    protected override void Die(Vector2 knockbackForce)
    {
        if (_role == BelialBossPartRole.Head)
        {
            _core?.NotifyHeadDied();
            base.Die(knockbackForce);
            return;
        }

        DisableInternal(new DamageData
        {
            Damage = 0f,
            GroggyDamage = 0f,
            AttackerTeam = TeamType.Player,
            HitPoint = transform.position,
            KnockbackForce = knockbackForce,
            IsPiercing = false,
            IsExecution = false,
            AttackKind = WeaponAttackKind.None
        });
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!_battleActive)
            return;

        TryDealSweepContactDamage(other);
    }

    protected override void OnTriggerStay2D(Collider2D other)
    {
        if (!_battleActive)
            return;

        TryDealSweepContactDamage(other);
    }

    public override bool CanBeCapturedByPierce()
    {
        return false;
    }

    public override bool CanExecuteCaptureFinisher()
    {
        return false;
    }

    private void TryDealSweepContactDamage(Collider2D other)
    {
        if (!_contactDamageEnabled || _sweepContactDamage <= 0)
            return;

        if (Time.time < _lastContactDamageTime + _sweepContactDamageInterval)
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        _lastContactDamageTime = Time.time;
        playerHealth.TakeDamage(new DamageData
        {
            Damage = _sweepContactDamage,
            GroggyDamage = 0f,
            AttackerTeam = TeamType.Enemy,
            HitPoint = other.ClosestPoint(transform.position),
            KnockbackForce = Vector2.zero,
            IsPiercing = false,
            IsExecution = false,
            AttackKind = WeaponAttackKind.None
        });
    }

    private void DisableInternal(DamageData sourceDamage)
    {
        if (_disabledForBoss)
            return;

        _disabledForBoss = true;
        _frozenForBossGroggy = false;
        _frozenForSelfGroggy = false;
        _scriptedMotionDepth = 0;
        _attackLocked = true;

        StopIdleBob();
        KillMotionTweens();
        DOTween.Kill(this);

        StopGroggyRoutines();
        _currentHealth = _maxHealth;
        _currentGroggyGauge = 0f;
        _isCaptured = false;
        _isPierced = false;

        _disabledLocalPosition = transform.localPosition;
        _disabledLocalRotation = transform.localRotation;
        _disabledLocalScale = transform.localScale;
        _hasDisabledPose = true;

        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(false);

        if (_hitCollider != null)
            _hitCollider.enabled = false;

        if (_cachedPose)
        {
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        _core?.NotifyHandDisabled(this);
    }

    private IEnumerator FinisherDisableRoutine(DamageData sourceDamage)
    {
        float delay = Mathf.Max(0f, _finisherDisableDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _finisherDisableRoutine = null;
        _isPendingFinisherDisable = false;
        DisableInternal(sourceDamage);
    }

    private bool IsHand()
    {
        return _role == BelialBossPartRole.LeftHand || _role == BelialBossPartRole.RightHand;
    }

    private void KillMotionTweens()
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();
        _moveTween = null;
        _rotateTween = null;
        if (_scriptedMotionDepth > 0)
        {
            _scriptedMotionDepth = 0;
            RestoreRigidbodyAfterScriptedMotion();
        }
    }

    private void BeginScriptedMotion()
    {
        _scriptedMotionDepth++;
        if (_scriptedMotionDepth != 1)
            return;

        if (_rb == null)
            return;

        _scriptedMotionOriginalBodyType = _rb.bodyType;
        _hasScriptedMotionOriginalBodyType = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void EndScriptedMotion()
    {
        if (_scriptedMotionDepth <= 0)
            return;

        _scriptedMotionDepth--;
        if (_scriptedMotionDepth == 0)
            RestoreRigidbodyAfterScriptedMotion();
    }

    private void RestoreRigidbodyAfterScriptedMotion()
    {
        if (_rb == null)
            return;

        if (_frozenForBossGroggy || _frozenForSelfGroggy)
            return;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        if (_hasScriptedMotionOriginalBodyType)
            _rb.bodyType = _scriptedMotionOriginalBodyType;

        _hasScriptedMotionOriginalBodyType = false;
    }

    private void CacheRendererBaseColors()
    {
        if (_renderers == null)
            _renderers = new SpriteRenderer[0];

        _baseRendererColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _baseRendererColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
    }

    private void ApplyVisualStyle()
    {
        if (_renderers == null || _baseRendererColors == null)
            return;

        int count = Mathf.Min(_renderers.Length, _baseRendererColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer sr = _renderers[i];
            if (sr == null)
                continue;

            Color baseColor = _baseRendererColors[i];
            Color c = new Color(
                baseColor.r * _visualTint.r,
                baseColor.g * _visualTint.g,
                baseColor.b * _visualTint.b,
                baseColor.a * _visualTint.a * _visualAlpha);
            sr.color = c;
        }
    }

    private void PlayUnifiedHitFlash()
    {
        if (_renderers == null || _renderers.Length == 0 || _disabledForBoss)
            return;

        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }

        if (_hitFlashRoutine != null)
            StopCoroutine(_hitFlashRoutine);

        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float duration = Mathf.Max(0.01f, _hitFlashDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float blend = 1f - t;

            int count = Mathf.Min(_renderers.Length, _baseRendererColors.Length);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = _renderers[i];
                if (sr == null)
                    continue;

                Color baseColor = _baseRendererColors[i];
                Color styled = new Color(
                    baseColor.r * _visualTint.r,
                    baseColor.g * _visualTint.g,
                    baseColor.b * _visualTint.b,
                    baseColor.a * _visualTint.a * _visualAlpha);

                sr.color = Color.Lerp(styled, _hitFlashColor, blend);
            }

            yield return null;
        }

        ApplyVisualStyle();
        _hitFlashRoutine = null;
    }

    private static void RemoveGhostGameplayComponents(GameObject ghost)
    {
        Collider2D[] colliders = ghost.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                Destroy(colliders[i]);
        }

        Rigidbody2D[] rigidbodies = ghost.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
                Destroy(rigidbodies[i]);
        }

        BelialBossPart[] bossParts = ghost.GetComponentsInChildren<BelialBossPart>(true);
        for (int i = 0; i < bossParts.Length; i++)
        {
            if (bossParts[i] != null)
                Destroy(bossParts[i]);
        }

        EnemyBase[] enemyBases = ghost.GetComponentsInChildren<EnemyBase>(true);
        for (int i = 0; i < enemyBases.Length; i++)
        {
            if (enemyBases[i] != null)
                Destroy(enemyBases[i]);
        }
    }

    protected override void OnDisable()
    {
        _isPendingFinisherDisable = false;
        if (_finisherDisableRoutine != null)
        {
            StopCoroutine(_finisherDisableRoutine);
            _finisherDisableRoutine = null;
        }

        _frozenForBossGroggy = false;
        _hasCachedRigidbodyState = false;

        StopIdleBob();
        KillMotionTweens();
        DOTween.Kill(this);

        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
        }

        base.OnDisable();
    }

    private void LateUpdate()
    {
        if (!_frozenForBossGroggy && !_frozenForSelfGroggy)
            return;

        transform.localPosition = _frozenLocalPosition;
        transform.localRotation = _frozenLocalRotation;
        transform.localScale = _frozenLocalScale;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }
}

