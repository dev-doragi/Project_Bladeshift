using System.Collections;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [SerializeField] protected EnemyData _enemyData;
    [SerializeField] protected float _maxHealth = 50f;
    protected float _currentHealth;

    protected Rigidbody2D _rb;
    protected SpriteRenderer _spriteRenderer;
    protected Collider2D _collider;

    protected Color _originalColor;
    protected Coroutine _blinkRoutine;
    protected Coroutine _groggyRoutine;
    protected Coroutine _groggyRecoveryRoutine;
    protected bool _isGroggy;
    protected bool _isGroggyInvulnerable;
    protected bool _isRecoveringFromGroggy;
    protected bool _hasHitWallAfterDeath = false;

    public virtual TeamType Team => TeamType.Enemy;
    public virtual bool IsDead => _currentHealth <= 0f;
    public EnemyData Data => _enemyData;
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsGroggy => _isGroggy;
    public bool IsGroggyInvulnerable => _isGroggyInvulnerable;
    public bool IsRecoveringFromGroggy => _isRecoveringFromGroggy;
    public virtual EnemyCategory Category => _enemyData != null ? _enemyData.Category : EnemyCategory.Normal;
    public virtual float Weight => _enemyData != null ? _enemyData.Weight : 0f;
    public virtual bool CanBeCaptured => _enemyData != null && _enemyData.CanBeCaptured;
    public virtual float CaptureWeight => _enemyData != null ? _enemyData.CaptureWeight : 0f;
    public virtual bool CanBeExecuted => _enemyData == null || _enemyData.CanBeExecuted;
    public virtual GroggyRightClickActionType GroggyRightClickAction => _enemyData != null ? _enemyData.GroggyRightClickAction : GroggyRightClickActionType.None;
    public virtual bool UsesEmbeddedAttackMechanic => _enemyData != null && _enemyData.UsesEmbeddedAttackMechanic;
    public virtual int RequiredEmbeddedAttackCount => _enemyData != null ? _enemyData.RequiredEmbeddedAttackCount : 0;

    protected virtual void Awake()
    {
        if (_enemyData != null)
            _maxHealth = _enemyData.MaxHealth;

        _currentHealth = _maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;

        if (_rb != null)
        {
            _rb.freezeRotation = true;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.linearDamping = 10f;
        }
    }

    public virtual void TakeDamage(DamageData damageData)
    {
        if (IsDead) return;
        if (_isGroggyInvulnerable) return;

        float previousHealth = _currentHealth;

        _currentHealth -= damageData.Damage;

        if (_rb != null && damageData.KnockbackForce.sqrMagnitude > 0.0001f)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(damageData.KnockbackForce, ForceMode2D.Impulse);
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
            Die(damageData.KnockbackForce);
        }
    }

    public virtual bool ShouldPiercingAttackStick()
    {
        if (_enemyData == null)
            return true;

        return _enemyData.ShouldPiercingAttackStick(_isGroggy);
    }

    protected virtual bool TryHandleGroggyAfterDamage(float previousHealth, DamageData damageData)
    {
        if (_enemyData == null || !_enemyData.UsesGroggy)
            return false;

        if (_isGroggy)
        {
            if (_currentHealth <= 0f)
            {
                if (_enemyData.CanDieFromBasicAttackWhileGroggy)
                    return false;

                _currentHealth = 1f;
            }

            RestartGroggyHold();
            return true;
        }

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

    protected virtual void EnterGroggy(DamageData damageData)
    {
        _isGroggy = true;
        _isGroggyInvulnerable = true;
        _isRecoveringFromGroggy = false;

        if (_groggyRecoveryRoutine != null)
        {
            StopCoroutine(_groggyRecoveryRoutine);
            _groggyRecoveryRoutine = null;
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        OnGroggyEntered(damageData);
        RestartGroggyHold();
    }

    protected virtual void RestartGroggyHold()
    {
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

        _groggyRoutine = null;
        ExitGroggyAndRecover();
    }

    protected virtual void ExitGroggyAndRecover()
    {
        _isGroggy = false;
        _isGroggyInvulnerable = false;
        OnGroggyExited();

        if (gameObject.activeInHierarchy)
            _groggyRecoveryRoutine = StartCoroutine(GroggyRecoveryRoutine());
    }

    protected virtual IEnumerator GroggyRecoveryRoutine()
    {
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

    protected virtual void Die(Vector2 knockbackForce)
    {
        StopGroggyRoutines();
        _hasHitWallAfterDeath = false;

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
            float timeout = 4f;
            while (_rb.linearVelocity.sqrMagnitude > 0.5f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
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

        transform.rotation = Quaternion.identity;
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

        _isGroggy = false;
        _isGroggyInvulnerable = false;
        _isRecoveringFromGroggy = false;
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDead && !_hasHitWallAfterDeath && _rb != null && _rb.bodyType == RigidbodyType2D.Dynamic)
        {
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
