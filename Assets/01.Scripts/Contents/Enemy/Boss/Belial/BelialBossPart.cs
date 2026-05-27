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
    [SerializeField, Min(0f)] private float _embeddedFinisherGraceDuration = 0.15f;

    [Header("Sweep Telegraph")]
    [SerializeField] private GameObject _sweepGhostPrefab;
    [SerializeField] private Color _sweepGhostColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Ease _sweepGhostEase = Ease.OutQuad;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;
    private bool _cachedPose;

    private Tween _moveTween;
    private Tween _rotateTween;
    private Tween _bobTween;

    private bool _battleActive;
    private bool _contactDamageEnabled;
    private bool _attackLocked;
    private float _lastContactDamageTime = -999f;

    private Color[] _baseRendererColors;
    private Color _visualTint = Color.white;
    private float _visualAlpha = 1f;

    private Coroutine _hitFlashRoutine;
    private Coroutine _finisherDisableRoutine;
    private Coroutine _finisherGraceRoutine;

    private BelialBossPartState _currentState = BelialBossPartState.PreBattle;

    public BelialBossPartRole Role => _role;
    public BelialBossPartState CurrentPartState => _currentState;
    public bool IsDisabledForBoss => _currentState == BelialBossPartState.Disabled || _currentState == BelialBossPartState.BossGroggyFrozen;
    public bool IsHandInterruptingPattern =>
        _currentState == BelialBossPartState.HandGroggy ||
        _currentState == BelialBossPartState.FinisherGrace ||
        _currentState == BelialBossPartState.FinisherPending ||
        _currentState == BelialBossPartState.Disabled ||
        _currentState == BelialBossPartState.BossGroggyFrozen ||
        _currentState == BelialBossPartState.Dead;
    public bool CanRunPattern => IsHand() && _battleActive && _currentState == BelialBossPartState.Idle;
    public bool CanAcceptEmbeddedFinisher =>
        _currentState == BelialBossPartState.HandGroggy ||
        _currentState == BelialBossPartState.FinisherGrace ||
        _currentState == BelialBossPartState.FinisherPending;

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
        ChangeState(BelialBossPartState.PreBattle);
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
    }

    public IEnumerator MoveToAnchor(Transform anchor, float duration)
    {
        if (!CanRunPattern || anchor == null)
            yield break;

        ChangeState(BelialBossPartState.PatternMoving);
        KillMotionTweens();

        _moveTween = transform.DOLocalMove(anchor.localPosition, duration).SetEase(Ease.InOutSine).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(anchor.localRotation, duration).SetEase(Ease.InOutSine).SetTarget(this);
        yield return _moveTween.WaitForCompletion();
    }

    public IEnumerator ReturnToInitialPose(float duration)
    {
        if (IsDisabledForBoss || !_cachedPose)
            yield break;

        KillMotionTweens();
        _moveTween = transform.DOLocalMove(_initialLocalPosition, duration).SetEase(Ease.InOutSine).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(_initialLocalRotation, duration).SetEase(Ease.InOutSine).SetTarget(this);
        transform.localScale = _initialLocalScale;
        yield return _moveTween.WaitForCompletion();
    }

    public void StartIdleBob()
    {
        if (IsDisabledForBoss || _role == BelialBossPartRole.Head)
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
        if (IsDisabledForBoss || _role == BelialBossPartRole.Head)
            yield break;

        ChangeState(BelialBossPartState.Charging);

        if (_directAttacker == null || _attackData == null || target == null)
            yield break;

        float safeDuration = Mathf.Max(0.01f, chargeDuration);
        Vector3 chargeScale = _initialLocalScale * 1.1f;
        Tween t = transform.DOScale(chargeScale, safeDuration).SetEase(Ease.InOutSine).SetTarget(this);
        yield return t.WaitForCompletion();

        if (_currentState == BelialBossPartState.Charging && !_attackLocked)
            _directAttacker.TryPerformAttack(target, _attackData);

        transform.DOScale(_initialLocalScale, 0.1f).SetEase(Ease.OutSine).SetTarget(this);
    }

    public IEnumerator SweepTo(Transform targetAnchor, float duration)
    {
        if (IsDisabledForBoss || targetAnchor == null || _role == BelialBossPartRole.Head)
            yield break;

        ChangeState(BelialBossPartState.Sweeping);
        KillMotionTweens();

        _moveTween = transform.DOLocalMove(targetAnchor.localPosition, duration).SetEase(Ease.Linear).SetTarget(this);
        _rotateTween = transform.DOLocalRotateQuaternion(targetAnchor.localRotation, duration).SetEase(Ease.Linear).SetTarget(this);
        yield return _moveTween.WaitForCompletion();
    }

    public IEnumerator PlaySweepTelegraph(Transform targetAnchor, float duration, float delay)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        if (targetAnchor == null)
            yield break;

        if (_sweepGhostPrefab == null)
        {
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
            ghostRenderers[i].color = _sweepGhostColor;
        }

        Quaternion targetRotation = targetAnchor.rotation;
        Sequence seq = DOTween.Sequence();
        seq.Join(ghost.transform.DOMove(targetAnchor.position, safeDuration).SetEase(_sweepGhostEase));
        seq.Join(ghost.transform.DORotateQuaternion(targetRotation, safeDuration).SetEase(_sweepGhostEase));

        for (int i = 0; i < ghostRenderers.Length; i++)
        {
            SpriteRenderer sr = ghostRenderers[i];
            if (sr == null)
                continue;
            seq.Join(sr.DOFade(0f, safeDuration).SetEase(Ease.Linear));
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
        if (!active)
        {
            _contactDamageEnabled = false;
            ChangeState(BelialBossPartState.PreBattle);
            return;
        }

        if (_currentState == BelialBossPartState.PreBattle)
        {
            ChangeState(BelialBossPartState.Idle);
            return;
        }

        if (_currentState == BelialBossPartState.Disabled || _currentState == BelialBossPartState.BossGroggyFrozen || _currentState == BelialBossPartState.Dead)
            return;

        ChangeState(_currentState);
    }

    public void DisableByFinisher(DamageData sourceDamage)
    {
        if (!IsHand())
            return;

        BeginFinisherDisable(sourceDamage);
    }

    public void RestoreFromBossGroggy()
    {
        if (!IsHand())
            return;

        if (_currentState != BelialBossPartState.Disabled && _currentState != BelialBossPartState.BossGroggyFrozen)
            return;

        if (_visualRoot != null)
            _visualRoot.gameObject.SetActive(true);

        _currentHealth = _maxHealth;
        _currentGroggyGauge = 0f;
        _isCaptured = false;
        _isPierced = false;

        if (_cachedPose)
        {
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        ApplyVisualStyle();
        ChangeState(BelialBossPartState.Idle);
    }

    public void FreezeForBossGroggy()
    {
        if (!IsHand())
            return;

        ChangeState(BelialBossPartState.BossGroggyFrozen);
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

    public void CancelPatternAction()
    {
        if (_currentState != BelialBossPartState.PatternMoving &&
            _currentState != BelialBossPartState.Charging &&
            _currentState != BelialBossPartState.Sweeping)
            return;

        KillMotionTweens();
        _contactDamageEnabled = false;

        if (_cachedPose)
        {
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        ChangeState(BelialBossPartState.Idle);
    }

    public override void TakeDamage(DamageData damageData)
    {
        if (!_battleActive)
            return;

        if (_currentState == BelialBossPartState.Disabled ||
            _currentState == BelialBossPartState.BossGroggyFrozen ||
            _currentState == BelialBossPartState.Dead ||
            _currentState == BelialBossPartState.FinisherPending)
            return;

        bool isEmbeddedFinisher =
            damageData.AttackKind == WeaponAttackKind.EmbeddedAttack ||
            damageData.AttackKind == WeaponAttackKind.EmbeddedTearOut;

        if (IsHand() && isEmbeddedFinisher)
        {
            if (CanAcceptEmbeddedFinisher)
            {
                BeginFinisherDisable(damageData);
                return;
            }

            return;
        }

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
    }

    protected override void Die(Vector2 knockbackForce)
    {
        if (_role == BelialBossPartRole.Head)
        {
            _core?.NotifyHeadDied();
            base.Die(knockbackForce);
            return;
        }

        ChangeState(BelialBossPartState.Dead);
        BeginFinisherDisable(new DamageData
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

    protected override void OnGroggyEntered(DamageData damageData)
    {
        base.OnGroggyEntered(damageData);

        if (!IsHand() || IsDisabledForBoss)
            return;

        ChangeState(BelialBossPartState.HandGroggy);
        _core?.NotifyHandGroggyChanged(this, true);
    }

    protected override void OnGroggyExited()
    {
        base.OnGroggyExited();

        if (!IsHand())
            return;

        _core?.NotifyHandGroggyChanged(this, false);

        if (_currentState == BelialBossPartState.FinisherPending ||
            _currentState == BelialBossPartState.Disabled ||
            _currentState == BelialBossPartState.BossGroggyFrozen ||
            _currentState == BelialBossPartState.Dead)
            return;

        if (_finisherGraceRoutine != null)
            StopCoroutine(_finisherGraceRoutine);

        _finisherGraceRoutine = StartCoroutine(FinisherGraceRoutine());
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

    private IEnumerator FinisherGraceRoutine()
    {
        ChangeState(BelialBossPartState.FinisherGrace);
        float delay = Mathf.Max(0f, _embeddedFinisherGraceDuration);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _finisherGraceRoutine = null;

        if (_currentState == BelialBossPartState.FinisherPending ||
            _currentState == BelialBossPartState.Disabled ||
            _currentState == BelialBossPartState.BossGroggyFrozen ||
            _currentState == BelialBossPartState.Dead)
            yield break;

        ChangeState(BelialBossPartState.Idle);
    }

    private void BeginFinisherDisable(DamageData sourceDamage)
    {
        if (_currentState == BelialBossPartState.Disabled ||
            _currentState == BelialBossPartState.FinisherPending ||
            _currentState == BelialBossPartState.BossGroggyFrozen ||
            _currentState == BelialBossPartState.Dead)
            return;

        if (_finisherGraceRoutine != null)
        {
            StopCoroutine(_finisherGraceRoutine);
            _finisherGraceRoutine = null;
        }

        ChangeState(BelialBossPartState.FinisherPending);

        StopGroggyRoutines();
        _currentHealth = _maxHealth;
        _currentGroggyGauge = 0f;
        _isCaptured = false;
        _isPierced = false;

        if (_finisherDisableRoutine != null)
            StopCoroutine(_finisherDisableRoutine);

        _finisherDisableRoutine = StartCoroutine(FinisherDisableRoutine(sourceDamage));
    }

    private IEnumerator FinisherDisableRoutine(DamageData sourceDamage)
    {
        float delay = Mathf.Max(0f, _finisherDisableDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _finisherDisableRoutine = null;

        ChangeState(BelialBossPartState.Disabled);

        if (_cachedPose)
        {
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
        }

        _core?.NotifyHandDisabled(this);
    }

    private void ChangeState(BelialBossPartState nextState)
    {
        if (_currentState == nextState)
            return;

        _currentState = nextState;

        switch (nextState)
        {
            case BelialBossPartState.PreBattle:
                SetColliderEnabled(false);
                _contactDamageEnabled = false;
                _attackLocked = true;
                StopIdleBob();
                break;

            case BelialBossPartState.Idle:
                if (IsDisabledForBoss)
                    break;
                SetColliderEnabled(_battleActive);
                _contactDamageEnabled = false;
                _attackLocked = false;
                StartIdleBob();
                break;

            case BelialBossPartState.PatternMoving:
                SetColliderEnabled(true);
                _contactDamageEnabled = false;
                _attackLocked = true;
                StopIdleBob();
                break;

            case BelialBossPartState.Charging:
                SetColliderEnabled(true);
                _contactDamageEnabled = false;
                _attackLocked = false;
                StopIdleBob();
                break;

            case BelialBossPartState.Sweeping:
                SetColliderEnabled(true);
                _contactDamageEnabled = true;
                _attackLocked = true;
                StopIdleBob();
                break;

            case BelialBossPartState.HandGroggy:
            case BelialBossPartState.FinisherGrace:
                KillMotionTweens();
                StopIdleBob();
                _contactDamageEnabled = false;
                _attackLocked = true;
                SetColliderEnabled(true);
                break;

            case BelialBossPartState.FinisherPending:
                KillMotionTweens();
                StopIdleBob();
                _contactDamageEnabled = false;
                _attackLocked = true;
                SetColliderEnabled(false);
                break;

            case BelialBossPartState.Disabled:
                KillMotionTweens();
                StopIdleBob();
                _contactDamageEnabled = false;
                _attackLocked = true;
                SetColliderEnabled(false);
                if (_visualRoot != null)
                    _visualRoot.gameObject.SetActive(false);
                _currentHealth = _maxHealth;
                _currentGroggyGauge = 0f;
                _isCaptured = false;
                _isPierced = false;
                break;

            case BelialBossPartState.BossGroggyFrozen:
                KillMotionTweens();
                StopIdleBob();
                _contactDamageEnabled = false;
                _attackLocked = true;
                SetColliderEnabled(false);
                if (_visualRoot != null)
                    _visualRoot.gameObject.SetActive(false);
                break;

            case BelialBossPartState.Dead:
                KillMotionTweens();
                StopIdleBob();
                _contactDamageEnabled = false;
                _attackLocked = true;
                SetColliderEnabled(false);
                break;
        }
    }

    private void SetColliderEnabled(bool enabled)
    {
        if (_hitCollider != null)
            _hitCollider.enabled = enabled;
    }

    private bool IsHand()
    {
        return _role == BelialBossPartRole.LeftHand || _role == BelialBossPartRole.RightHand;
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

    private void KillMotionTweens()
    {
        _moveTween?.Kill();
        _rotateTween?.Kill();
        _moveTween = null;
        _rotateTween = null;
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
        if (_renderers == null || _renderers.Length == 0 || IsDisabledForBoss)
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
        if (_finisherDisableRoutine != null)
        {
            StopCoroutine(_finisherDisableRoutine);
            _finisherDisableRoutine = null;
        }

        if (_finisherGraceRoutine != null)
        {
            StopCoroutine(_finisherGraceRoutine);
            _finisherGraceRoutine = null;
        }

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
}
