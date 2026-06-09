using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDeathSequence : MonoBehaviour, IDeathSequence
{
    private EnemyBase _owner;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private SpriteRenderer _spriteRenderer;
    private float _deathCollisionWaitTimeout;
    private float _deathSettleTimeout;

    public void Configure(
        EnemyBase owner,
        Rigidbody2D rb,
        Collider2D targetCollider,
        SpriteRenderer spriteRenderer,
        float deathCollisionWaitTimeout,
        float deathSettleTimeout)
    {
        _owner = owner;
        _rb = rb;
        _collider = targetCollider;
        _spriteRenderer = spriteRenderer;
        _deathCollisionWaitTimeout = Mathf.Max(0f, deathCollisionWaitTimeout);
        _deathSettleTimeout = Mathf.Max(0f, deathSettleTimeout);
    }

    public void Play(Vector2 knockbackForce)
    {
        if (_owner == null)
            return;

        StartCoroutine(PlayRoutine(knockbackForce));
    }

    private IEnumerator PlayRoutine(Vector2 knockbackForce)
    {
        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.freezeRotation = false;
            _rb.linearDamping = 1.5f;
            _rb.angularDamping = 1.0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(knockbackForce, ForceMode2D.Impulse);
            float torqueDir = knockbackForce.x > 0 ? -1f : 1f;
            _rb.AddTorque(torqueDir * 40f, ForceMode2D.Impulse);
        }

        int corpseLayer = LayerMask.NameToLayer("Corpse");
        if (corpseLayer != -1)
            gameObject.layer = corpseLayer;

        yield return new WaitForSeconds(0.5f);
        if (_rb != null)
        {
            float collisionTimeout = _deathCollisionWaitTimeout;
            while (!_owner.HasTouchedSurfaceAfterDeath
                   && _rb.linearVelocity.sqrMagnitude > 0.5f
                   && collisionTimeout > 0f)
            {
                collisionTimeout -= Time.deltaTime;
                yield return null;
            }

            float settleTimeout = _deathSettleTimeout;
            while (_rb.linearVelocity.sqrMagnitude > 0.5f && settleTimeout > 0f)
            {
                settleTimeout -= Time.deltaTime;
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

        if (_collider != null)
            _collider.enabled = false;

        float fadeTime = 1.2f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 0.8f;
        Color startColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            if (_spriteRenderer != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                _spriteRenderer.color = c;
            }

            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
