using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyDirectProjectile : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private bool _rotateToDirection = true;

    [Header("Collision")]
    [SerializeField] private LayerMask _obstacleLayer;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private Vector3 _moveDirection;
    private float _speed;
    private float _damage;
    private float _lifeTime;
    private float _knockbackPower;
    private GameObject _owner;
    private bool _isInitialized;
    private bool _isDespawning;
    private Coroutine _lifeTimeRoutine;

    public void Initialize(
        EnemyProjectileAttackData attackData,
        Vector3 startPosition,
        Vector3 targetPosition,
        GameObject owner)
    {
        if (attackData == null)
        {
            Debug.LogError("[EnemyDirectProjectile] attackData is null.", this);
            Despawn();
            return;
        }

        _startPosition = startPosition;
        _targetPosition = targetPosition;
        _moveDirection = (_targetPosition - _startPosition).normalized;
        if (_moveDirection.sqrMagnitude <= 0.0001f)
            _moveDirection = transform.right;

        _speed = attackData.ProjectileSpeed;
        _damage = attackData.Damage;
        _lifeTime = attackData.LifeTime;
        _knockbackPower = attackData.KnockbackPower;
        _owner = owner;
        _isInitialized = true;
        _isDespawning = false;

        transform.position = _startPosition;
        ApplyRotation();
        RestartLifeTimeRoutine();
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<StageClearedEvent>(HandleStageCleared);
        EventBus.Instance.Subscribe<StageFailedEvent>(HandleStageFailed);
        EventBus.Instance.Subscribe<SceneLoadedEvent>(HandleSceneLoaded);
    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe<StageClearedEvent>(HandleStageCleared);
        EventBus.Instance.Unsubscribe<StageFailedEvent>(HandleStageFailed);
        EventBus.Instance.Unsubscribe<SceneLoadedEvent>(HandleSceneLoaded);

        StopLifeTimeRoutine();
        _isInitialized = false;
        _isDespawning = false;
        _owner = null;
    }

    private void Update()
    {
        if (!_isInitialized)
            return;

        transform.position += _moveDirection * (_speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isInitialized || _isDespawning || other == null)
            return;

        if (_owner != null && other.transform.IsChildOf(_owner.transform))
            return;

        if (TryDamageTarget(other))
        {
            Despawn();
            return;
        }

        if (IsInLayerMask(other.gameObject.layer, _obstacleLayer))
            Despawn();
    }

    private bool TryDamageTarget(Collider2D other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();

        if (damageable == null)
            return false;

        if (damageable.Team != TeamType.Player || damageable.IsDead)
            return false;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        Vector2 knockbackDirection = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
            knockbackDirection = Vector2.right;

        damageable.TakeDamage(new DamageData
        {
            Damage = _damage,
            GroggyDamage = 0f,
            AttackerTeam = TeamType.Enemy,
            HitPoint = hitPoint,
            KnockbackForce = knockbackDirection * _knockbackPower,
            IsPiercing = false,
            IsExecution = false,
            AttackKind = WeaponAttackKind.None
        });

        return true;
    }

    private void RestartLifeTimeRoutine()
    {
        StopLifeTimeRoutine();
        if (gameObject.activeInHierarchy)
            _lifeTimeRoutine = StartCoroutine(LifeTimeRoutine());
    }

    private void StopLifeTimeRoutine()
    {
        if (_lifeTimeRoutine == null)
            return;

        StopCoroutine(_lifeTimeRoutine);
        _lifeTimeRoutine = null;
    }

    private IEnumerator LifeTimeRoutine()
    {
        yield return new WaitForSeconds(_lifeTime);
        Despawn();
    }

    private void ApplyRotation()
    {
        if (!_rotateToDirection)
            return;

        if (_moveDirection.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void HandleStageCleared(StageClearedEvent evt) => Despawn();
    private void HandleStageFailed(StageFailedEvent evt) => Despawn();
    private void HandleSceneLoaded(SceneLoadedEvent evt) => Despawn();

    private void Despawn()
    {
        if (_isDespawning)
            return;

        _isDespawning = true;
        _isInitialized = false;

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
