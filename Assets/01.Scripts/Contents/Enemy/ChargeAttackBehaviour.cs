using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ChargeAttackBehaviour : MonoBehaviour, IEnemyBehaviour
{
    private enum ChargeState
    {
        Idle,
        Descending,
        Warmup,
        Charging
    }

    private static Sprite _warningSprite;

    private EnemyBase _enemyBase;
    private Rigidbody2D _rigidbody;
    private Collider2D _bodyCollider;
    private SpriteRenderer _spriteRenderer;
    private HeavyEnemyMovementData _movementData;
    private Func<bool> _canMove;
    private Func<bool> _isTargetDetected;
    private Func<Transform> _getTarget;
    private Action _stopMovement;
    private ChargeState _chargeState;
    private Vector2 _chargeDirection;
    private Vector2 _chargeStartPosition;
    private float _chargeCooldownTimer;
    private float _warmupTimer;
    private GameObject _warningObject;
    private SpriteRenderer _warningRenderer;

    public bool IsBehaviourActive => _chargeState != ChargeState.Idle;
    public bool IsCharging => _chargeState == ChargeState.Charging;

    public void Initialize(
        EnemyBase enemyBase,
        Rigidbody2D rigidbody,
        Collider2D bodyCollider,
        SpriteRenderer spriteRenderer,
        HeavyEnemyMovementData movementData,
        Func<bool> canMove,
        Func<bool> isTargetDetected,
        Func<Transform> getTarget,
        Action stopMovement)
    {
        _enemyBase = enemyBase;
        _rigidbody = rigidbody;
        _bodyCollider = bodyCollider;
        _spriteRenderer = spriteRenderer;
        _movementData = movementData;
        _canMove = canMove;
        _isTargetDetected = isTargetDetected;
        _getTarget = getTarget;
        _stopMovement = stopMovement;
    }

    public void TickIdle()
    {
        if (_movementData == null)
            return;

        UpdateChargeCooldown();
        TryStartWarmup();
    }

    public void TickActive()
    {
        if (_movementData == null)
            return;

        switch (_chargeState)
        {
            case ChargeState.Descending:
                UpdatePreChargeDescent();
                break;
            case ChargeState.Warmup:
                UpdateWarmup();
                break;
            case ChargeState.Charging:
                UpdateCharge();
                break;
        }
    }

    public void HandleGroggyStateEntered(EnemyBase enemy)
    {
        if (_chargeState == ChargeState.Idle)
            return;

        CancelChargePattern();
    }

    public void HandleCollisionEnter2D(Collision2D collision)
    {
        if (_movementData == null)
            return;

        if (_chargeState == ChargeState.Descending && IsGroundLandingCollision(collision))
        {
            _rigidbody.linearVelocity = Vector2.zero;
            EventBus.Instance?.Publish(new CameraShakeEvent
            {
                Intensity = _movementData.PreChargeLandingShakeIntensity
            });
            StartWarmup();
            return;
        }

        if (_chargeState != ChargeState.Charging)
            return;

        if (TryHandleWeaponPlatformCollision(collision.collider))
            return;

        if (IsChargeBlocker(collision.collider))
            EndCharge(true);
    }

    public void Cleanup()
    {
        DestroyWarningVisual();

        if (_enemyBase != null)
        {
            _enemyBase.IsDamageBlocked = false;
            _enemyBase.IsKnockbackBlocked = false;
        }

        _chargeState = ChargeState.Idle;
    }

    private void UpdateChargeCooldown()
    {
        if (_chargeCooldownTimer <= 0f)
            return;

        _chargeCooldownTimer -= Time.fixedDeltaTime;
    }

    private void TryStartWarmup()
    {
        if (_chargeCooldownTimer > 0f)
            return;

        Transform target = _getTarget?.Invoke();
        if (_canMove == null || !_canMove.Invoke() || _isTargetDetected == null || !_isTargetDetected.Invoke() || target == null)
            return;

        if (!IsTargetInChargeStartRange(target))
            return;

        float directionX = target.position.x - transform.position.x;
        if (Mathf.Abs(directionX) <= 0.01f)
            directionX = _spriteRenderer != null && _spriteRenderer.flipX ? -1f : 1f;

        _chargeDirection = new Vector2(Mathf.Sign(directionX), 0f);
        if (IsGroundedForCharge())
            StartWarmup();
        else
            _chargeState = ChargeState.Descending;

        _stopMovement?.Invoke();
    }

    private bool IsTargetInChargeStartRange(Transform target)
    {
        Vector2 startRange = _movementData.ChargeStartDistanceRange;
        float distance = Vector2.Distance(transform.position, target.position);
        return distance >= startRange.x && distance <= startRange.y;
    }

    private void UpdatePreChargeDescent()
    {
        if (_enemyBase == null || _enemyBase.IsDead || _enemyBase.IsGroggy)
        {
            CancelChargePattern();
            return;
        }

        if (IsGroundedForCharge())
        {
            _rigidbody.linearVelocity = Vector2.zero;
            EventBus.Instance?.Publish(new CameraShakeEvent
            {
                Intensity = _movementData.PreChargeLandingShakeIntensity
            });
            StartWarmup();
            return;
        }

        _rigidbody.linearVelocity = new Vector2(0f, -_movementData.PreChargeDropSpeed);
    }

    private void StartWarmup()
    {
        _warmupTimer = 0f;
        _chargeState = ChargeState.Warmup;
        if (_enemyBase != null)
            _enemyBase.IsKnockbackBlocked = true;

        CreateWarningVisual();
        _stopMovement?.Invoke();
    }

    private void UpdateWarmup()
    {
        if (_enemyBase == null || _enemyBase.IsDead || _enemyBase.IsGroggy)
        {
            CancelChargePattern();
            return;
        }

        _stopMovement?.Invoke();
        UpdateWarningVisual();

        _warmupTimer += Time.fixedDeltaTime;
        if (_warmupTimer < _movementData.ChargeWarmupDuration)
            return;

        StartCharge();
    }

    private void StartCharge()
    {
        DestroyWarningVisual();
        _chargeStartPosition = _rigidbody.position;
        _chargeState = ChargeState.Charging;

        if (_enemyBase != null)
        {
            _enemyBase.IsKnockbackBlocked = false;
            _enemyBase.IsDamageBlocked = true;
        }
    }

    private void UpdateCharge()
    {
        if (_enemyBase == null || _enemyBase.IsDead)
        {
            EndCharge(false);
            return;
        }

        _rigidbody.linearVelocity = _chargeDirection * _movementData.ChargeSpeed;

        float traveledDistance = Vector2.Distance(_chargeStartPosition, _rigidbody.position);
        if (traveledDistance >= _movementData.ChargeDistance)
            EndCharge(true);
    }

    private void EndCharge(bool startCooldown)
    {
        if (_enemyBase != null)
        {
            _enemyBase.IsDamageBlocked = false;
            _enemyBase.IsKnockbackBlocked = false;
        }

        if (_rigidbody != null && (_enemyBase == null || !_enemyBase.IsDead))
            _rigidbody.linearVelocity = Vector2.zero;

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = startCooldown ? _movementData.ChargeCooldown : 0f;
    }

    private void CancelChargePattern()
    {
        DestroyWarningVisual();

        if (_enemyBase != null)
        {
            _enemyBase.IsDamageBlocked = false;
            _enemyBase.IsKnockbackBlocked = false;
        }

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = _movementData != null ? _movementData.ChargeCooldown : 0f;
    }

    private bool TryHandleWeaponPlatformCollision(Collider2D collider)
    {
        if (collider == null)
            return false;

        WeaponWallPlatform platform = collider.GetComponentInParent<WeaponWallPlatform>();
        WeaponStateMachine stateMachine = collider.GetComponentInParent<WeaponStateMachine>();

        if (platform == null || stateMachine == null || !stateMachine.IsPinnedToWall || !collider.enabled)
            return false;

        WeaponLinkEnergy linkEnergy = collider.GetComponentInParent<WeaponLinkEnergy>();
        linkEnergy?.NotifyDepleted();

        ThrustPierceModule thrustPierce = collider.GetComponentInParent<ThrustPierceModule>();
        thrustPierce?.ForceReturnPinnedWallToPlayer();

        EventBus.Instance?.Publish(new CameraShakeEvent
        {
            Intensity = _movementData.PlatformHitShakeIntensity
        });

        if (_enemyBase != null)
        {
            _enemyBase.IsDamageBlocked = false;
            _enemyBase.IsKnockbackBlocked = false;
            _enemyBase.ForceEnterGroggy(-_chargeDirection * _movementData.PlatformHitKnockback);
        }

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(-_chargeDirection * _movementData.PlatformHitKnockback, ForceMode2D.Impulse);

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = _movementData.ChargeCooldown;
        return true;
    }

    private bool IsChargeBlocker(Collider2D collider)
    {
        if (collider == null)
            return false;

        LayerMask blockerLayer = _movementData.ChargeBlockerLayer;
        if (blockerLayer.value == 0)
            return false;

        return (blockerLayer.value & (1 << collider.gameObject.layer)) != 0;
    }

    private bool IsGroundLandingCollision(Collision2D collision)
    {
        if (collision == null || !IsChargeBlocker(collision.collider))
            return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
                return true;
        }

        return false;
    }

    private bool IsGroundedForCharge()
    {
        if (_rigidbody == null || _movementData == null)
            return true;

        LayerMask groundLayer = _movementData.ChargeBlockerLayer;
        if (groundLayer.value == 0)
            return true;

        Vector2 rayOrigin = _rigidbody.position;
        if (_bodyCollider != null)
            rayOrigin = new Vector2(_bodyCollider.bounds.center.x, _bodyCollider.bounds.min.y + 0.02f);

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            _movementData.PreChargeGroundCheckDistance,
            groundLayer);
        return hit.collider != null;
    }

    private void CreateWarningVisual()
    {
        DestroyWarningVisual();

        _warningObject = new GameObject("HeavyChargeWarning");
        _warningRenderer = _warningObject.AddComponent<SpriteRenderer>();
        _warningRenderer.sprite = GetWarningSprite();
        _warningRenderer.sortingLayerID = _spriteRenderer != null ? _spriteRenderer.sortingLayerID : 0;
        _warningRenderer.sortingOrder = _spriteRenderer != null ? _spriteRenderer.sortingOrder - 1 : 0;
        _warningRenderer.color = new Color(1f, 0f, 0f, _movementData.ChargeWarningMaxAlpha);

        Vector2 center = (Vector2)transform.position + _chargeDirection * (_movementData.ChargeDistance * 0.5f);
        _warningObject.transform.position = center;
        _warningObject.transform.rotation = Quaternion.FromToRotation(Vector3.right, _chargeDirection);
        _warningObject.transform.localScale = new Vector3(
            _movementData.ChargeDistance,
            _movementData.ChargeWarningWidth,
            1f);
    }

    private void UpdateWarningVisual()
    {
        if (_warningRenderer == null)
            return;

        float blink = _movementData.ChargeWarningBlinkSpeed <= 0f
            ? 1f
            : Mathf.PingPong(Time.time * _movementData.ChargeWarningBlinkSpeed, 1f);
        float alpha = Mathf.Lerp(
            _movementData.ChargeWarningMinAlpha,
            _movementData.ChargeWarningMaxAlpha,
            blink);

        Color color = _warningRenderer.color;
        color.a = alpha;
        _warningRenderer.color = color;
    }

    private void DestroyWarningVisual()
    {
        if (_warningObject != null)
            Destroy(_warningObject);

        _warningObject = null;
        _warningRenderer = null;
    }

    private static Sprite GetWarningSprite()
    {
        if (_warningSprite != null)
            return _warningSprite;

        _warningSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        return _warningSprite;
    }
}
