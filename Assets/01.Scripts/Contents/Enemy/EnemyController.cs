using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private EnemyBase _enemyBase;

    [Header("AI")]
    [SerializeField] private float _moveSpeed = 2.5f;
    [SerializeField] private float _detectRange = 8f;
    [SerializeField] private float _stopDistance = 0.8f;
    [SerializeField] private LayerMask _groundLayer;

    private void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody2D>();

        if (_enemyBase == null)
            _enemyBase = GetComponent<EnemyBase>();

        if (_playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                _playerTransform = playerObject.transform;
        }
    }

    private void FixedUpdate()
    {
        if (!CanChasePlayer())
        {
            StopMoveX();
            return;
        }

        Vector2 direction = _playerTransform.position - transform.position;

        if (Mathf.Abs(direction.x) <= _stopDistance)
        {
            StopMoveX();
            return;
        }

        float moveX = Mathf.Sign(direction.x) * _moveSpeed;

        _rigidbody.linearVelocity = new Vector2(
            moveX,
            _rigidbody.linearVelocity.y
        );
    }

    private bool CanChasePlayer()
    {
        if (_playerTransform == null)
            return false;

        if (_rigidbody == null)
            return false;

        if (_enemyBase != null && _enemyBase.IsDead)
            return false;

        Vector2 origin = transform.position;
        Vector2 target = _playerTransform.position;
        Vector2 direction = target - origin;

        if (direction.sqrMagnitude > _detectRange * _detectRange)
            return false;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            direction.magnitude,
            _groundLayer
        );

        return hit.collider == null;
    }

    private void StopMoveX()
    {
        if (_rigidbody == null)
            return;

        _rigidbody.linearVelocity = new Vector2(
            0f,
            _rigidbody.linearVelocity.y
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectRange);

        if (_playerTransform == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, _playerTransform.position);
    }
}