using UnityEngine;

[CreateAssetMenu(fileName = "EnemyProjectileAttackData", menuName = "BladeShift/Enemy/Projectile Attack Data")]
public class EnemyProjectileAttackData : ScriptableObject
{
    [Header("Projectile")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 12f;
    [SerializeField, Min(0.1f)] private float _lifeTime = 5f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _damage = 1f;
    [SerializeField, Min(0f)] private float _knockbackPower = 0f;

    [Header("Attack Timing")]
    [SerializeField, Min(0f)] private float _initialAttackDelay = 0.5f;
    [SerializeField, Min(0.05f)] private float _attackCooldown = 1.5f;
    [SerializeField, Min(0.1f)] private float _attackRange = 8f;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => _projectileSpeed;
    public float LifeTime => _lifeTime;
    public float Damage => _damage;
    public float KnockbackPower => _knockbackPower;
    public float InitialAttackDelay => _initialAttackDelay;
    public float AttackCooldown => _attackCooldown;
    public float AttackRange => _attackRange;

    private void OnValidate()
    {
        _projectileSpeed = Mathf.Max(0.01f, _projectileSpeed);
        _lifeTime = Mathf.Max(0.1f, _lifeTime);
        _damage = Mathf.Max(0f, _damage);
        _knockbackPower = Mathf.Max(0f, _knockbackPower);
        _initialAttackDelay = Mathf.Max(0f, _initialAttackDelay);
        _attackCooldown = Mathf.Max(0.05f, _attackCooldown);
        _attackRange = Mathf.Max(0.1f, _attackRange);
    }
}
