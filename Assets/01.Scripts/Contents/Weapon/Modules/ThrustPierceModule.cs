using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class ThrustPierceModule : WeaponActionModule
{
    [SerializeField] private float _thrustFireCost = 18f;
    [SerializeField] private float _focusHoldCostPerSecond = 10f;
    [SerializeField] private float _captureHoldCostPerSecond = 10f;
    [SerializeField] private ContinuousEnergySpendMode _captureHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;
    [SerializeField] private float _gamepadAimDeadzone = 0.2f;
    [SerializeField] private float _gamepadAimMaxDistanceRatio = 1f;
    [Header("Pin Flight Tuning")]
    [SerializeField] private float _pinStartHitStopDuration = 0.15f;
    [SerializeField] private ShakeIntensity _pinWallHitShakeIntensity = ShakeIntensity.Weak;
    [SerializeField, Range(0f, 1f)] private float _extraFlightRangeRatio = 0.2f;
    [SerializeField] private float _rangeBrakeDeceleration = 45f;
    [SerializeField] private float _rangeAutoReturnSpeedThreshold = 2f;
    [SerializeField] private float _dockArrivalDistance = 0.6f;
    [SerializeField] private float _dockRechargeDuration = 1f;
    [Header("Finisher Tuning")]
    [SerializeField] private float _finisherHitStopDuration = 0.2f;
    [SerializeField] private float _finisherRadiusMultiplier = 1.8f;
    [SerializeField] private float _finisherOrbitDuration = 0.35f;
    [SerializeField] private ShakeIntensity _finisherShakeIntensity = ShakeIntensity.Strong;
    [Header("Capture Weight")]
    [SerializeField] private float _captureWeightLimit = 3f;

    private readonly HashSet<IDamageable> _pierceHitTargets = new HashSet<IDamageable>();
    private readonly HashSet<EnemyBase> _groggyEnteredDuringCurrentPin = new HashSet<EnemyBase>();
    private Vector2 _aimLockPosition;
    private Vector2 _aimMouseStartPosition;
    private bool _isAiming;
    private bool _isGamepadHoldAim;
    private bool _hasGamepadAimDirection;
    private Vector2 _gamepadAimDirection;
    private float _gamepadAimDragDistance;
    private Vector2 _lastAimDirection;
    private bool _isAutoReturning;
    private bool _isDockWaiting;
    private bool _isFinisherRunning;
    private bool _hasActivatedSlowMotion;
    private bool _aimCanceledByEnergyShortage;
    private bool _lastPinShotWasGamepad;
    private bool _snapCursorOnCurrentReturnFlow;
    private float _currentCaptureWeight;
    private bool _captureWeightBlocked;
    private Coroutine _dockRechargeRoutine;
    public bool IsAiming => _isAiming;
    public bool IsAutoReturning => _isAutoReturning;
    public bool IsDockWaiting => _isDockWaiting;
    public bool IsPinningFlightActive => Controller != null && Controller.CurrentState == WeaponState.PinningFlight;

    public override bool BlocksPrimaryInput => _isAiming;
    public override bool TryHandlePinnedPrimary() => TryHandlePinnedAction();
    public override bool TryHandlePinnedSecondary() => TryHandlePinnedAction();

    public override void OnPress()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (_isAiming || _isAutoReturning || _isDockWaiting) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;

        bool useGamepadAim = IsActionFromGamepad(WeaponActionInputType.Secondary);

        _aimLockPosition = Controller.transform.position;
        _aimMouseStartPosition = useGamepadAim
            ? _aimLockPosition
            : Controller.Sensor.GetRawPointerWorldPosition();
        _isAiming = true;
        _hasActivatedSlowMotion = false;
        _aimCanceledByEnergyShortage = false;
        _isGamepadHoldAim = useGamepadAim;
        _gamepadAimDragDistance = 0f;
        _lastAimDirection = WeaponAimResolver.ResolveFallbackAimDirection(Controller);
        _gamepadAimDirection = _lastAimDirection;
        _hasGamepadAimDirection = useGamepadAim && _gamepadAimDirection.sqrMagnitude > 0.0001f;
        Controller.Movement.StopFollow();
        Controller.Movement.HoldPosition(_aimLockPosition);
    }

    public override void OnFrameTick()
    {
        if (Controller == null) return;

        WeaponView view = Controller.View;
        if (view == null) return;

        if (!_isAiming)
        {
            view.HideTrajectory();
            return;
        }

        if (Controller.CurrentMode != WeaponMode.Remote || Controller.CurrentState != WeaponState.Controlled)
        {
            view.HideTrajectory();
            return;
        }

        TryResolveCurrentAim(out Vector2 direction, out Vector2 visualTarget, out bool canFire, out _);
        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Controller.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        view.ShowTrajectory(_aimLockPosition, visualTarget);

        if (canFire && !_hasActivatedSlowMotion)
        {
            _hasActivatedSlowMotion = true;
            Controller.PublishSlowMotion();
        }
        else if (!canFire && _hasActivatedSlowMotion)
        {
            _hasActivatedSlowMotion = false;
            Controller.ResetTimeScale();
        }

        Controller.Movement.HoldPosition(_aimLockPosition);
    }

    public override void OnTick()
    {
        if (Controller == null) return;

        TickRemoteControlState();
        TickEnergy();
        TickPinnedCaptureDrain();
        TickDockWait();
        TickFocusHoldEnergy();
    }

    public override void OnRelease()
    {
        if (Controller == null || !_isAiming) return;

        TryResolveCurrentAim(out Vector2 direction, out _, out bool canFire, out bool isGamepadAim);

        if (_aimCanceledByEnergyShortage)
        {
            _aimCanceledByEnergyShortage = false;
            EndAiming();
            return;
        }

        EndAiming();

        if (!canFire)
        {
            Controller.ChangeState(WeaponState.Controlled);
            return;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            Controller.ChangeState(WeaponState.Controlled);
            return;
        }

        if (Controller.LinkEnergy != null && !Controller.LinkEnergy.TrySpendEnergy(_thrustFireCost))
        {
            Controller.ChangeState(WeaponState.Controlled);
            return;
        }

        StartPinSequence(direction.normalized, isGamepadAim);
    }

    private void TickRemoteControlState()
    {
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (_isAutoReturning || _isDockWaiting) return;
        if (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToWall) return;

        Vector2 mouseWorldPos = Controller.Sensor.GetAimWorldPosition();
        bool isEnemyPinned = Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEnemy;
        bool isEmbeddedPinned = Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEmbeddedEnemy;
        bool hasEnemyCapture = Controller.Capture != null && Controller.Capture.HasCapturedEnemy;
        bool canHover = Controller.CurrentState == WeaponState.Controlled ||
                        Controller.CurrentState == WeaponState.Slashing ||
                        (Controller.CurrentState == WeaponState.Pinned && !isEmbeddedPinned && (isEnemyPinned || hasEnemyCapture));

        if (!_isAiming && canHover)
        {
            Controller.Movement.HandleHoverMovement(
                Controller.Sensor.GetReachableAimTargetPosition(Controller.WallAndEnvironmentLayer),
                Controller.CurrentState == WeaponState.Slashing,
                Controller.WallAndEnvironmentLayer);
        }

        if (Controller.CurrentState == WeaponState.Grounded)
        {
            if (Controller.Sensor.ShouldAcquireRemoteControl(Controller.transform.position, mouseWorldPos, Controller.WallAndEnvironmentLayer))
                Controller.ChangeState(WeaponState.Controlled);
        }
        else if (Controller.CurrentState == WeaponState.Controlled && !_isAiming)
        {
            if (Controller.Sensor.ShouldReleaseRemoteControl(Controller.transform.position, mouseWorldPos, Controller.WallAndEnvironmentLayer))
                Controller.ChangeState(WeaponState.Grounded);
        }
    }

    private void TickEnergy()
    {
        WeaponLinkEnergy linkEnergy = Controller.LinkEnergy;
        if (linkEnergy == null || Controller.PlayerTransform == null) return;
        bool isWallPinned = Controller.StateMachine != null && Controller.StateMachine.IsPinnedToWall;

        bool isRemoteControlling =
            Controller.CurrentMode == WeaponMode.Remote &&
            (Controller.CurrentState == WeaponState.Controlled ||
             Controller.CurrentState == WeaponState.Slashing ||
             Controller.CurrentState == WeaponState.Pinned);

        float distance = Vector2.Distance(Controller.PlayerTransform.position, Controller.transform.position);
        linkEnergy.Tick(distance, Controller.ControlRadius, isRemoteControlling, Time.fixedDeltaTime);
        linkEnergy.ClearFrameSpendFlag();
        if (isWallPinned) return;

        if (!isRemoteControlling) return;
        if (!linkEnergy.IsControlLocked) return;
        if (!linkEnergy.IsOffline && !linkEnergy.IsDepletedRechargeMode) return;
        HandleLinkEnergyDepleted();
    }

    private void TickPinnedCaptureDrain()
    {
        if (Controller.CurrentState != WeaponState.Pinned) return;
        if (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToWall) return;
        bool isEnemyPinned = Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEnemy;
        if (!isEnemyPinned) return;
        if (Controller.Capture == null || !Controller.Capture.HasCapturedEnemy) return;
        if (Controller.LinkEnergy == null) return;

        bool keepCapturing = Controller.LinkEnergy.SpendEnergyOverTime(_captureHoldCostPerSecond, _captureHoldSpendMode);
        if (!keepCapturing && _captureHoldSpendMode == ContinuousEnergySpendMode.StopBeforeEmpty)
            StartReturnToPlayer();
    }

    private void TickDockWait()
    {
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (!_isDockWaiting) return;
        if (_dockRechargeRoutine != null) return;
        _dockRechargeRoutine = StartCoroutine(DockRechargeRoutine());
    }

    private void TickFocusHoldEnergy()
    {
        if (!_isAiming) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;
        if (_focusHoldCostPerSecond <= 0f || Controller.LinkEnergy == null) return;

        bool keepAiming = Controller.LinkEnergy.SpendEnergyOverTime(
            _focusHoldCostPerSecond,
            ContinuousEnergySpendMode.DepleteToZero,
            Time.unscaledDeltaTime);

        if (keepAiming)
            return;

        _aimCanceledByEnergyShortage = true;
        EndAiming();
        Controller.ChangeState(WeaponState.Controlled);
    }

    private bool TryResolveCurrentAim(
        out Vector2 direction,
        out Vector2 visualTarget,
        out bool canFire,
        out bool isGamepadAim)
    {
        direction = WeaponAimResolver.ResolveFallbackAimDirection(Controller);
        float maxDistance = ResolveGamepadAimMaxDistance();
        visualTarget = _aimLockPosition + direction * maxDistance;
        canFire = false;
        isGamepadAim = false;

        if (Controller == null)
            return false;

        bool useGamepadAim = _isGamepadHoldAim;
        if (useGamepadAim)
            return TryResolveGamepadAim(maxDistance, out direction, out visualTarget, out canFire, out isGamepadAim);

        Vector2 mouseWorld = Controller.Sensor.GetRawPointerWorldPosition();
        isGamepadAim = false;
        if (!WeaponAimResolver.TryResolveMouseThrustAim(
                _aimLockPosition,
                _aimMouseStartPosition,
                mouseWorld,
                Controller.ThrustDragThreshold,
                out direction,
                out visualTarget,
                out canFire))
        {
            return false;
        }
        _lastAimDirection = direction;
        return true;
    }

    private bool TryResolveGamepadAim(
        float maxDistance,
        out Vector2 direction,
        out Vector2 visualTarget,
        out bool canFire,
        out bool isGamepadAim)
    {
        _isGamepadHoldAim = true;
        isGamepadAim = true;

        Vector2 fallbackDirection = WeaponAimResolver.ResolveFallbackAimDirection(Controller);
        WeaponAimResolver.TryResolveGamepadThrustAim(
            fallbackDirection,
            _gamepadAimDeadzone,
            maxDistance,
            Controller.ThrustDragThreshold,
            ref _hasGamepadAimDirection,
            ref _gamepadAimDirection,
            out direction,
            out Vector2 relativeVisualTarget,
            out canFire,
            out _gamepadAimDragDistance);

        visualTarget = _aimLockPosition + relativeVisualTarget;
        if (_hasGamepadAimDirection)
            _lastAimDirection = _gamepadAimDirection;
        return true;
    }

    private float ResolveGamepadAimMaxDistance()
    {
        if (Controller == null)
            return 0.01f;

        float ratio = Mathf.Max(0.01f, _gamepadAimMaxDistanceRatio);
        float maxDistance = Mathf.Max(Controller.ControlRadius * ratio, Controller.ThrustDragThreshold + 0.01f);
        return Mathf.Max(0.01f, maxDistance);
    }

    public void HandleLinkEnergyDepleted()
    {
        EndAiming();
        Vector2 deathKnockback = Controller.PlayerTransform != null
            ? ((Vector2)Controller.transform.position - (Vector2)Controller.PlayerTransform.position).normalized * 8f
            : Vector2.zero;
        Controller.EmbeddedAttack?.ReleaseWithoutDamage();
        Controller.Capture?.ExecuteCapturedEnemies(deathKnockback);
        Controller.transform.SetParent(null, true);
        Controller.ChangeState(WeaponState.Grounded);

        if (Controller.LinkEnergy == null || Controller.LinkEnergy.DepletionMode == LinkEnergyDepletionMode.DropGrounded)
            return;

        StartAutoReturnToDock();
    }

    private bool TryHandlePinnedAction()
    {
        if (Controller == null) return false;
        if (Controller.CurrentState != WeaponState.Pinned) return false;

        if (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToWall)
        {
            if (!Controller.Sensor.IsPlayerInRange(Controller.transform.position))
                return true;

            StartReturnToPlayer();
            return true;
        }

        bool isEnemyPinned = (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEnemy) ||
                             (Controller.Capture != null && Controller.Capture.HasCapturedEnemy);
        if (!isEnemyPinned) return false;

        if (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEmbeddedEnemy)
        {
            if (Controller.EmbeddedAttack != null)
                return Controller.EmbeddedAttack.TryHandlePinnedAction();

            return true;
        }

        if (!Controller.Sensor.IsPlayerInRange(Controller.transform.position)) return true;

        if (Controller.Capture != null && Controller.Capture.HasCapturedEnemy)
        {
            if (TryExecuteCaptureFinisher()) return true;
        }

        StartReturnToPlayer();
        return true;
    }

    private bool TryExecuteCaptureFinisher()
    {
        if (Controller == null) return false;
        if (Controller.StateMachine == null) return false;
        if (Controller.CurrentState != WeaponState.Pinned) return false;
        if (!Controller.StateMachine.IsPinnedToEnemy) return false;
        if (Controller.Capture == null || !Controller.Capture.HasCapturedEnemy) return false;
        if (_isFinisherRunning) return true;

        WeaponSensor sensor = Controller.Sensor;
        WeaponCombat combat = Controller.Combat;
        WeaponMovement movement = Controller.Movement;
        WeaponCapture capture = Controller.Capture;
        if (sensor == null || combat == null || movement == null || capture == null) return false;

        _isFinisherRunning = true;
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _finisherHitStopDuration });
        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _finisherShakeIntensity });

        Vector2 pivot = sensor.GetAimWorldPosition();
        float radius = Vector2.Distance(pivot, Controller.transform.position);
        radius = Mathf.Max(radius, combat.SlashRadius * Mathf.Max(0.1f, _finisherRadiusMultiplier));

        var captured = capture.GetCapturedEnemies();
        List<Transform> pinnedTargets = new List<Transform>(captured.Count);
        foreach (var enemy in captured)
        {
            if (enemy == null) continue;
            if (!enemy.CanExecuteCaptureFinisher()) continue;
            pinnedTargets.Add(enemy.transform);
        }

        if (pinnedTargets.Count == 0)
        {
            _isFinisherRunning = false;
            return false;
        }

        movement.ExecuteOrbitFinisher(
            pivot,
            radius,
            Mathf.Max(0.01f, _finisherOrbitDuration),
            () =>
            {
                combat.PerformSpinFinisher(Controller.transform.position, pinnedTargets, Controller.WallAndEnvironmentLayer);
                capture.UnbindAll();
            },
            () =>
            {
                _isFinisherRunning = false;
                if (_lastPinShotWasGamepad)
                    Controller.AimCursor?.ResetGamepadCursorNearPlayer(_lastAimDirection);
                Controller.ChangeState(WeaponState.Controlled);
            });

        return true;
    }

    private void StartPinSequence(Vector2 direction, bool isGamepadAim)
    {
        if (Controller.Movement == null || Controller.Combat == null || Controller.Capture == null || Controller.StateMachine == null) return;

        _pierceHitTargets.Clear();
        _groggyEnteredDuringCurrentPin.Clear();
        _currentCaptureWeight = 0f;
        _captureWeightBlocked = false;
        _lastPinShotWasGamepad = isGamepadAim;
        _snapCursorOnCurrentReturnFlow = false;
        Controller.StateMachine.ClearPinSource();
        Controller.ChangeState(WeaponState.PinningFlight);
        Controller.View?.SetLaunchedSorting();
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _pinStartHitStopDuration });

        Controller.Movement.ExecutePinFlight(
            direction,
            Controller.Combat.PinSpeed,
            Controller.Combat.EnemyLayer,
            Controller.WallAndEnvironmentLayer,
            () => Controller.PlayerTransform != null ? (Vector2)Controller.PlayerTransform.position : (Vector2)Controller.transform.position,
            Controller.ControlRadius * (1f + Mathf.Max(0f, _extraFlightRangeRatio)),
            _rangeBrakeDeceleration,
            _rangeAutoReturnSpeedThreshold,
            null,
            targetTransform =>
            {
                if (targetTransform == null) return false;
                if (!targetTransform.TryGetComponent<EnemyBase>(out var enemy))
                    return false;

                if (enemy.IsGroggy)
                {
                    if (_groggyEnteredDuringCurrentPin.Contains(enemy))
                        return false;

                    return TryHandleGroggyEnemyPierce(enemy, targetTransform, isGamepadAim);
                }

                bool damageApplied = Controller.Combat.PerformPinDamage(targetTransform, Controller.transform.position, direction, _pierceHitTargets);
                if (!damageApplied)
                    return false;
                if (enemy.IsGroggy)
                {
                    _groggyEnteredDuringCurrentPin.Add(enemy);
                    return false;
                }

                if (enemy.ShouldPiercePassThrough())
                    return false;

                if (enemy.ShouldPierceStick() && enemy.CanBeCapturedByPierce())
                {
                    if (!CanCaptureByWeight(enemy))
                        return false;

                    Controller.Capture.BindEnemy(targetTransform);
                    AddCaptureWeight(enemy);
                    Controller.StateMachine.SetPinSource(WeaponPinSource.EnemyCapture);

                    if (isGamepadAim)
                        SnapGamepadCapturedAimCursor();

                    return false;
                }

                return false;
            },
            _ =>
            {
                EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _pinWallHitShakeIntensity });
                if (Controller.Capture.HasCapturedEnemy)
                {
                    if (isGamepadAim)
                    {
                        StartGamepadCapturePullReturnToPinned();
                        return;
                    }
                    Controller.StateMachine.SetPinSource(WeaponPinSource.EnemyCapture);
                    Controller.ChangeState(WeaponState.Pinned);
                    return;
                }

                Controller.Capture.UnbindAll(forcePhysicsRestore: true);
                Controller.StateMachine.SetPinSource(WeaponPinSource.Wall);
                Controller.ChangeState(WeaponState.Pinned);
            },
            () =>
            {
                if (Controller.Capture.HasCapturedEnemy)
                {
                    if (isGamepadAim)
                    {
                        StartGamepadCapturePullReturnToPinned();
                        return;
                    }
                    Controller.StateMachine.SetPinSource(WeaponPinSource.EnemyCapture);
                    Controller.ChangeState(WeaponState.Pinned);
                    return;
                }

                Controller.StateMachine.ClearPinSource();
                StartRangeExceededReturnToDock();
            });
    }

    private void StartReturnToPlayer()
    {
        Controller.StateMachine?.ClearPinSource();
        Controller.Capture?.UnbindAll(forcePhysicsRestore: true);

        bool shouldSnapCursor = _lastPinShotWasGamepad;
        _snapCursorOnCurrentReturnFlow = shouldSnapCursor;
        if (shouldSnapCursor)
            Controller.AimCursor?.SnapToPlayerPosition();
        Controller.ChangeState(WeaponState.Returning);
        Controller.View?.RestoreDefaultSorting();

        Controller.Movement.ExecuteReturn(
            GetContextualReturnTargetPosition,
            Controller.ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _dockArrivalDistance,
            _ =>
            {
                if (shouldSnapCursor)
                    Controller.AimCursor?.SnapToPlayerPosition();
                _snapCursorOnCurrentReturnFlow = false;
                Controller.ChangeState(WeaponState.Controlled);
            });
    }

    private void SnapGamepadCapturedAimCursor()
    {
        if (Controller == null)
            return;

        _snapCursorOnCurrentReturnFlow = true;

        if (Controller.AimCursor != null)
            Controller.AimCursor.ResetGamepadCursorNearPlayer(_lastAimDirection);
    }

    private Vector2 GetReturnToPlayerTargetPosition()
    {
        Transform target = Controller.DroneDockPivot != null
            ? Controller.DroneDockPivot
            : Controller.PlayerTransform;

        return target != null
            ? (Vector2)target.position
            : Controller.Rigidbody.position;
    }

    private Vector2 GetContextualReturnTargetPosition()
    {
        if (!_lastPinShotWasGamepad)
        {
            if (Controller != null && Controller.AimCursor != null)
                return Controller.AimCursor.AimWorldPosition;

            if (Controller != null && Controller.Sensor != null)
                return Controller.Sensor.GetReachableAimTargetPosition(Controller.WallAndEnvironmentLayer);
        }

        return GetReturnToPlayerTargetPosition();
    }

    private Vector2 GetGamepadCapturedReturnTargetPosition()
    {
        if (Controller != null && Controller.AimCursor != null)
            return Controller.AimCursor.AimWorldPosition;

        if (Controller != null && Controller.PlayerTransform != null)
        {
            Vector2 direction = _lastAimDirection.sqrMagnitude > 0.0001f
                ? _lastAimDirection.normalized
                : WeaponAimResolver.ResolveFallbackAimDirection(Controller);
            float distance = Mathf.Max(0.5f, Controller.ControlRadius * 0.45f);
            return (Vector2)Controller.PlayerTransform.position + direction * distance;
        }

        return Controller != null && Controller.Rigidbody != null
            ? Controller.Rigidbody.position
            : Vector2.zero;
    }

    private void StartRangeExceededReturnToDock()
    {
        Controller.StateMachine?.ClearPinSource();
        _snapCursorOnCurrentReturnFlow = _lastPinShotWasGamepad;
        if (_lastPinShotWasGamepad)
        {
            Controller.AimCursor?.ResetGamepadCursorNearPlayer(_lastAimDirection);
            StartGamepadRangeExceededPullReturnToControl();
            return;
        }

        Controller.Capture?.UnbindAll(forcePhysicsRestore: true);
        Controller.ChangeState(WeaponState.Returning);
        Controller.View?.RestoreDefaultSorting();
        Controller.Movement.ExecuteReturn(
                    GetContextualRangeExceededTargetPosition,
            Controller.ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _dockArrivalDistance,
            _ => Controller.ChangeState(WeaponState.Controlled));
    }

    private Vector2 GetContextualRangeExceededTargetPosition()
    {
        if (!_lastPinShotWasGamepad)
            return GetContextualReturnTargetPosition();

        return GetDockTargetPosition();
    }

    private void StartAutoReturnToDock()
    {
        _isAutoReturning = true;
        _snapCursorOnCurrentReturnFlow = false;
        Controller.ChangeState(WeaponState.Returning);
        Controller.View?.RestoreDefaultSorting();

        Controller.Movement.ExecuteReturn(
            GetDockTargetPosition,
            Controller.ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _dockArrivalDistance,
            _ =>
            {
                _isAutoReturning = false;
                DockAndWait();
            });
    }

    private void StartGamepadCapturePullReturnToPinned()
    {
        if (Controller == null || Controller.Movement == null)
            return;

        Controller.StateMachine?.SetPinSource(WeaponPinSource.EnemyCapture);
        Controller.AimCursor?.ResetGamepadCursorNearPlayer(_lastAimDirection);
        Controller.ChangeState(WeaponState.Returning);
        Controller.View?.RestoreDefaultSorting();
        Controller.Movement.ExecuteCapturePullReturn(
            GetGamepadCapturedReturnTargetPosition,
            Controller.ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _dockArrivalDistance,
            _ =>
            {
                Controller.StateMachine?.SetPinSource(WeaponPinSource.EnemyCapture);
                Controller.ChangeState(WeaponState.Pinned);
            });
    }

    private void StartGamepadRangeExceededPullReturnToControl()
    {
        if (Controller == null || Controller.Movement == null)
            return;

        Controller.ChangeState(WeaponState.Returning);
        Controller.View?.RestoreDefaultSorting();
        Controller.Movement.ExecuteCapturePullReturn(
            GetGamepadCapturedReturnTargetPosition,
            Controller.ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= _dockArrivalDistance,
            _ => Controller.ChangeState(WeaponState.Controlled));
    }

    private Vector2 GetDockTargetPosition()
    {
        Transform dock = Controller.DroneDockPivot != null ? Controller.DroneDockPivot : Controller.PlayerTransform;
        return dock != null ? (Vector2)dock.position : Controller.Rigidbody.position;
    }

    private void DockAndWait()
    {
        Transform dock = Controller.DroneDockPivot != null ? Controller.DroneDockPivot : Controller.PlayerTransform;
        if (dock == null)
        {
            Controller.ChangeState(WeaponState.Grounded);
            return;
        }

        Controller.transform.position = dock.position;
        Controller.transform.rotation = dock.rotation;
        Controller.transform.SetParent(dock, true);
        if (_snapCursorOnCurrentReturnFlow)
            Controller.AimCursor?.SnapToPlayerPosition();
        _isDockWaiting = true;
        _dockRechargeRoutine = null;
    }

    private IEnumerator DockRechargeRoutine()
    {
        float duration = Mathf.Max(0.01f, _dockRechargeDuration);
        if (Controller.LinkEnergy != null)
            yield return StartCoroutine(Controller.LinkEnergy.RechargeToFullAndUnlockOverDuration(duration));

        _isDockWaiting = false;
        _dockRechargeRoutine = null;
        Controller.transform.SetParent(null, true);
        if (_snapCursorOnCurrentReturnFlow)
            Controller.AimCursor?.SnapToPlayerPosition();
        _snapCursorOnCurrentReturnFlow = false;
        Controller.ChangeState(WeaponState.Grounded);
    }

    private bool TryHandleGroggyEnemyPierce(EnemyBase enemy, Transform enemyTransform, bool isGamepadAim)
    {
        if (enemy == null || enemyTransform == null)
            return false;

        switch (enemy.GroggyRightClickAction)
        {
            case GroggyRightClickActionType.Capture:
                if (!enemy.TryHandleGroggyPierceInteraction())
                    return false;
                if (!CanCaptureByWeight(enemy))
                    return false;

                Controller.Capture.BindEnemy(enemyTransform);
                AddCaptureWeight(enemy);
                Controller.StateMachine.SetPinSource(WeaponPinSource.EnemyCapture);
                if (isGamepadAim)
                    SnapGamepadCapturedAimCursor();

                return false;

            case GroggyRightClickActionType.EmbeddedAttack:
                if (!enemy.TryHandleGroggyPierceInteraction())
                    return false;

                if (Controller.EmbeddedAttack == null || !Controller.EmbeddedAttack.BeginEmbeddedPin(enemy))
                    return false;

                Controller.StateMachine.SetPinSource(WeaponPinSource.EnemyEmbedded);
                Controller.ChangeState(WeaponState.Pinned);
                return true;

            case GroggyRightClickActionType.None:
            default:
                return false;
        }
    }

    private void EndAiming()
    {
        if (!_isAiming) return;
        _isAiming = false;
        Controller.View?.HideTrajectory();
        if (_hasActivatedSlowMotion)
            Controller.ResetTimeScale();
        _hasActivatedSlowMotion = false;
        _isGamepadHoldAim = false;
        _gamepadAimDragDistance = 0f;
    }

    private bool CanCaptureByWeight(EnemyBase enemy)
    {
        if (enemy == null)
            return false;

        if (_captureWeightBlocked)
            return false;

        float weight = Mathf.Max(1f, enemy.CaptureWeight);
        if (_currentCaptureWeight + weight > _captureWeightLimit)
        {
            _captureWeightBlocked = true;
            return false;
        }

        return true;
    }

    private void AddCaptureWeight(EnemyBase enemy)
    {
        if (enemy == null)
            return;

        _currentCaptureWeight += Mathf.Max(1f, enemy.CaptureWeight);
    }
}

