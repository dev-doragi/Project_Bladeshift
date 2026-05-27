using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
public class RangedEnemyFloatController : MonoBehaviour
{
    [Header("Floating Physics")]
    [SerializeField, Min(0f)] private float _maxKnockbackSpeed = 4f;
    [SerializeField, Min(0f)] private float _airLinearDamping = 3f;

    [Header("Death Physics")]
    [SerializeField, Min(0f)] private float _deadGravityScale = 3f;
    [SerializeField, Min(0f)] private float _deadLinearDamping = 1.5f;

    private Rigidbody2D _rigidbody;
    private EnemyBase _enemyBase;
    private FlyingEnemyController _flyingEnemyController;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _enemyBase = GetComponent<EnemyBase>();
        _flyingEnemyController = GetComponent<FlyingEnemyController>();

        _rigidbody.gravityScale = 0f;
    }

    private void FixedUpdate()
    {
        if (_rigidbody == null)
            return;

        if (_enemyBase != null && _enemyBase.IsDead)
        {
            _rigidbody.gravityScale = _deadGravityScale;
            _rigidbody.linearDamping = _deadLinearDamping;
            return;
        }

        _rigidbody.gravityScale = 0f;
        _rigidbody.linearDamping = _airLinearDamping;

        if (_maxKnockbackSpeed <= 0f)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        float maxAllowedSpeed = _maxKnockbackSpeed;
        if (_flyingEnemyController != null && _flyingEnemyController.HasActiveMoveTarget)
            maxAllowedSpeed = Mathf.Max(maxAllowedSpeed, _flyingEnemyController.CurrentMoveSpeed);

        _rigidbody.linearVelocity = Vector2.ClampMagnitude(
            _rigidbody.linearVelocity,
            maxAllowedSpeed);
    }
}
