using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [FormerlySerializedAs("_maxHealth")]
    [SerializeField] private int _maxHp = 3;
    [FormerlySerializedAs("_currentHealth")]
    [SerializeField] private int _currentHp;

    private bool _isDead;

    [Header("Death")]
    [SerializeField] private float _deathResultDelay = 1.2f;
    private Coroutine _deathRoutine;

    [Header("Invincibility")]
    [SerializeField] private float _invincibleDuration = 1.0f;
    [SerializeField] private float _respawnInvincibleDuration = 0.8f;
    [SerializeField] private string _invincibleLayer = "Invincible";
    [SerializeField, Range(0f, 1f)] private float _invincibleBlinkMinAlpha = 0.35f;
    [SerializeField, Min(0.1f)] private float _invincibleBlinkSpeed = 18f;

    private int _originalLayer;
    private bool _isInvincible;
    private Coroutine _invincibleRoutine;
    private SpriteRenderer _bodySpriteRenderer;
    private Color _baseSpriteColor = Color.white;

    public TeamType Team => TeamType.Player;
    public int CurrentHp => _currentHp;
    public int MaxHp => _maxHp;
    public bool IsDead => _isDead;
    public bool IsInvincible => _isInvincible;

    private void Awake()
    {
        _maxHp = Mathf.Max(1, _maxHp);
        _currentHp = _maxHp;
        _isDead = false;
        _isInvincible = false;
        _originalLayer = gameObject.layer;
        _bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_bodySpriteRenderer != null)
            _baseSpriteColor = _bodySpriteRenderer.color;

        PublishHpChanged();
    }

    public void TakeDamage(DamageData damageData)
    {
        if (_isDead)
            return;
        if (_isInvincible)
            return;

        int damage = Mathf.RoundToInt(damageData.Damage);
        if (damage <= 0)
            return;

        if (_currentHp <= 0)
            return;

        _currentHp = Mathf.Max(0, _currentHp - damage);

        EventBus.Instance.Publish(new PlayerDamagedEvent
        {
            Damage = damage,
            CurrentHp = _currentHp,
            MaxHp = _maxHp
        });

        PublishHpChanged();

        if (_currentHp <= 0)
        {
            Die();
            return;
        }

        StartInvincible(_invincibleDuration);
    }

    public void Heal(int amount)
    {
        if (_isDead)
            return;

        if (amount <= 0)
            return;

        int nextHp = Mathf.Min(_maxHp, _currentHp + amount);
        if (nextHp == _currentHp)
            return;

        _currentHp = nextHp;

        PublishHpChanged();
    }

    public void ResetHp()
    {
        ReviveForRespawn(false);
    }

    public void ReviveForRespawn(bool applyRespawnInvincibility = true)
    {
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        StopInvincible();
        _isDead = false;
        _currentHp = _maxHp;

        PublishHpChanged();

        if (applyRespawnInvincibility)
        {
            StartInvincible(_respawnInvincibleDuration);
        }
    }

    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        StopInvincible();

        EventBus.Instance.Publish(new PlayerDeathStartedEvent());

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);

        _deathRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(_deathResultDelay);

        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.RestartCurrentSceneFromRespawnPoint();
        }
        else
        {
            Debug.LogWarning("[PlayerHealth] RespawnManager instance not found. Respawn skipped.", this);
        }

        _deathRoutine = null;
    }

    private void PublishHpChanged()
    {
        EventBus.Instance.Publish(new PlayerHpChangedEvent
        {
            CurrentHp = _currentHp,
            MaxHp = _maxHp
        });
    }

    private void StartInvincible(float duration)
    {
        StopInvincible();
        _invincibleRoutine = StartCoroutine(InvincibleRoutine(Mathf.Max(0f, duration)));
    }

    private void StopInvincible()
    {
        _isInvincible = false;
        gameObject.layer = _originalLayer;
        SetInvincibleVisualAlpha(1f);

        if (_invincibleRoutine != null)
        {
            StopCoroutine(_invincibleRoutine);
            _invincibleRoutine = null;
        }
    }

    private IEnumerator InvincibleRoutine(float duration)
    {
        _isInvincible = true;

        int invincibleLayer = LayerMask.NameToLayer(_invincibleLayer);
        if (invincibleLayer >= 0)
            gameObject.layer = invincibleLayer;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = (Mathf.Sin(Time.time * _invincibleBlinkSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(_invincibleBlinkMinAlpha, 1f, t);
            SetInvincibleVisualAlpha(alpha);
            yield return null;
        }

        _isInvincible = false;
        gameObject.layer = _originalLayer;
        SetInvincibleVisualAlpha(1f);
        _invincibleRoutine = null;
    }

    private void SetInvincibleVisualAlpha(float alpha)
    {
        if (_bodySpriteRenderer == null)
            return;

        Color c = _baseSpriteColor;
        c.a = Mathf.Clamp01(alpha);
        _bodySpriteRenderer.color = c;
    }
}
