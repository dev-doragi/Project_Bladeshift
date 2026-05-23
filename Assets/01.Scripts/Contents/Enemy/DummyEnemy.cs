using System.Collections;
using UnityEngine;

public class DummyEnemy : EnemyBase
{
    private bool _isDowned;
    private int _originalLayer;

    protected override void Awake()
    {
        base.Awake();
        _originalLayer = gameObject.layer;
    }

    public override bool CanBeCapturedByPierce()
    {
        if (Data != null) return base.CanBeCapturedByPierce();
        return true;
    }

    public override bool ShouldPierceStick()
    {
        if (Data != null) return base.ShouldPierceStick();
        return true;
    }

    public override bool ShouldPiercePassThrough()
    {
        if (Data != null) return base.ShouldPiercePassThrough();
        return false;
    }

    public override bool CanExecuteCaptureFinisher()
    {
        if (Data != null) return base.CanExecuteCaptureFinisher();
        return true;
    }

    public override void TakeDamage(DamageData damageData)
    {
        if (_isDowned) return;

        _currentHealth -= damageData.Damage;

        if (_rb != null && damageData.KnockbackForce.sqrMagnitude > 0.0001f)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(damageData.KnockbackForce, ForceMode2D.Impulse);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Weak });
        }

        if (_blinkRoutine == null && gameObject.activeInHierarchy)
            _blinkRoutine = StartCoroutine(BlinkRoutine());

        if (_currentHealth <= 0f)
            Die(damageData.KnockbackForce);
    }

    protected override void Die(Vector2 knockbackForce)
    {
        if (_isDowned) return;

        _isDowned = true;
        _hasHitWallAfterDeath = false;

        int weaponLayer = LayerMask.NameToLayer("Weapon");
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.layer == weaponLayer)
                child.SetParent(null);
        }

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.freezeRotation = false;
            _rb.linearDamping = 1.5f;
            _rb.angularDamping = 1.0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.AddForce(knockbackForce, ForceMode2D.Impulse);

            float torqueDir = knockbackForce.x > 0 ? -1f : 1f;
            _rb.AddTorque(torqueDir * 40f, ForceMode2D.Impulse);
        }

        int corpseLayer = LayerMask.NameToLayer("Corpse");
        if (corpseLayer != -1) gameObject.layer = corpseLayer;

        StartCoroutine(DummyReviveRoutine());
    }

    private IEnumerator DummyReviveRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        if (_rb != null)
        {
            float timeout = 3f;
            while (_rb.linearVelocity.sqrMagnitude > 0.2f && timeout > 0f)
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

        float reviveDuration = 0.6f;
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.identity;

        while (elapsed < reviveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / reviveDuration;
            float curve = 1f - Mathf.Pow(1f - t, 3f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curve);
            yield return null;
        }

        transform.rotation = targetRotation;

        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.freezeRotation = true;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.linearDamping = 10f;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        if (_collider != null) _collider.enabled = true;

        if (_originalLayer != -1) gameObject.layer = _originalLayer;

        _currentHealth = _maxHealth;
        _isDowned = false;
    }
}
