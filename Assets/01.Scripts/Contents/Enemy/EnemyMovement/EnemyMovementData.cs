using UnityEngine;

[CreateAssetMenu(fileName = "EnemyMovementData", menuName = "BladeShift/Enemy/Movement Data")]
public class EnemyMovementData : ScriptableObject
{
    [Header("Chase")]
    [SerializeField, Min(0f)] private float _moveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float _detectRange = 8f;
    [SerializeField, Min(0f)] private float _stopDistance = 0.8f;
    [SerializeField] private bool _keepChasingAfterDetection;

    [Header("Sight")]
    [SerializeField] private bool _requiresLineOfSight = true;
    [SerializeField] private LayerMask _lineOfSightBlockerLayer;

    public float MoveSpeed => _moveSpeed;
    public float DetectRange => _detectRange;
    public float StopDistance => _stopDistance;
    public bool KeepChasingAfterDetection => _keepChasingAfterDetection;
    public bool RequiresLineOfSight => _requiresLineOfSight;
    public LayerMask LineOfSightBlockerLayer => _lineOfSightBlockerLayer;

    protected virtual void OnValidate()
    {
        _moveSpeed = Mathf.Max(0f, _moveSpeed);
        _detectRange = Mathf.Max(0f, _detectRange);
        _stopDistance = Mathf.Max(0f, _stopDistance);
    }
}
