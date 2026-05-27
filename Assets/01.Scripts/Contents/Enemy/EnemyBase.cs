using System.Collections;
using System;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [SerializeField] protected EnemyData _enemyData;
    [SerializeField] protected float _maxHealth = 50f;

    [Header("Contact Damage")]
    [SerializeField, Min(0)] private int _contactDamage = 1;

    [Header("Groggy Motion")]
    [SerializeField] private float _groggyLeanAngle = 10f;
    [SerializeField] private float _groggyPoseDuration = 0.18f;
    [SerializeField] private float _groggyRecoilForce = 3f;

    [Header("Movement Damping")]
    [SerializeField, Min(0f)] private float _groundLinearDamping = 10f;
    [SerializeField, Min(0f)] private float _fallingLinearDamping = 0f;
    [SerializeField] private float _fallingVelocityThreshold = -0.05f;

    [Header("Death Sequence")]
    [SerializeField, Min(0f)] private float _deathCollisionWaitTimeout = 8f;
    [SerializeField, Min(0f)] private float _deathSettleTimeout = 4f;

    protected float _currentHealth;

    protected Rigidbody2D _rb;
    protected SpriteRenderer _spriteRenderer;
    protected Collider2D _collider;

    protected Color _originalColor;
    protected Coroutine _blinkRoutine;
    protected Coroutine _groggyRoutine;
    protected Coroutine _groggyRecoveryRoutine;
    protected Coroutine _groggyVisualRoutine;
    protected Coroutine _groggyPoseRoutine;
    protected bool _isGroggy;
    protected bool _isGroggyInvulnerable;
    protected bool _isRecoveringFromGroggy;
    protected bool _isCaptured;
    protected bool _isPierced;
    protected bool _hasHitWallAfterDeath = false;
    protected bool _hasTouchedSurfaceAfterDeath = false;
    protected float _currentGroggyGauge;
    private Quaternion _originalRotation;

    public virtual TeamType Team => TeamType.Enemy;
    public virtual bool IsDead => _currentHealth <= 0f;
    public EnemyData Data => _enemyData;
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsGroggy => _isGroggy;
    public bool IsGroggyInvulnerable => _isGroggyInvulnerable;
    public bool IsRecoveringFromGroggy => _isRecoveringFromGroggy;
    public bool IsCaptured => _isCaptured;
    public bool IsPierced => _isPierced;
    public float CurrentGroggyGauge => _currentGroggyGauge;
    public float MaxGroggyGauge => _enemyData != null ? _enemyData.MaxGroggyGauge : 0f;
    public virtual EnemyCategory Category => _enemyData != null ? _enemyData.Category : EnemyCategory.Normal;
    public virtual float Weight => _enemyData != null ? _enemyData.Weight : 0f;
    public virtual bool CanBeCaptured => _enemyData != null && _enemyData.CanBeCaptured;
    public virtual float CaptureWeight => _enemyData != null ? _enemyData.CaptureWeight : 0f;
    public virtual bool CanBeExecuted => _enemyData == null || _enemyData.CanBeExecuted;
    public virtual GroggyRightClickActionType GroggyRightClickAction => _enemyData != null ? _enemyData.GroggyRightClickAction : GroggyRightClickActionType.None;
    public virtual bool UsesEmbeddedAttackMechanic => _enemyData != null && _enemyData.UsesEmbeddedAttackMechanic;
    public virtual float EmbeddedTearOutDamage => _enemyData != null ? _enemyData.EmbeddedTearOutDamage : 50f;
    public virtual float EmbeddedAttackDamage => _enemyData != null ? _enemyData.EmbeddedAttackDamage : 100f;
    public virtual float EmbeddedAttackRange => _enemyData != null ? _enemyData.EmbeddedAttackRange : 2f;
    public event Action<EnemyBase> GroggyStateEntered;
    public event Action<EnemyBase> GroggyStateExited;
    public bool IsDamageBlocked { get; set; }
    public bool IsKnockbackBlocked { get; set; }

    protected virtual void Awake()
    {
        if (_enemyData != null)
            _maxHealth = _enemyData.MaxHealth;

        _currentHealth = _maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _originalRotation = transform.rotation;

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;

        if (_rb != null)
        {
            _rb.freezeRotation = true;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.linearDamping = _groundLinearDamping;
        }
    }

    protected virtual void FixedUpdate()
    {
        UpdateLinearDamping();
    }

    private void UpdateLinearDamping()
    {
        if (_rb == null || IsDead)
            return;

        _rb.linearDamping = _rb.linearVelocity.y < _fallingVelocityThreshold
            ? _fallingLinearDamping
            : _groundLinearDamping;
    }

    public virtual void TakeDamage(DamageData damageData)
    {
        if (IsDead) return;
        if (IsDamageBlocked) return;
        if (_isGroggy && damageData.IsPiercing && !damageData.IsExecution) return;
        if (_isGroggyInvulnerable && !CanBypassGroggyInvulnerability(damageData)) return;

        StopRecoveryOnDamage();

        float previousHealth = _currentHealth;

        _currentHealth -= damageData.Damage;

        if (_rb != null && !IsKnockbackBlocked && damageData.KnockbackForce.sqrMagnitude > 0.0001f)
        {
            Vector2 adjustedKnockback = damageData.KnockbackForce * GetKnockbackTakenMultiplier();
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(adjustedKnockback, ForceMode2D.Impulse);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Weak });
            //EventBus.Instance.Publish(new HitStopEvent { Duration = 0.3f });
        }

        if (_blinkRoutine == null && gameObject.activeInHierarchy)
            _blinkRoutine = StartCoroutine(BlinkRoutine());

        if (TryHandleGroggyAfterDamage(previousHealth, damageData))
            return;

        if (_currentHealth <= 0f)
        {
            Die(IsKnockbackBlocked ? Vector2.zero : damageData.KnockbackForce);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    private void TryDealContactDamage(Collider2D other)
    {
        if (IsDead)
            return;
        if (_isCaptured)
            return;
        if (_isPierced)
            return;

        if (_contactDamage <= 0)
            return;

        if (other == null)
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        playerHealth.TakeDamage(new DamageData
        {
            Damage = _contactDamage,
            GroggyDamage = 0,
            AttackerTeam = TeamType.Enemy,
            HitPoint = other.ClosestPoint(transform.position),
            KnockbackForce = Vector2.zero,
            IsPiercing = false
        });
    }

    public virtual bool ShouldPiercingAttackStick()
    {
        return ShouldPierceStick();
    }

    public virtual bool CanBeCapturedByPierce()
    {
        // Conservative default for generic enemies without policy data.
        if (_enemyData == null) return false;
        return _enemyData.CanBeCapturedByPierce(_isGroggy);
    }

    public virtual bool ShouldPierceStick()
    {
        // Conservative default for generic enemies without policy data.
        if (_enemyData == null) return false;
        return _enemyData.ShouldPierceStick(_isGroggy);
    }

    public virtual bool ShouldPiercePassThrough()
    {
        // Conservative default for generic enemies without policy data.
        if (_enemyData == null) return true;
        return _enemyData.ShouldPiercePassThrough(_isGroggy);
    }

    public virtual bool CanExecuteCaptureFinisher()
    {
        if (_enemyData == null) return false;
        return CanBeCapturedByPierce() && CanBeExecuted;
    }

    protected virtual bool TryHandleGroggyAfterDamage(float previousHealth, DamageData damageData)
    {
        if (_isGroggy)
            return false;

        if (damageData.IsExecution)
            return false;

        if (_enemyData == null || !_enemyData.UsesGroggy)
            return false;

        switch (_enemyData.GroggyTriggerMode)
        {
            case GroggyTriggerMode.HealthThreshold:
                return TryHandleHealthThresholdGroggy(previousHealth, damageData);
            case GroggyTriggerMode.Gauge:
                return TryHandleGaugeGroggy(damageData);
            case GroggyTriggerMode.None:
            default:
                return false;
        }
    }

    protected virtual bool TryHandleHealthThresholdGroggy(float previousHealth, DamageData damageData)
    {
        bool crossedThreshold = previousHealth > _enemyData.GroggyThresholdHealth &&
                                _currentHealth <= _enemyData.GroggyThresholdHealth;
        bool recoveryHitBelowThreshold = _isRecoveringFromGroggy &&
                                         _currentHealth <= _enemyData.GroggyThresholdHealth;
        bool wouldDie = _currentHealth <= 0f;

        if (!crossedThreshold && !recoveryHitBelowThreshold && !wouldDie)
            return false;

        _currentHealth = Mathf.Max(1f, _currentHealth);
        EnterGroggy(damageData);
        return true;
    }

    protected virtual bool TryHandleGaugeGroggy(DamageData damageData)
    {
        if (_currentHealth <= 0f)
            return false;

        if (damageData.GroggyDamage <= 0f)
            return false;

        _currentGroggyGauge = Mathf.Min(_currentGroggyGauge + damageData.GroggyDamage, _enemyData.MaxGroggyGauge);
        if (_currentGroggyGauge < _enemyData.MaxGroggyGauge)
            return false;

        EnterGroggy(damageData);
        return true;
    }

    protected virtual bool CanBypassGroggyInvulnerability(DamageData damageData)
    {
        if (damageData.IsExecution)
            return true;

        return damageData.AttackKind == WeaponAttackKind.Execution ||
               damageData.AttackKind == WeaponAttackKind.EmbeddedAttack ||
               damageData.AttackKind == WeaponAttackKind.EmbeddedTearOut;
    }

    protected virtual float GetKnockbackTakenMultiplier()
    {
        return _enemyData != null ? _enemyData.KnockbackTakenMultiplier : 1f;
    }

    protected virtual void EnterGroggy(DamageData damageData)
    {
        _isGroggy = true;
        _isGroggyInvulnerable = true;
        _isRecoveringFromGroggy = false;

        if (_enemyData != null && _enemyData.ResetGroggyGaugeOnEnter)
            _currentGroggyGauge = 0f;

        if (_groggyRecoveryRoutine != null)
        {
            StopCoroutine(_groggyRecoveryRoutine);
            _groggyRecoveryRoutine = null;
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (!IsKnockbackBlocked)
            {
                Vector2 recoilDirection = damageData.KnockbackForce.sqrMagnitude > 0.0001f
                    ? damageData.KnockbackForce.normalized
                    : -Vector2.right;
                _rb.AddForce(recoilDirection * _groggyRecoilForce, ForceMode2D.Impulse);
            }
        }

        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }

        OnGroggyEntered(damageData);
        GroggyStateEntered?.Invoke(this);
        StartGroggyPose();
        StartGroggyVisual();
        RestartGroggyHold();
    }

    protected virtual void RestartGroggyHold()
    {
        if (_isCaptured)
            return;

        if (_groggyRoutine != null)
            StopCoroutine(_groggyRoutine);

        if (gameObject.activeInHierarchy)
            _groggyRoutine = StartCoroutine(GroggyRoutine());
    }

    protected virtual IEnumerator GroggyRoutine()
    {
        float invulnerableDuration = _enemyData != null ? _enemyData.GroggyInvulnerableDuration : 0.75f;
        float groggyDuration = _enemyData != null ? _enemyData.GroggyDuration : 3f;

        _isGroggyInvulnerable = true;
        yield return new WaitForSeconds(invulnerableDuration);
        _isGroggyInvulnerable = false;

        yield return new WaitForSeconds(groggyDuration);

        if (_isCaptured)
        {
            _groggyRoutine = null;
            yield break;
        }

        _groggyRoutine = null;
        ExitGroggyAndRecover();
    }

    protected virtual void ExitGroggyAndRecover()
    {
        ExitGroggy(true);
    }

    protected virtual void ExitGroggy(bool allowRecovery)
    {
        _isGroggy = false;
        _isGroggyInvulnerable = false;
        StopGroggyPose(true);
        StopGroggyVisual(true);
        OnGroggyExited();
        GroggyStateExited?.Invoke(this);

        if (_enemyData != null && _enemyData.ResetGroggyGaugeOnExit)
            _currentGroggyGauge = 0f;

        if (!allowRecovery)
            return;

        if (_enemyData != null && !_enemyData.UsesGroggyRecovery)
            return;

        if (gameObject.activeInHierarchy)
            _groggyRecoveryRoutine = StartCoroutine(GroggyRecoveryRoutine());
    }

    protected virtual IEnumerator GroggyRecoveryRoutine()
    {
        if (_isCaptured)
            yield break;

        _isRecoveringFromGroggy = true;
        OnGroggyRecoveryStarted();

        float recoveryDuration = _enemyData != null ? _enemyData.GroggyRecoveryDuration : 5f;
        float targetHealth = _enemyData != null ? _enemyData.GroggyRecoveryTargetHealth : _maxHealth * 0.5f;
        float startHealth = _currentHealth;
        float elapsed = 0f;

        if (recoveryDuration <= 0f)
        {
            _currentHealth = Mathf.Max(_currentHealth, targetHealth);
            _isRecoveringFromGroggy = false;
            _groggyRecoveryRoutine = null;
            OnGroggyRecoveryCompleted();
            yield break;
        }

        while (elapsed < recoveryDuration)
        {
            if (_isGroggy)
            {
                _isRecoveringFromGroggy = false;
                _groggyRecoveryRoutine = null;
                yield break;
            }

            if (_isCaptured)
            {
                _isRecoveringFromGroggy = false;
                _groggyRecoveryRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoveryDuration);
            _currentHealth = Mathf.Lerp(startHealth, targetHealth, t);
            yield return null;
        }

        _currentHealth = Mathf.Max(_currentHealth, targetHealth);
        _isRecoveringFromGroggy = false;
        _groggyRecoveryRoutine = null;
        OnGroggyRecoveryCompleted();
    }

    protected virtual void OnGroggyEntered(DamageData damageData) { }

    protected virtual void OnGroggyExited() { }

    protected virtual void OnGroggyRecoveryStarted() { }

    protected virtual void OnGroggyRecoveryCompleted() { }

    public virtual void ForceExitGroggy(bool allowRecovery)
    {
        if (!_isGroggy)
            return;

        if (_groggyRoutine != null)
        {
            StopCoroutine(_groggyRoutine);
            _groggyRoutine = null;
        }

        ExitGroggy(allowRecovery);
    }

    protected virtual void StopRecoveryOnDamage()
    {
        if (_groggyRecoveryRoutine == null)
            return;

        StopCoroutine(_groggyRecoveryRoutine);
        _groggyRecoveryRoutine = null;
        _isRecoveringFromGroggy = false;
    }

    protected virtual void StartGroggyVisual()
    {
        if (_spriteRenderer == null)
            return;

        StopGroggyVisual(false);
        if (gameObject.activeInHierarchy)
            _groggyVisualRoutine = StartCoroutine(GroggyVisualRoutine());
    }

    protected virtual void StopGroggyVisual(bool restoreColor)
    {
        if (_groggyVisualRoutine != null)
        {
            StopCoroutine(_groggyVisualRoutine);
            _groggyVisualRoutine = null;
        }

        if (restoreColor && _spriteRenderer != null)
            _spriteRenderer.color = _originalColor;
    }

    protected virtual IEnumerator GroggyVisualRoutine()
    {
        Color groggyColor = Color.yellow;

        while (_isGroggy)
        {
            float t = Mathf.PingPong(Time.time * 2f, 1f);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            _spriteRenderer.color = Color.Lerp(_originalColor, groggyColor, easedT);
            yield return null;
        }

        _groggyVisualRoutine = null;
    }

    public virtual bool TryHandleGroggyPierceInteraction()
    {
        if (!_isGroggy || _enemyData == null)
            return false;

        switch (_enemyData.GroggyRightClickAction)
        {
            case GroggyRightClickActionType.Capture:
                return CanBeCaptured;
            case GroggyRightClickActionType.EmbeddedAttack:
                if (!UsesEmbeddedAttackMechanic)
                    return false;

                OnEmbeddedAttackRequested();
                return true;
            case GroggyRightClickActionType.None:
            default:
                return false;
        }
    }

    protected virtual void OnEmbeddedAttackRequested() { }

    public virtual void SetCaptured(bool isCaptured)
    {
        _isCaptured = isCaptured;

        if (_isCaptured)
        {
            StopGroggyTimerOnly();
            _isGroggyInvulnerable = false;
            _isRecoveringFromGroggy = false;
            return;
        }

        if (_isGroggy && gameObject.activeInHierarchy)
            RestartGroggyHold();
    }

    public virtual void SetPierced(bool isPierced)
    {
        _isPierced = isPierced;
    }

    public virtual void ExecuteDeath(Vector2 knockbackForce)
    {
        if (IsDead) return;

        _currentHealth = 0f;
        Die(knockbackForce);
    }

    public virtual void ForceEnterGroggy(Vector2 knockbackForce)
    {
        if (IsDead || _isCaptured || _isGroggy)
            return;

        EnterGroggy(new DamageData
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

    protected virtual void Die(Vector2 knockbackForce)
    {
        StopGroggyRoutines();
        _hasHitWallAfterDeath = false;
        _hasTouchedSurfaceAfterDeath = false;

        int weaponLayer = LayerMask.NameToLayer("Weapon");
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.layer == weaponLayer)
            {
                child.SetParent(null);
            }
        }

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic; 
            _rb.freezeRotation = false; 
            _rb.linearDamping = 1.5f;
            _rb.angularDamping = 1.0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(knockbackForce, ForceMode2D.Impulse); 
            float torqueDir = knockbackForce.x > 0 ? -1f : 1f;
            _rb.AddTorque(torqueDir * 40f, ForceMode2D.Impulse);
        }

        int corpseLayer = LayerMask.NameToLayer("Corpse");
        if (corpseLayer != -1) gameObject.layer = corpseLayer;

        StartCoroutine(DeathSequenceRoutine());
    }

    protected virtual IEnumerator DeathSequenceRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (_rb != null)
        {
            float collisionTimeout = _deathCollisionWaitTimeout;
            while (!_hasTouchedSurfaceAfterDeath
                   && _rb.linearVelocity.sqrMagnitude > 0.5f
                   && collisionTimeout > 0f)
            {
                collisionTimeout -= Time.deltaTime;
                yield return null;
            }

            float settleTimeout = _deathSettleTimeout;
            while (_rb.linearVelocity.sqrMagnitude > 0.5f && settleTimeout > 0f)
            {
                settleTimeout -= Time.deltaTime;
                yield return null;
            }
        }
        yield return new WaitForSeconds(0.5f);
        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
        if (_collider != null) _collider.enabled = false;

        float fadeTime = 1.2f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 0.8f;
        Color startColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            if (_spriteRenderer != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                _spriteRenderer.color = c;
            }

            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        StopGroggyRoutines();

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;

        transform.rotation = _originalRotation;
        _blinkRoutine = null;
    }

    protected virtual void StopGroggyRoutines()
    {
        if (_groggyRoutine != null)
        {
            StopCoroutine(_groggyRoutine);
            _groggyRoutine = null;
        }

        if (_groggyRecoveryRoutine != null)
        {
            StopCoroutine(_groggyRecoveryRoutine);
            _groggyRecoveryRoutine = null;
        }

        StopGroggyVisual(false);
        StopGroggyPose(false);
        _isGroggy = false;
        _isGroggyInvulnerable = false;
        _isRecoveringFromGroggy = false;
        _isCaptured = false;
        _isPierced = false;
        _currentGroggyGauge = 0f;
    }

    protected virtual void StartGroggyPose()
    {
        StopGroggyPose(false);
        if (gameObject.activeInHierarchy)
            _groggyPoseRoutine = StartCoroutine(GroggyPoseRoutine(GetGroggyLeanRotation()));
    }

    protected virtual void StopGroggyPose(bool restoreRotation)
    {
        if (_groggyPoseRoutine != null)
        {
            StopCoroutine(_groggyPoseRoutine);
            _groggyPoseRoutine = null;
        }

        if (restoreRotation && gameObject.activeInHierarchy)
            _groggyPoseRoutine = StartCoroutine(GroggyPoseRoutine(_originalRotation));
        else if (restoreRotation)
            transform.rotation = _originalRotation;
    }

    protected virtual Quaternion GetGroggyLeanRotation()
    {
        float direction = _spriteRenderer != null && _spriteRenderer.flipX ? 1f : -1f;
        return _originalRotation * Quaternion.Euler(0f, 0f, _groggyLeanAngle * direction);
    }

    protected virtual IEnumerator GroggyPoseRoutine(Quaternion targetRotation)
    {
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        float safeDuration = Mathf.Max(0.01f, _groggyPoseDuration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
            yield return null;
        }

        transform.rotation = targetRotation;
        _groggyPoseRoutine = null;
    }

    protected virtual void StopGroggyTimerOnly()
    {
        if (_groggyRoutine != null)
        {
            StopCoroutine(_groggyRoutine);
            _groggyRoutine = null;
        }

        if (_groggyRecoveryRoutine != null)
        {
            StopCoroutine(_groggyRecoveryRoutine);
            _groggyRecoveryRoutine = null;
        }
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsDead)
            TryDealContactDamage(collision.collider);

        if (IsDead && !_hasHitWallAfterDeath && _rb != null && _rb.bodyType == RigidbodyType2D.Dynamic)
        {
            _hasTouchedSurfaceAfterDeath = true;

            if (collision.relativeVelocity.sqrMagnitude > 25f)
            {
                _hasHitWallAfterDeath = true;

                if (EventBus.Instance != null)
                {
                    EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Medium });
                    EventBus.Instance.Publish(new HitStopEvent { Duration = 0.35f });
                }
            }
        }
    }

    protected virtual IEnumerator BlinkRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.white;
            yield return new WaitForSecondsRealtime(0.05f);
            _spriteRenderer.color = _originalColor;
        }
        _blinkRoutine = null;
    }
}
