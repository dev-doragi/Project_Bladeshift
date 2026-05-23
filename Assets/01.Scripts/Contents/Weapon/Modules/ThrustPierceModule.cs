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

    private readonly HashSet<IDamageable> _pierceHitTargets = new HashSet<IDamageable>();
    private Vector2 _aimLockPosition;
    private Vector2 _aimMouseStartPosition;
    private bool _isAiming;
    private bool _isAutoReturning;
    private bool _isDockWaiting;
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

        Vector2 mouseWorldPos = Controller.Sensor.GetMouseWorldPosition();
        bool hasEnemy = Controller.Capture != null && Controller.Capture.GetCapturedEnemies().Count > 0;
        bool canHover = Controller.CurrentState == WeaponState.Controlled ||
                        Controller.CurrentState == WeaponState.Slashing ||
                        (Controller.CurrentState == WeaponState.Pinned && hasEnemy);

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

        bool isRemoteControlling =
            Controller.CurrentMode == WeaponMode.Remote &&
            (Controller.CurrentState == WeaponState.Controlled ||
             Controller.CurrentState == WeaponState.Slashing ||
             Controller.CurrentState == WeaponState.Pinned);

        float distance = Vector2.Distance(Controller.PlayerTransform.position, Controller.transform.position);
        linkEnergy.Tick(distance, Controller.ControlRadius, isRemoteControlling, Time.fixedDeltaTime);
        linkEnergy.ClearFrameSpendFlag();

        if (!linkEnergy.IsControlLocked || !isRemoteControlling) return;
        HandleLinkEnergyDepleted();
    }

    private void TickPinnedCaptureDrain()
    {
        if (Controller.CurrentState != WeaponState.Pinned) return;
        if (Controller.Capture == null || Controller.Capture.GetCapturedEnemies().Count == 0) return;
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
        if (!Controller.Sensor.IsPlayerInRange(Controller.transform.position)) return true;

        if (Controller.Capture != null && Controller.Capture.GetCapturedEnemies().Count > 0)
        {
            WeaponActionModule remotePrimary = GetComponent<SpinSlashModule>();
            if (remotePrimary != null && remotePrimary.TryExecutePinnedFinisher()) return true;
        }

        StartReturnToPlayer();
        return true;
    }

    private void StartPinSequence(Vector2 direction)
    {
        if (Controller.Movement == null || Controller.Combat == null || Controller.Capture == null) return;

        _pierceHitTargets.Clear();
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
                if (!Controller.Combat.PerformPinDamage(targetTransform, Controller.transform.position, direction, _pierceHitTargets))
                    return false;

                if (targetTransform != null)
                    Controller.Capture.BindEnemy(targetTransform);

                return false;
            },
            _ =>
            {
                EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _pinWallHitShakeIntensity });
                Controller.ChangeState(WeaponState.Pinned);
            },
            () =>
            {
                if (Controller.Capture != null && Controller.Capture.GetCapturedEnemies().Count > 0)
                {
                    Controller.ChangeState(WeaponState.Pinned);
                    return;
                }

                StartReturnToPlayer();
            });
    }

    private void StartReturnToPlayer()
    {
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
