using UnityEngine;

[CreateAssetMenu(fileName = "FlyingEnemyMovementData", menuName = "BladeShift/Enemy/Movement/Flying")]
public class FlyingEnemyMovementData : EnemyMovementData
{
    [Header("Reposition")]
    [SerializeField] private Vector2 _maintainDistanceRange = new(4f, 6f);
    [SerializeField] private Vector2 _repositionSpeedRange = new(6f, 9f);
    [SerializeField, Min(0.01f)] private float _arriveDistance = 0.25f;
    [SerializeField, Min(1)] private int _positionSampleAttempts = 12;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField, Min(0f)] private float _clearanceRadius = 0.45f;
    [SerializeField, Min(0f)] private float _wallAvoidanceDistance = 0.7f;
    [SerializeField, Min(0f)] private float _floorAvoidanceDistance = 1.2f;
    [SerializeField, Min(0f)] private float _ceilingAvoidanceDistance = 0.35f;

    public Vector2 MaintainDistanceRange => _maintainDistanceRange;
    public Vector2 RepositionSpeedRange => _repositionSpeedRange;
    public float ArriveDistance => _arriveDistance;
    public int PositionSampleAttempts => _positionSampleAttempts;
    public LayerMask ObstacleLayer => _obstacleLayer;
    public float ClearanceRadius => _clearanceRadius;
    public float WallAvoidanceDistance => _wallAvoidanceDistance;
    public float FloorAvoidanceDistance => _floorAvoidanceDistance;
    public float CeilingAvoidanceDistance => _ceilingAvoidanceDistance;

    protected override void OnValidate()
    {
        base.OnValidate();

        _maintainDistanceRange.x = Mathf.Max(0f, _maintainDistanceRange.x);
        _maintainDistanceRange.y = Mathf.Max(_maintainDistanceRange.x, _maintainDistanceRange.y);
        _repositionSpeedRange.x = Mathf.Max(0f, _repositionSpeedRange.x);
        _repositionSpeedRange.y = Mathf.Max(_repositionSpeedRange.x, _repositionSpeedRange.y);
        _arriveDistance = Mathf.Max(0.01f, _arriveDistance);
        _positionSampleAttempts = Mathf.Max(1, _positionSampleAttempts);
        _clearanceRadius = Mathf.Max(0f, _clearanceRadius);
        _wallAvoidanceDistance = Mathf.Max(0f, _wallAvoidanceDistance);
        _floorAvoidanceDistance = Mathf.Max(0f, _floorAvoidanceDistance);
        _ceilingAvoidanceDistance = Mathf.Max(0f, _ceilingAvoidanceDistance);
    }
}
