using UnityEngine;

public class SpinSlashModule : WeaponActionModule
{
    [SerializeField] private float _slashHoldCostPerSecond = 6f;
    [SerializeField] private ContinuousEnergySpendMode _slashHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;
    private bool _isSlashing;
    public bool IsSlashing => _isSlashing;

    public override void OnPress()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;
        if (_isSlashing) return;

        _isSlashing = true;
        //Controller.Combat.ResetTickTimer();
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
            Controller.Sensor.GetReachableAimTargetPosition(Controller.WallAndEnvironmentLayer),
            true,
            Controller.WallAndEnvironmentLayer);
        Controller.Movement.ApplySpinRotation(-Controller.Combat.SpinSpeed);
    }

    public override void OnRelease()
    {
        EndSpinSlash();
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

