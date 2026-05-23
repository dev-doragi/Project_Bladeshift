using System.Collections.Generic;
using UnityEngine;

public class ThrustPierceModule : WeaponActionModule
{
    [SerializeField] private float _thrustFireCost = 18f;
    [SerializeField] private float _captureHoldCostPerSecond = 10f;
    [SerializeField] private ContinuousEnergySpendMode _captureHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;
    [Header("Pin Flight Tuning")]
    [SerializeField] private float _pinStartHitStopDuration = 0.15f;
    [SerializeField] private ShakeIntensity _pinWallHitShakeIntensity = ShakeIntensity.Weak;
    [SerializeField, Range(0f, 1f)] private float _extraFlightRangeRatio = 0.2f;
    [SerializeField] private float _rangeBrakeDeceleration = 45f;
    [SerializeField] private float _rangeAutoReturnSpeedThreshold = 2f;
    [SerializeField] private float _dockArrivalDistance = 0.6f;
    [Header("Finisher Tuning")]
    [SerializeField] private float _finisherHitStopDuration = 0.2f;
    [SerializeField] private float _finisherRadiusMultiplier = 1.8f;
    [SerializeField] private float _finisherOrbitDuration = 0.35f;
    [SerializeField] private ShakeIntensity _finisherShakeIntensity = ShakeIntensity.Strong;

    private readonly HashSet<IDamageable> _pierceHitTargets = new HashSet<IDamageable>();
    private Vector2 _aimLockPosition;
    private Vector2 _aimMouseStartPosition;
    private bool _isAiming;
    private bool _isAutoReturning;
    private bool _isDockWaiting;
    private bool _isFinisherRunning;
    private float _dockWaitTimer;

    public override bool TryHandlePinnedPrimary() => TryHandlePinnedAction();
    public override bool TryHandlePinnedSecondary() => TryHandlePinnedAction();

    public override void OnPress()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (_isAiming || _isAutoReturning || _isDockWaiting) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;

        _aimLockPosition = Controller.transform.position;
        _aimMouseStartPosition = Controller.Sensor.GetMouseWorldPosition();
        _isAiming = true;
        Controller.Movement.StopFollow();
        Controller.PublishSlowMotion();
    }

    public override void OnTick()
    {
        if (Controller == null) return;

        TickModeSpecificVisuals();
        TickRemoteControlState();
        TickEnergy();
        TickPinnedCaptureDrain();
        TickDockWait();
    }

    public override void OnRelease()
    {
        if (Controller == null || !_isAiming) return;

        Vector2 releaseMousePos = Controller.Sensor.GetMouseWorldPosition();
        float dragDistance = Vector2.Distance(_aimMouseStartPosition, releaseMousePos);
        EndAiming();

        if (dragDistance < Controller.ThrustDragThreshold)
        {
            Controller.ChangeState(WeaponState.Controlled);
            return;
        }

        if (Controller.LinkEnergy != null && !Controller.LinkEnergy.TrySpendEnergy(_thrustFireCost))
        {
            Controller.ChangeState(WeaponState.Controlled);
            return;
        }

        Vector2 direction = (releaseMousePos - _aimLockPosition).normalized;
        StartPinSequence(direction);
    }

    private void TickModeSpecificVisuals()
    {
        WeaponView view = Controller.View;
        if (view == null) return;

        if (_isAiming && Controller.CurrentMode == WeaponMode.Remote && Controller.CurrentState == WeaponState.Controlled)
        {
            Vector2 mouseWorldPos = Controller.Sensor.GetMouseWorldPosition();
            Vector2 dir = mouseWorldPos - (Vector2)Controller.transform.position;
            if (dir.sqrMagnitude > 0f)
                Controller.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            view.ShowTrajectory(Controller.transform.position, mouseWorldPos);
            Controller.Movement.HoldPosition(_aimLockPosition);
        }
        else
        {
            view.HideTrajectory();
        }
    }

    private void TickRemoteControlState()
    {
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (_isAutoReturning || _isDockWaiting) return;
        if (Controller.StateMachine != null && Controller.StateMachine.IsPinnedToWall) return;

        Vector2 mouseWorldPos = Controller.Sensor.GetMouseWorldPosition();
        bool isEnemyPinned = Controller.StateMachine != null && Controller.StateMachine.IsPinnedToEnemy;
        bool hasEnemyCapture = Controller.Capture != null && Controller.Capture.HasCapturedEnemy;
        bool canHover = Controller.CurrentState == WeaponState.Controlled ||
                        Controller.CurrentState == WeaponState.Slashing ||
                        (Controller.CurrentState == WeaponState.Pinned && (isEnemyPinned || hasEnemyCapture));

        if (!_isAiming && canHover)
        {
            Controller.Movement.HandleHoverMovement(
                Controller.Sensor.GetClampedTargetPosition(Controller.WallAndEnvironmentLayer),
                Controller.CurrentState == WeaponState.Slashing,
                Controller.WallAndEnvironmentLayer);
        }

        if (Controller.CurrentState == WeaponState.Grounded)
        {
            if (CanStartControl() && Controller.Sensor.ShouldAcquireControl(Controller.transform.position, mouseWorldPos, Controller.WallAndEnvironmentLayer))
                Controller.ChangeState(WeaponState.Controlled);
        }
        else if (Controller.CurrentState == WeaponState.Controlled && !_isAiming)
        {
            if (Controller.Sensor.ShouldReleaseControl(Controller.transform.position, mouseWorldPos, Controller.WallAndEnvironmentLayer))
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

        if (!linkEnergy.IsControlLocked || !isRemoteControlling) return;
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
        if (!_isDockWaiting) return;
        _dockWaitTimer += Time.fixedDeltaTime;
        if (_dockWaitTimer < 1f) return;

        _isDockWaiting = false;
        _dockWaitTimer = 0f;
        Controller.LinkEnergy?.RestoreFullAndUnlock();
        Controller.transform.SetParent(null, true);
        Controller.ChangeState(WeaponState.Grounded);
    }

    private bool CanStartControl()
    {
        return Controller.LinkEnergy == null || Controller.LinkEnergy.CanStartControl;
    }

    private void HandleLinkEnergyDepleted()
    {
        EndAiming();
        Controller.Capture?.UnbindAll(forcePhysicsRestore: true);
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

        Vector2 pivot = sensor.GetMouseWorldPosition();
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
                Controller.ChangeState(WeaponState.Controlled);
            });

        return true;
    }

    private void StartPinSequence(Vector2 direction)
    {
        if (Controller.Movement == null || Controller.Combat == null || Controller.Capture == null || Controller.StateMachine == null) return;

        _pierceHitTargets.Clear();
        Controller.StateMachine.ClearPinSource();
        Controller.ChangeState(WeaponState.PinningFlight);
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

                bool damageApplied = Controller.Combat.PerformPinDamage(targetTransform, Controller.transform.position, direction, _pierceHitTargets);
                if (!damageApplied)
                    return false;

                if (!targetTransform.TryGetComponent<EnemyBase>(out var enemy))
                    return false;

                if (enemy.ShouldPiercePassThrough())
                    return false;

                if (enemy.ShouldPierceStick() && enemy.CanBeCapturedByPierce())
                {
                    Controller.Capture.BindEnemy(targetTransform);
                    Controller.StateMachine.SetPinSource(WeaponPinSource.Enemy);
                    // Keep flying while carrying captured enemy.
                    // Pinned state is finalized only on wall hit or range-end.
                    return false;
                }

                return false;
            },
            _ =>
            {
                EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _pinWallHitShakeIntensity });
                if (Controller.Capture.HasCapturedEnemy)
                {
                    Controller.StateMachine.SetPinSource(WeaponPinSource.Enemy);
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
                    Controller.StateMachine.SetPinSource(WeaponPinSource.Enemy);
                    Controller.ChangeState(WeaponState.Pinned);
                    return;
                }

                Controller.StateMachine.ClearPinSource();
                StartReturnToPlayer();
            });
    }

    private void StartReturnToPlayer()
    {
        Controller.StateMachine?.ClearPinSource();
        Controller.Capture?.UnbindAll(forcePhysicsRestore: true);
        Controller.ChangeState(WeaponState.Returning);
        Controller.Movement.ExecuteReturn(
            () => Controller.Sensor.GetClampedTargetPosition(Controller.WallAndEnvironmentLayer),
            Controller.ControlRadius,
            (currentPos, mousePos) => Controller.Sensor.IsMouseHovering(currentPos, mousePos),
            _ => Controller.ChangeState(WeaponState.Controlled));
    }

    private void StartAutoReturnToDock()
    {
        _isAutoReturning = true;
        Controller.ChangeState(WeaponState.Returning);

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
        _dockWaitTimer = 0f;
        _isDockWaiting = true;
    }

    private void EndAiming()
    {
        if (!_isAiming) return;
        _isAiming = false;
        Controller.View?.HideTrajectory();
        Controller.ResetTimeScale();
    }
}
