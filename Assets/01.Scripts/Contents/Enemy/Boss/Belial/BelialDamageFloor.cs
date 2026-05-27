using UnityEngine;

public sealed class BelialDamageFloor : MonoBehaviour
{
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private LayerMask _playerLayer;

    private float _lastDamageTime = -999f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (_damage <= 0)
            return;

        if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (Time.time < _lastDamageTime + Mathf.Max(0.05f, _damageInterval))
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();

        if (damageable == null || damageable.Team != TeamType.Player || damageable.IsDead)
            return;

        _lastDamageTime = Time.time;

        damageable.TakeDamage(new DamageData
        {
            Damage = _damage,
            GroggyDamage = 0f,
            AttackerTeam = TeamType.Enemy,
            HitPoint = other.ClosestPoint(transform.position),
            KnockbackForce = Vector2.zero,
            IsPiercing = false,
            IsExecution = false,
            AttackKind = WeaponAttackKind.None
        });
    }
}
