using UnityEngine;

public class ThrustPierceModule : WeaponActionModule
{
    [SerializeField] private float _thrustFireCost = 18f;
    [SerializeField] private float _captureHoldCostPerSecond = 10f;
    [SerializeField] private ContinuousEnergySpendMode _captureHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;

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
        Controller.StartThrustPin(direction);
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
}
