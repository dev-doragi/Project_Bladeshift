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

    public TeamType Team => TeamType.Player;
    public int CurrentHp => _currentHp;
    public int MaxHp => _maxHp;
    public bool IsDead => _isDead;

    private void Awake()
    {
        _maxHp = Mathf.Max(1, _maxHp);
        _currentHp = _maxHp;
        _isDead = false;

        PublishHpChanged();
    }

    public void TakeDamage(DamageData damageData)
    {
        if (_isDead)
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
            Die();
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
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        _isDead = false;
        _currentHp = _maxHp;

        PublishHpChanged();
    }

    public void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

        EventBus.Instance.Publish(new PlayerDeathStartedEvent());

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);

        _deathRoutine = StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(_deathResultDelay);

        EventBus.Instance.Publish(new StageFailedEvent { StageIndex = 0 });

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
}