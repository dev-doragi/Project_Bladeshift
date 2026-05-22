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

    public override bool TryHandlePinnedPrimary()
    {
        return TryHandlePinnedAction();
    }

    public override bool TryHandlePinnedSecondary()
    {
        return TryHandlePinnedAction();
    }

    public override void OnPress()
    {
        if (Controller == null) return;
        if (!Controller.CanStartThrustAim()) return;

        Controller.BeginThrustAim();
    }

    public override void OnTick()
    {
        if (Controller == null) return;
        if (LinkEnergy == null) return;
        if (Controller.CurrentState != WeaponState.Pinned) return;
        if (!Controller.HasCapturedEnemies()) return;

        bool keepCapturing = LinkEnergy.SpendEnergyOverTime(_captureHoldCostPerSecond, _captureHoldSpendMode);
        if (!keepCapturing && _captureHoldSpendMode == ContinuousEnergySpendMode.StopBeforeEmpty)
        {
            Controller.ExecutePinnedRecall();
        }
    }

    public override void OnRelease()
    {
        if (Controller == null || Sensor == null) return;
        if (!Controller.CanReleaseThrustAim()) return;

        Vector2 mouseWorldPos = Sensor.GetMouseWorldPosition();
        float dragDistance = Controller.GetThrustDragDistance(mouseWorldPos);
        Controller.EndThrustAimAndReset();

        if (dragDistance < Controller.ThrustDragThreshold)
        {
            Controller.RestoreControlledAfterThrustCancel();
            return;
        }

        if (LinkEnergy != null && !LinkEnergy.TrySpendEnergy(_thrustFireCost))
        {
            Controller.RestoreControlledAfterThrustCancel();
            return;
        }

        Vector2 direction = (mouseWorldPos - Controller.FixedAimPosition).normalized;
        StartPinSequence(direction);
    }

    private bool TryHandlePinnedAction()
    {
        if (Controller == null) return false;
        if (Controller.CurrentState != WeaponState.Pinned) return false;
        if (Controller.IsAttacking) return true;
        if (!Controller.IsPlayerInControlRange()) return true;

        if (Controller.HasCapturedEnemies())
        {
            Controller.ExecutePinnedFinisher();
        }
        else
        {
            Controller.ExecutePinnedRecall();
        }

        return true;
    }

    private void StartPinSequence(Vector2 direction)
    {
        if (Controller == null || Movement == null || Combat == null || Capture == null) return;

        Controller.SetThrustAimingFlag(false);
        Controller.HideTrajectoryFromModule();
        Controller.ResetTimeScaleFromModule();
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _pinStartHitStopDuration });
        Controller.ClearHitTargets();
        Controller.ChangeStateFromModule(WeaponState.PinningFlight);
        Controller.SetAttackingFlag(true);
        Controller.SetPiercingAttackActive(true);

        Movement.ExecutePinFlight(
            direction,
            Combat.PinSpeed,
            Combat.EnemyLayer,
            Controller.WallAndEnvironmentLayer,
            () =>
            {
                Transform player = Sensor.GetPlayerTransform();
                return player != null ? (Vector2)player.position : (Vector2)Controller.transform.position;
            },
            Controller.ControlRadius * (1f + Mathf.Max(0f, _extraFlightRangeRatio)),
            _rangeBrakeDeceleration,
            _rangeAutoReturnSpeedThreshold,
            () =>
            {
                Controller.SetPiercingAttackActive(false);
            },
            targetTransform =>
            {
                if (!Combat.PerformPinDamage(targetTransform, Controller.transform.position, direction, Controller.HitTargets)) return false;
                if (targetTransform != null)
                {
                    Capture.BindEnemy(targetTransform);
                }
                return false;
            },
            hitTransform =>
            {
                Controller.SetPiercingAttackActive(false);
                EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _pinWallHitShakeIntensity });
                Controller.SetAttackingFlag(false);
                Controller.ChangeStateFromModule(WeaponState.Pinned);
            },
            () =>
            {
                Controller.SetPiercingAttackActive(false);
                Controller.SetAttackingFlag(false);
                Controller.SetThrustAimingFlag(false);
                Controller.HideTrajectoryFromModule();
                Controller.ResetTimeScaleFromModule();
                if (Controller.HasCapturedEnemies())
                {
                    Controller.ChangeStateFromModule(WeaponState.Pinned);
                    return;
                }

                Controller.StartReturnFromModule();
            });
    }
}
