using UnityEngine;

[DisallowMultipleComponent]
public class EnemyContactDamage : MonoBehaviour
{
    private EnemyBase _owner;
    private int _contactDamage;

    public void Configure(EnemyBase owner, int contactDamage)
    {
        _owner = owner;
        _contactDamage = Mathf.Max(0, contactDamage);
    }

    public void TryDealContactDamage(Collider2D other)
    {
        if (_owner == null || other == null)
            return;
        if (_owner.IsDead || _owner.IsCaptured || _owner.IsPierced)
            return;
        if (_contactDamage <= 0)
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
}
