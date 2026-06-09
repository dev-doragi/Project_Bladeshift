using UnityEngine;

[System.Serializable]
public sealed class EnemyHealth
{
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _currentHealth;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0f;

    public void Initialize(float maxHealth)
    {
        _maxHealth = Mathf.Max(1f, maxHealth);
        _currentHealth = _maxHealth;
    }

    public float ApplyDamage(float damage)
    {
        float previous = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - Mathf.Max(0f, damage));
        return previous;
    }

    public void SetCurrent(float value)
    {
        _currentHealth = Mathf.Clamp(value, 0f, _maxHealth);
    }
}
