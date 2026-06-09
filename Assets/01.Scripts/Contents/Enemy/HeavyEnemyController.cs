using UnityEngine;

[RequireComponent(typeof(ChargeAttackBehaviour))]
public class HeavyEnemyController : EnemyController
{
    private HeavyEnemyMovementData _heavyMovementData;
    private SpriteAfterImage _afterImage;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _bodyCollider;
    private ChargeAttackBehaviour _chargeBehaviour;

    public bool IsCharging => _chargeBehaviour != null && _chargeBehaviour.IsCharging;

    protected override void Awake()
    {
        base.Awake();

        TryGetMovementData(out _heavyMovementData);
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _bodyCollider = GetComponent<Collider2D>();
        _afterImage = GetComponent<SpriteAfterImage>();
        if (_afterImage == null)
            _afterImage = gameObject.AddComponent<SpriteAfterImage>();

        _chargeBehaviour = GetComponent<ChargeAttackBehaviour>();
        if (_chargeBehaviour == null)
            _chargeBehaviour = gameObject.AddComponent<ChargeAttackBehaviour>();

        _chargeBehaviour.Initialize(
            EnemyBase,
            Rigidbody,
            _bodyCollider,
            _spriteRenderer,
            _heavyMovementData,
            CanMove,
            () => IsTargetDetected,
            () => Target,
            StopMovement);

        _afterImage.Initialize(() => IsCharging, _spriteRenderer);
        if (_heavyMovementData != null)
            _afterImage.SetAfterImageColor(_heavyMovementData.ChargeAfterImageColor);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (EnemyBase != null)
            EnemyBase.GroggyStateEntered += _chargeBehaviour.HandleGroggyStateEntered;
    }

    protected override void OnDisable()
    {
        _chargeBehaviour?.Cleanup();

        if (EnemyBase != null && _chargeBehaviour != null)
            EnemyBase.GroggyStateEntered -= _chargeBehaviour.HandleGroggyStateEntered;

        base.OnDisable();
    }

    protected override void FixedUpdate()
    {
        if (_heavyMovementData == null || _chargeBehaviour == null)
        {
            base.FixedUpdate();
            return;
        }

        if (_chargeBehaviour.IsBehaviourActive)
        {
            _chargeBehaviour.TickActive();
            return;
        }

        base.FixedUpdate();
        _chargeBehaviour.TickIdle();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        _chargeBehaviour?.HandleCollisionEnter2D(collision);
    }
}
