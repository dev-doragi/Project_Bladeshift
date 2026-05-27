using UnityEngine;

public class FlyingEnemyController : EnemyController
{
    private FlyingEnemyMovementData _flyingMovementData;
    private EnemyRangedAttackController _rangedAttackController;
    private Vector2 _currentMoveTarget;
    private float _currentMoveSpeed;
    private float _repositionIntervalTimer;
    private bool _hasMoveTarget;

    public bool HasActiveMoveTarget => _hasMoveTarget;
    public float CurrentMoveSpeed => _currentMoveSpeed;

    protected override void Awake()
    {
        base.Awake();

        TryGetMovementData(out _flyingMovementData);
        _rangedAttackController = GetComponent<EnemyRangedAttackController>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (_rangedAttackController != null)
            _rangedAttackController.AttackPerformed += HandleAttackPerformed;
    }

    protected override void OnDisable()
    {
        if (_rangedAttackController != null)
            _rangedAttackController.AttackPerformed -= HandleAttackPerformed;

        base.OnDisable();
    }

    protected override bool TryGetMoveTarget(out Vector2 moveTarget)
    {
        moveTarget = default;

        if (_flyingMovementData == null || Target == null)
            return false;

        bool canUseTarget = KeepChasingAfterDetection
            ? HasDetectedTarget
            : IsTargetDetected;

        if (!canUseTarget)
            return false;

        bool shouldPickContinuousTarget = !_flyingMovementData.MoveAfterAttack
            && (!_hasMoveTarget || IsCurrentMoveTargetTooCloseToTarget())
            && CanPickContinuousMoveTarget();

        if (shouldPickContinuousTarget && !TrySetNextMoveTarget())
        {
            return false;
        }

        if (!_hasMoveTarget)
            return false;

        moveTarget = _currentMoveTarget;
        return true;
    }

    protected override Vector2 CalculateMoveVelocity(Vector2 moveTarget)
    {
        Vector2 currentPosition = Rigidbody.position;
        Vector2 toTarget = moveTarget - currentPosition;

        if (toTarget.sqrMagnitude <= _flyingMovementData.ArriveDistance * _flyingMovementData.ArriveDistance)
        {
            _hasMoveTarget = false;
            StartRepositionInterval();
            return Vector2.zero;
        }

        return toTarget.normalized * _currentMoveSpeed;
    }

    protected override void ApplyMoveVelocity(Vector2 velocity)
    {
        Rigidbody.linearVelocity = velocity;
    }

    protected override void StopMovement()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    protected override void OnTargetLost(Transform target, Vector2 lastObservedPosition)
    {
        _hasMoveTarget = false;
    }

    private void HandleAttackPerformed()
    {
        if (_flyingMovementData == null || !_flyingMovementData.MoveAfterAttack)
            return;

        if (!CanMove() || !CanUseCurrentTarget())
            return;

        _repositionIntervalTimer = 0f;
        TrySetNextMoveTarget();
    }

    private bool TrySetNextMoveTarget()
    {
        if (_flyingMovementData == null || Target == null)
            return false;

        if (!TryPickMoveTarget(out _currentMoveTarget))
            return false;

        Vector2 speedRange = _flyingMovementData.RepositionSpeedRange;
        _currentMoveSpeed = Random.Range(speedRange.x, speedRange.y);
        _hasMoveTarget = true;
        return true;
    }

    private bool CanUseCurrentTarget()
    {
        if (Target == null)
            return false;

        return KeepChasingAfterDetection
            ? HasDetectedTarget
            : IsTargetDetected;
    }

    private bool CanPickContinuousMoveTarget()
    {
        if (_repositionIntervalTimer <= 0f)
            return true;

        _repositionIntervalTimer -= Time.fixedDeltaTime;
        return _repositionIntervalTimer <= 0f;
    }

    private void StartRepositionInterval()
    {
        if (_flyingMovementData == null || _flyingMovementData.MoveAfterAttack)
        {
            _repositionIntervalTimer = 0f;
            return;
        }

        Vector2 intervalRange = _flyingMovementData.RepositionIntervalRange;
        _repositionIntervalTimer = Random.Range(intervalRange.x, intervalRange.y);
    }

    private bool IsCurrentMoveTargetTooCloseToTarget()
    {
        if (Target == null || _flyingMovementData == null)
            return false;

        float minDistance = _flyingMovementData.MaintainDistanceRange.x;
        return ((Vector2)Target.position - _currentMoveTarget).sqrMagnitude < minDistance * minDistance;
    }

    private bool TryPickMoveTarget(out Vector2 moveTarget)
    {
        Vector2 targetPosition = Target.position;
        Vector2 distanceRange = _flyingMovementData.MaintainDistanceRange;
        Vector2 currentPosition = Rigidbody.position;
        Vector2 fromTargetToEnemy = currentPosition - targetPosition;

        if (fromTargetToEnemy.sqrMagnitude < distanceRange.x * distanceRange.x
            && TryPickAwayMoveTarget(targetPosition, fromTargetToEnemy, distanceRange, out moveTarget))
        {
            return true;
        }

        if (fromTargetToEnemy.sqrMagnitude > distanceRange.y * distanceRange.y
            && TryPickApproachMoveTarget(targetPosition, fromTargetToEnemy, distanceRange, out moveTarget))
        {
            return true;
        }

        for (int i = 0; i < _flyingMovementData.PositionSampleAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 candidate = targetPosition + direction * Random.Range(distanceRange.x, distanceRange.y);

            if (IsMoveTargetSafe(candidate) && IsTravelDistanceAllowed(candidate))
            {
                moveTarget = candidate;
                return true;
            }
        }

        moveTarget = default;
        return false;
    }

    private bool TryPickApproachMoveTarget(
        Vector2 targetPosition,
        Vector2 fromTargetToEnemy,
        Vector2 distanceRange,
        out Vector2 moveTarget)
    {
        Vector2 currentPosition = Rigidbody.position;
        Vector2 directionToTarget = -fromTargetToEnemy.normalized;
        float currentDistanceToTarget = fromTargetToEnemy.magnitude;
        float distanceUntilMaintainRange = Mathf.Max(0f, currentDistanceToTarget - distanceRange.y);
        Vector2 travelDistanceRange = _flyingMovementData.RepositionTravelDistanceRange;

        for (int i = 0; i < _flyingMovementData.PositionSampleAttempts; i++)
        {
            float angleOffset = Random.Range(-35f, 35f);
            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffset) * directionToTarget;
            float travelDistance = GetApproachTravelDistance(distanceUntilMaintainRange, travelDistanceRange);
            Vector2 candidate = currentPosition + direction * travelDistance;

            if (IsMoveTargetSafe(candidate) && IsApproachTargetDistanceAllowed(candidate, currentDistanceToTarget))
            {
                moveTarget = candidate;
                return true;
            }
        }

        moveTarget = default;
        return false;
    }

    private bool TryPickAwayMoveTarget(
        Vector2 targetPosition,
        Vector2 fromTargetToEnemy,
        Vector2 distanceRange,
        out Vector2 moveTarget)
    {
        Vector2 awayDirection = fromTargetToEnemy.sqrMagnitude > 0.0001f
            ? fromTargetToEnemy.normalized
            : Vector2.up;

        for (int i = 0; i < _flyingMovementData.PositionSampleAttempts; i++)
        {
            float angleOffset = Random.Range(-55f, 55f);
            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffset) * awayDirection;
            Vector2 candidate = targetPosition + direction * Random.Range(distanceRange.x, distanceRange.y);

            if (IsMoveTargetSafe(candidate) && IsTravelDistanceAllowed(candidate))
            {
                moveTarget = candidate;
                return true;
            }
        }

        moveTarget = default;
        return false;
    }

    private bool IsMoveTargetSafe(Vector2 candidate)
    {
        LayerMask obstacleLayer = _flyingMovementData.ObstacleLayer;
        if (obstacleLayer.value == 0)
            return true;

        float radius = _flyingMovementData.ClearanceRadius;
        if (radius > 0f && Physics2D.OverlapCircle(candidate, radius, obstacleLayer) != null)
            return false;

        if (IsBlocked(candidate, Vector2.left, _flyingMovementData.WallAvoidanceDistance, obstacleLayer))
            return false;

        if (IsBlocked(candidate, Vector2.right, _flyingMovementData.WallAvoidanceDistance, obstacleLayer))
            return false;

        if (IsBlocked(candidate, Vector2.down, _flyingMovementData.FloorAvoidanceDistance, obstacleLayer))
            return false;

        if (IsBlocked(candidate, Vector2.up, _flyingMovementData.CeilingAvoidanceDistance, obstacleLayer))
            return false;

        Vector2 currentPosition = Rigidbody.position;
        Vector2 path = candidate - currentPosition;
        float pathDistance = path.magnitude;

        if (pathDistance <= 0.0001f)
            return true;

        return Physics2D.CircleCast(
            currentPosition,
            radius,
            path.normalized,
            pathDistance,
            obstacleLayer).collider == null;
    }

    private bool IsApproachTargetDistanceAllowed(Vector2 candidate, float currentDistanceToTarget)
    {
        if (Target == null)
            return false;

        Vector2 distanceRange = _flyingMovementData.MaintainDistanceRange;
        float distanceToTarget = Vector2.Distance(Target.position, candidate);
        return distanceToTarget >= distanceRange.x
               && distanceToTarget < currentDistanceToTarget;
    }

    private bool IsTravelDistanceAllowed(Vector2 candidate)
    {
        Vector2 travelDistanceRange = _flyingMovementData.RepositionTravelDistanceRange;

        if (travelDistanceRange.y <= 0f)
            return true;

        float sqrDistance = (candidate - Rigidbody.position).sqrMagnitude;
        return sqrDistance >= travelDistanceRange.x * travelDistanceRange.x
               && sqrDistance <= travelDistanceRange.y * travelDistanceRange.y;
    }

    private static float GetApproachTravelDistance(float distanceUntilMaintainRange, Vector2 travelDistanceRange)
    {
        if (travelDistanceRange.y <= 0f)
            return distanceUntilMaintainRange;

        if (distanceUntilMaintainRange <= travelDistanceRange.y)
            return distanceUntilMaintainRange;

        return Random.Range(travelDistanceRange.x, travelDistanceRange.y);
    }

    private static bool IsBlocked(Vector2 origin, Vector2 direction, float distance, LayerMask obstacleLayer)
    {
        if (distance <= 0f)
            return false;

        return Physics2D.Raycast(origin, direction, distance, obstacleLayer).collider != null;
    }
}
