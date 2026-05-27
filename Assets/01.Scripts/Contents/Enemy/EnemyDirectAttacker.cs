using UnityEngine;

public class EnemyDirectAttacker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _spawnPoint;

    public bool TryPerformAttack(Transform target, EnemyProjectileAttackData attackData)
    {
        if (target == null)
        {
            Debug.LogError("[EnemyDirectAttacker] target is null.", this);
            return false;
        }

        if (attackData == null)
        {
            Debug.LogError("[EnemyDirectAttacker] attackData is null.", this);
            return false;
        }

        if (attackData.ProjectilePrefab == null)
        {
            Debug.LogError("[EnemyDirectAttacker] ProjectilePrefab is missing.", this);
            return false;
        }

        if (PoolManager.Instance == null)
        {
            Debug.LogError("[EnemyDirectAttacker] PoolManager.Instance is missing.", this);
            return false;
        }

        Vector3 startPosition = _spawnPoint != null ? _spawnPoint.position : transform.position;
        Vector3 targetPosition = target.position;

        GameObject projectileObject = PoolManager.Instance.Spawn(
            attackData.ProjectilePrefab.name,
            startPosition,
            Quaternion.identity);

        if (projectileObject == null)
        {
            Debug.LogError($"[EnemyDirectAttacker] Failed to spawn projectile: {attackData.ProjectilePrefab.name}", this);
            return false;
        }

        if (!projectileObject.TryGetComponent(out EnemyDirectProjectile projectile))
        {
            Debug.LogError($"[EnemyDirectAttacker] Spawned prefab has no EnemyDirectProjectile: {projectileObject.name}", projectileObject);
            PoolManager.Instance.Despawn(projectileObject);
            return false;
        }

        projectile.Initialize(attackData, startPosition, targetPosition, gameObject);
        return true;
    }
}
