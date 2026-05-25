using UnityEngine;

using System.Collections.Generic;

public class WeaponCapture : MonoBehaviour
{
    private readonly List<EnemyBase> _capturedEnemies = new List<EnemyBase>();
    private Transform _weaponTransform;
    public bool HasCapturedEnemy => _capturedEnemies.Count > 0;
    public bool HasCapturedTarget => _capturedEnemies.Count > 0;
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
            enemy.SetPierced(true);
        }
        if (enemyTransform.TryGetComponent<Rigidbody2D>(out var rb))
            rb.bodyType = RigidbodyType2D.Kinematic;
        SetEnemyCollidersEnabled(enemyTransform, false);
    }

    public void UnbindAll(bool forcePhysicsRestore = false)
    {
        foreach (var enemy in _capturedEnemies)
        {
            if (enemy == null) continue;

            Transform enemyTransform = enemy.transform;
            enemyTransform.SetParent(null);
            enemy.SetCaptured(false);
            enemy.SetPierced(false);

            if (enemyTransform.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                if (forcePhysicsRestore)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }

            SetEnemyCollidersEnabled(enemyTransform, true);
        }
        _capturedEnemies.Clear();
    }

    public void ForceReleaseCapturedTarget()
    {
        if (!HasCapturedTarget) return;
        UnbindAll(forcePhysicsRestore: true);
    }

    public void ExecuteCapturedEnemies(Vector2 knockbackForce)
    {
        foreach (var enemy in _capturedEnemies)
        {
            if (enemy == null) continue;

            enemy.transform.SetParent(null);
            enemy.SetCaptured(false);
            enemy.SetPierced(false);

            SetEnemyCollidersEnabled(enemy.transform, true);

            enemy.ExecuteDeath(knockbackForce);
        }

        _capturedEnemies.Clear();
    }

    public IReadOnlyList<EnemyBase> GetCapturedEnemies() => _capturedEnemies;

    private void SetEnemyCollidersEnabled(Transform enemyTransform, bool enabled)
    {
        if (enemyTransform == null)
            return;

        Collider2D[] colliders = enemyTransform.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            col.enabled = enabled;
            if (enabled)
                col.isTrigger = false;
        }
    }
}
