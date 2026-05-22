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

    public override void OnPress()
    {
        if (Controller == null) return;
        if (!Controller.CanStartSpinSlash()) return;

        Controller.BeginSpinSlash();
    }

    public override void OnTick()
    {
        if (Controller == null) return;
        if (Controller.CurrentState != WeaponState.Slashing) return;

        if (LinkEnergy != null && !LinkEnergy.SpendEnergyOverTime(_slashHoldCostPerSecond, _slashHoldSpendMode))
        {
            Controller.EndSpinSlash();
            return;
        }

        Controller.TickSpinSlashCombat();
    }

    public override void OnRelease()
    {
        if (Controller == null) return;
        Controller.EndSpinSlash();
    }

    public override bool TryExecutePinnedFinisher()
    {
        if (Controller == null || Sensor == null || Combat == null || Movement == null || Capture == null) return false;
        if (Controller.CurrentState != WeaponState.Pinned) return false;
        if (!Controller.HasCapturedEnemies()) return false;
        if (Controller.IsAttacking) return true;

        Controller.SetAttackingFlag(true);
        EventBus.Instance?.Publish(new HitStopEvent { Duration = _finisherHitStopDuration });
        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = _finisherShakeIntensity });

        Vector2 pivot = Sensor.GetMouseWorldPosition();
        float radius = Vector2.Distance(pivot, Controller.transform.position);
        radius = Mathf.Max(radius, Combat.SlashRadius * Mathf.Max(0.1f, _finisherRadiusMultiplier));

        var captured = Capture.GetCapturedEnemies();
        System.Collections.Generic.List<Transform> pinnedTargets = new System.Collections.Generic.List<Transform>(captured.Count);
        foreach (var enemy in captured)
        {
            if (enemy != null)
                pinnedTargets.Add(enemy.transform);
        }

        Movement.ExecuteOrbitFinisher(
            pivot,
            radius,
            Mathf.Max(0.01f, _finisherOrbitDuration),
            () =>
            {
                Combat.PerformSpinFinisher(Controller.transform.position, pinnedTargets);
                Capture.UnbindAll();
            },
            () =>
            {
                Controller.SetAttackingFlag(false);
                Controller.ChangeStateFromModule(WeaponState.Controlled);
            });

        return true;
    }
}
