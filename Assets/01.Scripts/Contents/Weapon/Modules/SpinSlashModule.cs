using UnityEngine;

public class SpinSlashModule : WeaponActionModule
{
    [SerializeField] private float _slashHoldCostPerSecond = 6f;
    [SerializeField] private ContinuousEnergySpendMode _slashHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;
    [Header("Finisher Tuning")]
    [SerializeField] private float _finisherHitStopDuration = 0.2f;
    [SerializeField] private float _finisherRadiusMultiplier = 1.8f;
    [SerializeField] private float _finisherOrbitDuration = 0.35f;
    [SerializeField] private ShakeIntensity _finisherShakeIntensity = ShakeIntensity.Strong;
    private bool _isSlashing;
    private bool _isFinisherRunning;

    public override void OnPress()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;
        if (_isSlashing || _isFinisherRunning) return;

        _isSlashing = true;
        Controller.Combat.ResetTickTimer();
        Controller.ChangeState(WeaponState.Slashing);
    }

    public override void OnTick()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Slashing || !_isSlashing) return;

        WeaponLinkEnergy energy = Controller.LinkEnergy;
        if (energy != null && !energy.SpendEnergyOverTime(_slashHoldCostPerSecond, _slashHoldSpendMode))
        {
            EndSpinSlash();
            return;
        }

        Controller.Combat.TryTickSpinDamage(Controller.transform.position, Controller.transform.eulerAngles.z);
        Controller.Combat.DefendProjectiles(Controller.transform.position);
        Controller.Movement.HandleHoverMovement(
            Controller.Sensor.GetClampedTargetPosition(Controller.WallAndEnvironmentLayer),
            true,
            Controller.WallAndEnvironmentLayer);
        Controller.Movement.ApplySpinRotation(Controller.Combat.SpinSpeed);
    }

    public override void OnRelease()
    {
        EndSpinSlash();
    }

    public override bool TryExecutePinnedFinisher()
    {
        if (Controller == null) return false;
        WeaponSensor sensor = Controller.Sensor;
        WeaponCombat combat = Controller.Combat;
        WeaponMovement movement = Controller.Movement;
        WeaponCapture capture = Controller.Capture;
        if (sensor == null || combat == null || movement == null || capture == null) return false;
        if (Controller.CurrentState != WeaponState.Pinned) return false;
        if (capture.GetCapturedEnemies().Count <= 0) return false;
        if (_isFinisherRunning) return true;

        _isFinisherRunning = true;
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _finisherHitStopDuration });
        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _finisherShakeIntensity });

        Vector2 pivot = sensor.GetMouseWorldPosition();
        float radius = Vector2.Distance(pivot, Controller.transform.position);
        radius = Mathf.Max(radius, combat.SlashRadius * Mathf.Max(0.1f, _finisherRadiusMultiplier));

        var captured = capture.GetCapturedEnemies();
        System.Collections.Generic.List<Transform> pinnedTargets = new System.Collections.Generic.List<Transform>(captured.Count);
        foreach (var enemy in captured)
        {
            if (enemy != null)
                pinnedTargets.Add(enemy.transform);
        }

        movement.ExecuteOrbitFinisher(
            pivot,
            radius,
            Mathf.Max(0.01f, _finisherOrbitDuration),
            () =>
            {
                combat.PerformSpinFinisher(Controller.transform.position, pinnedTargets);
                capture.UnbindAll();
            },
            () =>
            {
                _isFinisherRunning = false;
                Controller.ChangeState(WeaponState.Controlled);
            });

        return true;
    }

    private void EndSpinSlash()
    {
        if (Controller == null) return;
        if (!_isSlashing) return;

        _isSlashing = false;
        if (Controller.CurrentState == WeaponState.Slashing)
            Controller.ChangeState(WeaponState.Controlled);
    }
}
