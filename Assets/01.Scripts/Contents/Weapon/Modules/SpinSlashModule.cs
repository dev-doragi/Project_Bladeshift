using UnityEngine;

public class SpinSlashModule : WeaponActionModule
{
    [SerializeField] private float _slashHoldCostPerSecond = 6f;
    [SerializeField] private ContinuousEnergySpendMode _slashHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;

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
}
