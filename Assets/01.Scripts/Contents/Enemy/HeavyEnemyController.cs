using UnityEngine;

public class HeavyEnemyController : EnemyController
{
    private enum ChargeState
    {
        Idle,
        Descending,
        Warmup,
        Charging
    }

    private static Sprite _warningSprite;

    private HeavyEnemyMovementData _heavyMovementData;
    private SpriteAfterImage _afterImage;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _bodyCollider;
    private ChargeState _chargeState;
    private Vector2 _chargeDirection;
    private Vector2 _chargeStartPosition;
    private float _chargeCooldownTimer;
    private float _warmupTimer;
    private GameObject _warningObject;
    private SpriteRenderer _warningRenderer;

    public bool IsCharging => _chargeState == ChargeState.Charging;

    protected override void Awake()
    {
        base.Awake();

        TryGetMovementData(out _heavyMovementData);
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _bodyCollider = GetComponent<Collider2D>();
        _afterImage = GetComponent<SpriteAfterImage>();
        if (_afterImage == null)
            _afterImage = gameObject.AddComponent<SpriteAfterImage>();

        _afterImage.Initialize(() => IsCharging, _spriteRenderer);
        if (_heavyMovementData != null)
            _afterImage.SetAfterImageColor(_heavyMovementData.ChargeAfterImageColor);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (EnemyBase != null)
            EnemyBase.GroggyStateEntered += HandleGroggyStateEntered;
    }

    protected override void OnDisable()
    {
        CleanupChargeState();

        if (EnemyBase != null)
            EnemyBase.GroggyStateEntered -= HandleGroggyStateEntered;

        base.OnDisable();
    }

    protected override void FixedUpdate()
    {
        if (_heavyMovementData == null)
        {
            base.FixedUpdate();
            return;
        }

        switch (_chargeState)
        {
            case ChargeState.Descending:
                UpdatePreChargeDescent();
                return;
            case ChargeState.Warmup:
                UpdateWarmup();
                return;
            case ChargeState.Charging:
                UpdateCharge();
                return;
            case ChargeState.Idle:
            default:
                base.FixedUpdate();
                UpdateChargeCooldown();
                TryStartWarmup();
                return;
        }
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

        if (!CanMove() || !IsTargetDetected || Target == null)
            return;

        if (!IsTargetInChargeStartRange())
            return;

        float directionX = Target.position.x - transform.position.x;
        if (Mathf.Abs(directionX) <= 0.01f)
            directionX = _spriteRenderer != null && _spriteRenderer.flipX ? -1f : 1f;

        _chargeDirection = new Vector2(Mathf.Sign(directionX), 0f);
        if (IsGroundedForCharge())
            StartWarmup();
        else
            _chargeState = ChargeState.Descending;

        StopMovement();
    }

    private bool IsTargetInChargeStartRange()
    {
        if (Target == null)
            return false;

        Vector2 startRange = _heavyMovementData.ChargeStartDistanceRange;
        float distance = Vector2.Distance(transform.position, Target.position);
        return distance >= startRange.x && distance <= startRange.y;
    }

    private void UpdatePreChargeDescent()
    {
        if (EnemyBase == null || EnemyBase.IsDead || EnemyBase.IsGroggy)
        {
            CancelChargePattern();
            return;
        }

        if (IsGroundedForCharge())
        {
            Rigidbody.linearVelocity = Vector2.zero;
            EventBus.Instance?.Publish(new CameraShakeEvent
            {
                Intensity = _heavyMovementData.PreChargeLandingShakeIntensity
            });
            StartWarmup();
            return;
        }

        Rigidbody.linearVelocity = new Vector2(0f, -_heavyMovementData.PreChargeDropSpeed);
    }

    private void StartWarmup()
    {
        _warmupTimer = 0f;
        _chargeState = ChargeState.Warmup;
        if (EnemyBase != null)
            EnemyBase.IsKnockbackBlocked = true;

        CreateWarningVisual();
        StopMovement();
    }

    private void UpdateWarmup()
    {
        if (EnemyBase == null || EnemyBase.IsDead || EnemyBase.IsGroggy)
        {
            CancelChargePattern();
            return;
        }

        StopMovement();
        UpdateWarningVisual();

        _warmupTimer += Time.fixedDeltaTime;
        if (_warmupTimer < _heavyMovementData.ChargeWarmupDuration)
            return;

        StartCharge();
    }

    private void StartCharge()
    {
        DestroyWarningVisual();
        _chargeStartPosition = Rigidbody.position;
        _chargeState = ChargeState.Charging;

        if (EnemyBase != null)
        {
            EnemyBase.IsKnockbackBlocked = false;
            EnemyBase.IsDamageBlocked = true;
        }
    }

    private void UpdateCharge()
    {
        if (EnemyBase == null || EnemyBase.IsDead)
        {
            EndCharge(false);
            return;
        }

        Rigidbody.linearVelocity = _chargeDirection * _heavyMovementData.ChargeSpeed;

        float traveledDistance = Vector2.Distance(_chargeStartPosition, Rigidbody.position);
        if (traveledDistance >= _heavyMovementData.ChargeDistance)
            EndCharge(true);
    }

    private void EndCharge(bool startCooldown)
    {
        if (EnemyBase != null)
        {
            EnemyBase.IsDamageBlocked = false;
            EnemyBase.IsKnockbackBlocked = false;
        }

        if (Rigidbody != null && (EnemyBase == null || !EnemyBase.IsDead))
            Rigidbody.linearVelocity = Vector2.zero;

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = startCooldown ? _heavyMovementData.ChargeCooldown : 0f;
    }

    private void CancelChargePattern()
    {
        DestroyWarningVisual();

        if (EnemyBase != null)
        {
            EnemyBase.IsDamageBlocked = false;
            EnemyBase.IsKnockbackBlocked = false;
        }

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = _heavyMovementData != null ? _heavyMovementData.ChargeCooldown : 0f;
    }

    private void CleanupChargeState()
    {
        DestroyWarningVisual();

        if (EnemyBase != null)
        {
            EnemyBase.IsDamageBlocked = false;
            EnemyBase.IsKnockbackBlocked = false;
        }

        _chargeState = ChargeState.Idle;
    }

    private void HandleGroggyStateEntered(EnemyBase enemy)
    {
        if (_chargeState == ChargeState.Idle)
            return;

        CancelChargePattern();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_chargeState == ChargeState.Descending && IsGroundLandingCollision(collision))
        {
            Rigidbody.linearVelocity = Vector2.zero;
            EventBus.Instance?.Publish(new CameraShakeEvent
            {
                Intensity = _heavyMovementData.PreChargeLandingShakeIntensity
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
            Intensity = _heavyMovementData.PlatformHitShakeIntensity
        });

        if (EnemyBase != null)
        {
            EnemyBase.IsDamageBlocked = false;
            EnemyBase.IsKnockbackBlocked = false;
            EnemyBase.ForceEnterGroggy(-_chargeDirection * _heavyMovementData.PlatformHitKnockback);
        }

        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.AddForce(-_chargeDirection * _heavyMovementData.PlatformHitKnockback, ForceMode2D.Impulse);

        _chargeState = ChargeState.Idle;
        _chargeCooldownTimer = _heavyMovementData.ChargeCooldown;
        return true;
    }

    private bool IsChargeBlocker(Collider2D collider)
    {
        if (collider == null)
            return false;

        LayerMask blockerLayer = _heavyMovementData.ChargeBlockerLayer;
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
        if (Rigidbody == null || _heavyMovementData == null)
            return true;

        LayerMask groundLayer = _heavyMovementData.ChargeBlockerLayer;
        if (groundLayer.value == 0)
            return true;

        Vector2 rayOrigin = Rigidbody.position;
        if (_bodyCollider != null)
            rayOrigin = new Vector2(_bodyCollider.bounds.center.x, _bodyCollider.bounds.min.y + 0.02f);

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            _heavyMovementData.PreChargeGroundCheckDistance,
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
        _warningRenderer.color = new Color(1f, 0f, 0f, _heavyMovementData.ChargeWarningMaxAlpha);

        Vector2 center = (Vector2)transform.position + _chargeDirection * (_heavyMovementData.ChargeDistance * 0.5f);
        _warningObject.transform.position = center;
        _warningObject.transform.rotation = Quaternion.FromToRotation(Vector3.right, _chargeDirection);
        _warningObject.transform.localScale = new Vector3(
            _heavyMovementData.ChargeDistance,
            _heavyMovementData.ChargeWarningWidth,
            1f);
    }

    private void UpdateWarningVisual()
    {
        if (_warningRenderer == null)
            return;

        float blink = _heavyMovementData.ChargeWarningBlinkSpeed <= 0f
            ? 1f
            : Mathf.PingPong(Time.time * _heavyMovementData.ChargeWarningBlinkSpeed, 1f);
        float alpha = Mathf.Lerp(
            _heavyMovementData.ChargeWarningMinAlpha,
            _heavyMovementData.ChargeWarningMaxAlpha,
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
