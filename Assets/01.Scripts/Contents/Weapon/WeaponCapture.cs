using UnityEngine;

using System.Collections.Generic;

public class WeaponCapture : MonoBehaviour
{
    private readonly List<EnemyBase> _capturedEnemies = new List<EnemyBase>();
    private Transform _weaponTransform;
    public bool HasCapturedEnemy => _capturedEnemies.Count > 0;
    public int CapturedCount => _capturedEnemies.Count;

    private void Awake()
    {
        _weaponTransform = transform;
    }

    public void BindEnemy(Transform enemyTransform)
    {
        if (enemyTransform == null) return;
        enemyTransform.SetParent(_weaponTransform);
        enemyTransform.localPosition = new Vector3(
            UnityEngine.Random.Range(-0.8f, 0.8f),
            enemyTransform.localPosition.y,
            enemyTransform.localPosition.z
        );
        if (enemyTransform.TryGetComponent<EnemyBase>(out var enemy))
        {
            if (!_capturedEnemies.Contains(enemy))
                _capturedEnemies.Add(enemy);

            enemy.SetCaptured(true);
        }
        if (enemyTransform.TryGetComponent<Rigidbody2D>(out var rb))
            rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void UnbindAll(bool forcePhysicsRestore = false)
    {
        foreach (var enemy in _capturedEnemies)
        {
            if (enemy == null) continue;

            Transform enemyTransform = enemy.transform;
            enemyTransform.SetParent(null);
            enemy.SetCaptured(false);

            if (enemyTransform.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                if (forcePhysicsRestore)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }

            if (enemyTransform.TryGetComponent<Collider2D>(out var col))
            {
                col.enabled = true;
                col.isTrigger = false;
            }
        }
        _capturedEnemies.Clear();
    }

    public void ExecuteCapturedEnemies(Vector2 knockbackForce)
    {
        foreach (var enemy in _capturedEnemies)
        {
            if (enemy == null) continue;

            enemy.transform.SetParent(null);
            enemy.ExecuteDeath(knockbackForce);
        }

        _capturedEnemies.Clear();
    }

    public IReadOnlyList<EnemyBase> GetCapturedEnemies() => _capturedEnemies;
}
