using System;
using System.Collections.Generic;
using UnityEngine;

 [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
 [RequireComponent(typeof(WeaponMovement), typeof(WeaponCombat))]
 [RequireComponent(typeof(WeaponSensor), typeof(WeaponView))]
 [RequireComponent(typeof(WeaponCapture), typeof(WeaponLinkEnergy))]
public class WeaponController : MonoBehaviour
{
    [Header("1. Dual-Radius Settings")]
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Transform _droneDockPivot;

    [Header("2. Combat Settings")]
    [SerializeField] private float _slowMotionScale = 0.2f;
    [SerializeField] private LayerMask _wallAndEnvironmentLayer;
    [SerializeField] private float _thrustDragThreshold = 2.0f;
    [SerializeField] private float _returnCompleteDistance = 0.7f;
    [SerializeField] private float _fullRechargeDelayAfterReturn = 1.0f;
    [Header("2-1. Action Modules")]
    [SerializeField] private WeaponActionModule _primaryModule;
    [SerializeField] private WeaponActionModule _secondaryModule;
    [SerializeField] private float _recallEnergyCost = 0f;
    [Header("3. Link Line Colors")]
    [SerializeField] private Color _recoverColor = Color.cyan;
    [SerializeField] private Color _stableColor = Color.white;
    [SerializeField] private Color _drainColor = Color.yellow;
    [SerializeField] private Color _criticalColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float _criticalEnergyRatio = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _blinkEnergyRatio = 0.1f;
    [SerializeField] private float _blinkInterval = 0.12f;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private WeaponMovement _movement;
    private WeaponCombat _combat;
    private WeaponSensor _sensor;
    private WeaponView _view;
    private HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private WeaponCapture _capture;
    private WeaponLinkEnergy _linkEnergy;
    private PlayerController _playerController;
    private float _controlRadius;

    private WeaponState _currentState = WeaponState.Grounded;
    private bool _isAttacking = false;
    private bool _isThrustAiming = false;
    private bool _isTimeSlowed = false;
    private Vector3 _originalScale;
    private Vector2 _fixedAimPos;
    private Vector2 _mouseStartPos;
    private Camera _mainCamera;
    private bool _isDepletionSequenceActive;
    private bool _isAutoReturnInProgress;
    private bool _isDockWaiting;
    private bool _isAwaitingDockRecoveryTrigger;
    private float _dockWaitTimer;

    public float RecallEnergyCost => _recallEnergyCost;
    public WeaponState CurrentState => _currentState;
    public bool IsAttacking => _isAttacking;
    public bool IsThrustAiming => _isThrustAiming;
    public bool IsActionInputBlocked => _isAutoReturnInProgress || _isDockWaiting;
    public float ThrustDragThreshold => _thrustDragThreshold;
    public Vector2 FixedAimPosition => _fixedAimPos;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _movement = GetComponent<WeaponMovement>();
        _combat = GetComponent<WeaponCombat>();
        _sensor = GetComponent<WeaponSensor>();
        _view = GetComponent<WeaponView>();
        _mainCamera = Camera.main;
        _originalScale = transform.localScale;
        _capture = GetComponent<WeaponCapture>();
        _linkEnergy = GetComponent<WeaponLinkEnergy>();

        if (_playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _playerTransform = playerObject.transform;
        }

        if (_playerTransform == null)
        {
            Debug.LogError("[WeaponController] Player object missing.");
            enabled = false;
            return;
        }

        _playerController = _playerTransform.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("[WeaponController] PlayerController missing on player object.");
            enabled = false;
            return;
        }

        _controlRadius = _playerController.ControlRadius;

        _sensor.Configure(_playerTransform, _mainCamera, _controlRadius, 1.0f, 1.5f, 0f);
        _view.Configure(_sensor.GetPlayerTransform(), _controlRadius, 0f, 0f, _combat.SlashRadius);
        _movement.CacheRigidbody(_rb);
        ConfigureActionModules();

        ChangeState(WeaponState.Grounded);
    }

    private void ConfigureActionModules()
    {
        if (_primaryModule == null)
            _primaryModule = GetComponent<SpinSlashModule>();
        if (_secondaryModule == null)
            _secondaryModule = GetComponent<ThrustPierceModule>();

        _primaryModule?.Initialize(this, _linkEnergy, _movement, _combat, _sensor, _capture, _view);
        _secondaryModule?.Initialize(this, _linkEnergy, _movement, _combat, _sensor, _capture, _view);
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Subscribe<PrimaryAttackEvent>(OnPrimaryAttack);
        EventBus.Instance.Subscribe<SecondaryAttackEvent>(OnSecondaryAttack);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.Unsubscribe<PrimaryAttackEvent>(OnPrimaryAttack);
        EventBus.Instance.Unsubscribe<SecondaryAttackEvent>(OnSecondaryAttack);
        ResetTimeScale();
    }

    private void Update()
    {
        Vector2 mouseWorldPos = _sensor.GetMouseWorldPosition();
        bool showConnectionLine = ShouldRenderConnectionLine();
        _view.RenderConnectionLine(_playerTransform.position, transform.position, showConnectionLine, GetCurrentConnectionColor());

        if (_isThrustAiming)
        {
            Vector2 dir = mouseWorldPos - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        if (_isThrustAiming && _currentState == WeaponState.Controlled)
            _view.ShowTrajectory(transform.position, mouseWorldPos);
        else
            _view.HideTrajectory();

        if (_isAutoReturnInProgress || _isDockWaiting)
        {
            return;
        }

        if (!_isAttacking && _currentState == WeaponState.Grounded)
        {
            if (CanStartControl() && _sensor.ShouldAcquireControl(transform.position, mouseWorldPos, _wallAndEnvironmentLayer))
                ChangeState(WeaponState.Controlled);
        }
        else if (!_isAttacking && _currentState == WeaponState.Controlled)
        {
            if (_sensor.ShouldReleaseControl(transform.position, mouseWorldPos, _wallAndEnvironmentLayer))
                ChangeState(WeaponState.Grounded);
        }
    }

    private void FixedUpdate()
    {
        _primaryModule?.OnTick();
        _secondaryModule?.OnTick();

        TickLinkEnergy();
        TryStartDockReturnAfterApproach();
        TickDockWait();

        if (_isThrustAiming && _currentState == WeaponState.Controlled)
        {
            _rb.MovePosition(_fixedAimPos);
        }
        else
        {
            bool hasEnemy = _capture.GetCapturedEnemies().Count > 0;
            bool canHover = _currentState == WeaponState.Controlled ||
                            _currentState == WeaponState.Slashing ||
                            (_currentState == WeaponState.Pinned && hasEnemy);

            if (canHover && !_isThrustAiming)
            {
                _movement.HandleHoverMovement(_sensor.GetClampedTargetPosition(_wallAndEnvironmentLayer), _currentState == WeaponState.Slashing, _wallAndEnvironmentLayer);
            }
        }

        _linkEnergy?.ClearFrameSpendFlag();
    }

    private void TickLinkEnergy()
    {
        if (_linkEnergy == null || _playerTransform == null) return;

        bool isRemoteControlling =
            _currentState == WeaponState.Controlled ||
            _currentState == WeaponState.Slashing ||
            (_currentState == WeaponState.Controlled && _isThrustAiming) ||
            _currentState == WeaponState.Pinned;

        float distance = Vector2.Distance(_playerTransform.position, transform.position);
        _linkEnergy.Tick(distance, _controlRadius, isRemoteControlling, Time.fixedDeltaTime);

        if (_linkEnergy.IsControlLocked && isRemoteControlling && !_isDepletionSequenceActive)
        {
            HandleLinkEnergyDepleted();
        }
    }

    private void TickDockWait()
    {
        if (!_isDockWaiting) return;
        _dockWaitTimer += Time.fixedDeltaTime;
        if (_dockWaitTimer < Mathf.Max(0f, _fullRechargeDelayAfterReturn)) return;

        _isDockWaiting = false;
        _dockWaitTimer = 0f;

        _linkEnergy.RestoreFullAndUnlock();
        transform.SetParent(null);
        _isDepletionSequenceActive = false;
        ChangeState(WeaponState.Grounded);
    }

    private void TryStartDockReturnAfterApproach()
    {
        if (!_isAwaitingDockRecoveryTrigger) return;
        if (_linkEnergy == null || _playerTransform == null) return;
        if (_linkEnergy.DepletionMode != LinkEnergyDepletionMode.AutoReturn) return;
        if (_isAutoReturnInProgress || _isDockWaiting) return;

        float distance = Vector2.Distance(_playerTransform.position, transform.position);
        if (!_linkEnergy.IsInsideRecoverRadius(distance, _controlRadius)) return;

        _isAwaitingDockRecoveryTrigger = false;
        StartEnergyAutoReturnSequence();
    }

    private void HandleLinkEnergyDepleted()
    {
        _isDepletionSequenceActive = true;
        ResetTimeScale();
        _view.HideTrajectory();
        _isAttacking = false;
        _isThrustAiming = false;
        _isTimeSlowed = false;
        _movement.StopFollow();
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        _capture.UnbindAll(forcePhysicsRestore: true);

        transform.SetParent(null);
        transform.localScale = _originalScale;
        _linkEnergy.NotifyDepleted();

        if (_linkEnergy.DepletionMode == LinkEnergyDepletionMode.DropGrounded)
        {
            ChangeState(WeaponState.Grounded);
            _isDepletionSequenceActive = false;
            return;
        }

        _linkEnergy.BlockRecovery();
        _isAwaitingDockRecoveryTrigger = true;
        ChangeState(WeaponState.Grounded);
    }

    private bool CanStartControl()
    {
        if (_linkEnergy == null) return true;
        return _linkEnergy.CanStartControl;
    }

    private bool ShouldRenderConnectionLine()
    {
        bool baseVisible = _currentState == WeaponState.Controlled || _isAttacking;
        if (!baseVisible || _linkEnergy == null) return baseVisible;
        if (_linkEnergy.IsEmpty) return false;
        if (IsBlinkingNow()) return false;
        return true;
    }

    private bool IsBlinkingNow()
    {
        if (_linkEnergy == null) return false;
        if (_linkEnergy.IsRecovering) return false;
        if (!_linkEnergy.IsDraining) return false;
        if (_linkEnergy.IsEmpty) return false;
        if (_linkEnergy.Normalized > _blinkEnergyRatio) return false;
        float safeInterval = Mathf.Max(0.01f, _blinkInterval);
        float phase = Time.time / safeInterval;
        return Mathf.FloorToInt(phase) % 2 == 1;
    }

    private Color GetCurrentConnectionColor()
    {
        if (_linkEnergy == null) return _stableColor;
        if (_linkEnergy.IsEmpty) return Color.black;

        if (_linkEnergy.IsRecovering)
        {
            return _recoverColor;
        }

        float t = Mathf.Clamp01(_linkEnergy.DistanceRatio);
        Color baseColor = t < 0.5f
            ? Color.Lerp(_stableColor, _drainColor, t / 0.5f)
            : Color.Lerp(_drainColor, _criticalColor, (t - 0.5f) / 0.5f);

        float threshold = Mathf.Max(0.0001f, _criticalEnergyRatio);
        float criticalBoost = Mathf.Clamp01((threshold - _linkEnergy.Normalized) / threshold);
        return Color.Lerp(baseColor, _criticalColor, criticalBoost);
    }

    private void ChangeState(WeaponState newState)
    {
        if (_currentState == newState) return;
        if (newState == WeaponState.Grounded) ResetTimeScale();

        _currentState = newState;
        EventBus.Instance?.Publish(new WeaponStateChangeEvent { NewState = _currentState });

        bool isGrounded = _currentState == WeaponState.Grounded;
        UpdateCollisionInteractions(isGrounded);

        switch (_currentState)
        {
            case WeaponState.Grounded:
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _collider.isTrigger = false;
                _movement.TransferVelocityToPhysics();
                _isAttacking = false;
                _isThrustAiming = false;
                _isTimeSlowed = false;
                _view.HideTrajectory();
                break;

            case WeaponState.Pinned:
            case WeaponState.PinningFlight:
            case WeaponState.Returning:
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _collider.isTrigger = true;
                break;

            case WeaponState.Controlled:
            case WeaponState.Slashing:
            case WeaponState.Thrusting:
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _collider.isTrigger = true;
                break;
        }
    }

    private void UpdateCollisionInteractions(bool grounded)
    {
        int weaponLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int environmentLayer = LayerMask.NameToLayer("Environment");

        if (enemyLayer >= 0) Physics2D.IgnoreLayerCollision(weaponLayer, enemyLayer, true);
        if (environmentLayer >= 0) Physics2D.IgnoreLayerCollision(weaponLayer, environmentLayer, false);
    }

    private void OnPrimaryAttack(PrimaryAttackEvent evt)
    {
        if (IsActionInputBlocked) return;
        if (_isThrustAiming) return;
        if (evt.IsStarted && _secondaryModule != null && _secondaryModule.TryHandlePinnedPrimary()) return;

        if (evt.IsStarted) _primaryModule?.OnPress();
        else _primaryModule?.OnRelease();
    }

    private void OnSecondaryAttack(SecondaryAttackEvent evt)
    {
        if (IsActionInputBlocked) return;
        if (evt.IsStarted)
        {
            if (_secondaryModule != null && _secondaryModule.TryHandlePinnedSecondary()) return;
            _secondaryModule?.OnPress();
        }
        else
        {
            _secondaryModule?.OnRelease();
        }
    }

    private void StartPinSequence(Vector2 direction)
    {
        _isThrustAiming = false;
        _view.HideTrajectory();
        ResetTimeScale();
        EventBus.Instance?.Publish(new HitStopEvent { Duration = 0.15f });
        _hitTargets.Clear();
        ChangeState(WeaponState.PinningFlight);
        _isAttacking = true;
        _movement.ExecutePinFlight(direction, _combat.PinSpeed, _combat.EnemyLayer, _wallAndEnvironmentLayer,
        () => _playerTransform.position,
        _controlRadius,
        targetTransform =>
        {
            if (!_combat.PerformPinDamage(targetTransform, transform.position, direction, _hitTargets)) return false;
            if (targetTransform != null)
            {
                _capture.BindEnemy(targetTransform);
            }
            return false;
        },
        hitTransform => 
        {
            EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Weak });
            _isAttacking = false;
            ChangeState(WeaponState.Pinned);
        },
        () =>
        {
            if (HasCapturedEnemies())
            {
                _isAttacking = false;
                ChangeState(WeaponState.Pinned);
                return;
            }

            StartReturnSequence();
        });
    }

    public bool CanStartSpinSlash()
    {
        return !IsActionInputBlocked && !_isThrustAiming && _currentState == WeaponState.Controlled && !_isAttacking;
    }

    public void BeginSpinSlash()
    {
        _isAttacking = true;
        _combat.ResetTickTimer();
        ChangeState(WeaponState.Slashing);
    }

    public void EndSpinSlash()
    {
        _isAttacking = false;
        if (_currentState == WeaponState.Slashing)
            ChangeState(WeaponState.Controlled);
    }

    public void TickSpinSlashCombat()
    {
        if (_currentState != WeaponState.Slashing) return;
        _combat.TryTickSpinDamage(transform.position, transform.eulerAngles.z);
        _combat.DefendProjectiles(transform.position);
        _movement.ApplySpinRotation(_combat.SpinSpeed);
    }

    public bool CanStartThrustAim()
    {
        return !IsActionInputBlocked && _currentState == WeaponState.Controlled && !_isAttacking && !_isThrustAiming;
    }

    public void BeginThrustAim()
    {
        _fixedAimPos = transform.position;
        _mouseStartPos = _sensor.GetMouseWorldPosition();
        _isThrustAiming = true;
        _movement.StopFollow();
        ApplySlowMotion();
    }

    public bool CanReleaseThrustAim()
    {
        return _isThrustAiming && _currentState != WeaponState.Returning;
    }

    public void EndThrustAimAndReset()
    {
        _isThrustAiming = false;
        _view.HideTrajectory();
        ResetTimeScale();
    }

    public float GetThrustDragDistance(Vector2 releaseMousePos)
    {
        return Vector2.Distance(_mouseStartPos, releaseMousePos);
    }

    public void RestoreControlledAfterThrustCancel()
    {
        ChangeState(WeaponState.Controlled);
    }

    public void StartThrustPin(Vector2 direction)
    {
        StartPinSequence(direction);
    }

    public bool IsPlayerInControlRange()
    {
        return _sensor != null && _sensor.IsPlayerInRange(transform.position);
    }

    public bool HasCapturedEnemies()
    {
        return _capture != null && _capture.GetCapturedEnemies().Count > 0;
    }

    public void ExecutePinnedFinisher()
    {
        ExecuteSpinFinisher();
    }

    public void ExecutePinnedRecall()
    {
        UnpinAndReturn();
    }

    private void UnpinAndReturn()
    {
        _capture.UnbindAll(forcePhysicsRestore: true);
        transform.SetParent(null);
        transform.localScale = _originalScale;
        transform.rotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z);
        StartReturnSequence();
    }

    private void StartReturnSequence()
    {
        _hitTargets.Clear();
        ChangeState(WeaponState.Returning);

        _movement.ExecuteReturn(
            () => _sensor.GetClampedTargetPosition(_wallAndEnvironmentLayer),
            _controlRadius,
            (currentPos, mousePos) => _sensor.IsMouseHovering(currentPos, mousePos),
            isSuccess =>
            {
                _isAttacking = false;
                ChangeState(WeaponState.Controlled);
            });
    }

    private void StartEnergyAutoReturnSequence()
    {
        _hitTargets.Clear();
        _isAutoReturnInProgress = true;
        _isDepletionSequenceActive = true;
        ChangeState(WeaponState.Returning);

        _movement.ExecuteReturn(
            GetDockTargetPosition,
            _controlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _returnCompleteDistance,
            isSuccess =>
            {
                _isAutoReturnInProgress = false;
                DockAtPivotAndWait();
            });
    }

    private Vector2 GetDockTargetPosition()
    {
        Transform dock = _droneDockPivot != null ? _droneDockPivot : _playerTransform;
        return dock != null ? (Vector2)dock.position : _rb.position;
    }

    private void DockAtPivotAndWait()
    {
        Transform dock = _droneDockPivot != null ? _droneDockPivot : _playerTransform;
        if (dock == null)
        {
            _isDepletionSequenceActive = false;
            return;
        }

        _rb.MovePosition(dock.position);
        transform.position = dock.position;
        transform.rotation = dock.rotation;
        transform.SetParent(dock, true);

        _dockWaitTimer = 0f;
        _isDockWaiting = true;
    }

    private void ApplySlowMotion()
    {
        if (_isTimeSlowed) return;
        _isTimeSlowed = true;
        EventBus.Instance?.Publish(new SlowMotionEvent { TargetTimeScale = _slowMotionScale, Duration = 999f });
    }

    private void ResetTimeScale()
    {
        if (!_isTimeSlowed) return;
        _isTimeSlowed = false;
        TimeManager.Instance?.ResetTime();
    }

    private void OnDrawGizmos()
    {
        if (_view == null) _view = GetComponent<WeaponView>();
        if (_view != null) _view.DrawGizmos();
    }
    private void ExecuteSpinFinisher()
    {
        _isAttacking = true;
        EventBus.Instance?.Publish(new HitStopEvent { Duration = 0.2f });
        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Strong });
        Vector2 pivot = _sensor.GetMouseWorldPosition();
        float radius = Vector2.Distance(pivot, transform.position);
        radius = Mathf.Max(radius, _combat.SlashRadius * 1.8f);
        var captured = _capture.GetCapturedEnemies();
        List<Transform> pinnedTargets = new List<Transform>(captured.Count);
        foreach (var enemy in captured)
        {
            if (enemy != null)
                pinnedTargets.Add(enemy.transform);
        }

        _movement.ExecuteOrbitFinisher(
            pivot,
            radius,
            0.35f,
            () =>
            {
                _combat.PerformSpinFinisher(transform.position, pinnedTargets);
                _capture.UnbindAll();
            },
            () =>
            {
                _isAttacking = false;
                ChangeState(WeaponState.Controlled);
            });
    }
}
