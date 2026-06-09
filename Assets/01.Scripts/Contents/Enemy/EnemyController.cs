using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyMovementData _movementData;

    [FormerlySerializedAs("_moveSpeed")]
    [SerializeField, HideInInspector] private float _legacyMoveSpeed = 2.5f;
    [FormerlySerializedAs("_detectRange")]
    [SerializeField, HideInInspector] private float _legacyDetectRange = 8f;
    [FormerlySerializedAs("_stopDistance")]
    [SerializeField, HideInInspector] private float _legacyStopDistance = 0.8f;
    [FormerlySerializedAs("_keepChasingAfterDetection")]
    [SerializeField, HideInInspector] private bool _legacyKeepChasingAfterDetection;
    [FormerlySerializedAs("_groundLayer")]
    [SerializeField, HideInInspector] private LayerMask _legacyLineOfSightBlockerLayer;

    [Header("References")]
    [FormerlySerializedAs("_playerTransform")]
    [SerializeField] private Transform _targetOverride;
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private EnemyBase _enemyBase;

    private Transform _target;
    private bool _isTargetDetected;
    private bool _hasDetectedTarget;
    private Vector2 _lastObservedTargetPosition;
    private EnemyDetectionState _detectionState;

    protected EnemyMovementData MovementData => _movementData;
    protected Transform Target => _target;
    protected Rigidbody2D Rigidbody => _rigidbody;
    protected EnemyBase EnemyBase => _enemyBase;
    protected float MoveSpeed => _movementData != null ? _movementData.MoveSpeed : _legacyMoveSpeed;
    protected float DetectRange => _movementData != null ? _movementData.DetectRange : _legacyDetectRange;
    protected float StopDistance => _movementData != null ? _movementData.StopDistance : _legacyStopDistance;
    protected bool KeepChasingAfterDetection => _movementData != null
        ? _movementData.KeepChasingAfterDetection
        : _legacyKeepChasingAfterDetection;
    protected bool RequiresLineOfSight => _movementData == null || _movementData.RequiresLineOfSight;
    protected LayerMask LineOfSightBlockerLayer => _movementData != null
        ? _movementData.LineOfSightBlockerLayer
        : _legacyLineOfSightBlockerLayer;

    public bool IsTargetDetected => _isTargetDetected;
    public bool HasDetectedTarget => _hasDetectedTarget;
    public Vector2 LastObservedTargetPosition => _lastObservedTargetPosition;
    public EnemyDetectionState CurrentDetectionState => _detectionState;

    protected virtual void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();

        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();
    }

    protected virtual void OnEnable()
    {
        EventBus.Instance.Subscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);
    }

    protected virtual void OnDisable()
    {
        EventBus.Instance.Unsubscribe<PlayerSpawnedEvent>(HandlePlayerSpawned);
    }

    protected virtual void Start()
    {
        if (_targetOverride != null)
        {
            SetTarget(_targetOverride);
            return;
        }

        PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
        if (foundPlayer != null)
            SetTarget(foundPlayer.transform);
    }

    protected virtual void FixedUpdate()
    {
        UpdateDetectionState();

        if (!CanMove())
        {
            if (_enemyBase == null || !_enemyBase.IsDead)
                StopMovement();

            return;
        }

        if (!TryGetMoveTarget(out Vector2 moveTarget))
        {
            StopMovement();
            return;
        }

        Vector2 desiredVelocity = CalculateMoveVelocity(moveTarget);
        ApplyMoveVelocity(desiredVelocity);
    }

    protected virtual bool CanMove()
    {
        if (_rigidbody == null)
            return false;

        if (_enemyBase != null && (_enemyBase.IsDead || _enemyBase.IsCaptured || _enemyBase.IsGroggy))
            return false;

        return true;
    }

    protected virtual bool TryGetMoveTarget(out Vector2 moveTarget)
    {
        moveTarget = default;

        if (_target == null)
            return false;

        bool canUseTarget = KeepChasingAfterDetection
            ? _hasDetectedTarget
            : _isTargetDetected;

        if (!canUseTarget)
            return false;

        moveTarget = _target.position;
        return true;
    }

    protected virtual Vector2 CalculateMoveVelocity(Vector2 moveTarget)
    {
        Vector2 direction = moveTarget - (Vector2)transform.position;

        if (Mathf.Abs(direction.x) <= StopDistance)
            return new Vector2(0f, _rigidbody.linearVelocity.y);

        return new Vector2(
            Mathf.Sign(direction.x) * MoveSpeed,
            _rigidbody.linearVelocity.y);
    }

    protected virtual void ApplyMoveVelocity(Vector2 velocity)
    {
        _rigidbody.linearVelocity = velocity;
    }

    protected virtual void StopMovement()
    {
        StopMoveX();
    }

    protected virtual bool CanObserveTarget()
    {
        if (_target == null)
            return false;

        Vector2 origin = transform.position;
        Vector2 targetPosition = _target.position;
        Vector2 direction = targetPosition - origin;

        if (direction.sqrMagnitude > DetectRange * DetectRange)
            return false;

        if (!RequiresLineOfSight || direction.sqrMagnitude <= 0.0001f)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            direction.magnitude,
            LineOfSightBlockerLayer);

        return hit.collider == null;
    }

    protected virtual void OnTargetDetected(Transform target) { }

    protected virtual void OnTargetLost(Transform target, Vector2 lastObservedPosition) { }

    protected bool TryGetMovementData<TMovementData>(out TMovementData movementData)
        where TMovementData : EnemyMovementData
    {
        movementData = _movementData as TMovementData;
        return movementData != null;
    }

    protected void StopMoveX()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
    }

    private void UpdateDetectionState()
    {
        bool wasDetected = _isTargetDetected;
        _isTargetDetected = CanObserveTarget();

        if (_isTargetDetected)
        {
            _lastObservedTargetPosition = _target.position;
            _hasDetectedTarget = true;

            if (!wasDetected)
                OnTargetDetected(_target);
        }
        else if (wasDetected)
        {
            OnTargetLost(_target, _lastObservedTargetPosition);
        }

        _detectionState = new EnemyDetectionState
        {
            IsTargetDetected = _isTargetDetected,
            HasDetectedTarget = _hasDetectedTarget,
            LastObservedTargetPosition = _lastObservedTargetPosition
        };
    }

    private void HandlePlayerSpawned(PlayerSpawnedEvent evt)
    {
        if (evt.Player == null || _targetOverride != null)
            return;

        SetTarget(evt.Player.transform);
    }

    private void SetTarget(Transform target)
    {
        _target = target;
        if (_target != null)
            _lastObservedTargetPosition = _target.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectRange);

        Transform gizmoTarget = _targetOverride != null ? _targetOverride : _target;
        if (gizmoTarget == null)
            return;

        Gizmos.color = _isTargetDetected ? Color.red : Color.gray;
        Gizmos.DrawLine(transform.position, gizmoTarget.position);
    }
}
