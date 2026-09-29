using UnityEngine;

public class SpinSlashModule : WeaponActionModule
{
    [SerializeField] private float _slashHoldCostPerSecond = 6f;
    [SerializeField] private ContinuousEnergySpendMode _slashHoldSpendMode = ContinuousEnergySpendMode.DepleteToZero;
    [SerializeField, Range(0f, 180f)] private float _droneRetargetAngle = 70f;
    private bool _isSlashing;
    private Vector2 _catchUpTarget;
    public bool IsSlashing => _isSlashing;

    public override void OnPress()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Controlled) return;
        if (_isSlashing) return;
        if (Controller.UseDroneRemoteControl && Controller.LinkEnergy != null &&
            !Controller.LinkEnergy.CanStartControl)
        {
            return;
        }

        _isSlashing = true;
        if (Controller.UseDroneRemoteControl)
        {
            _catchUpTarget = Controller.Sensor.GetReachableAimTargetPosition(Controller.WallAndEnvironmentLayer);
            Controller.Combat.ResetDroneAttackSegment(Controller.transform.position);
        }
        //Controller.Combat.ResetTickTimer();
        Controller.ChangeState(WeaponState.Slashing);
    }

    public override void OnTick()
    {
        if (Controller == null) return;
        if (Controller.CurrentMode != WeaponMode.Remote) return;
        if (Controller.CurrentState != WeaponState.Slashing || !_isSlashing) return;

        WeaponLinkEnergy energy = Controller.LinkEnergy;
        if (Controller.UseDroneRemoteControl)
        {
            if (energy != null &&
                !energy.SpendEnergyOverTime(_slashHoldCostPerSecond, ContinuousEnergySpendMode.DepleteToZero))
            {
                EndSpinSlash();
                return;
            }

            if (energy != null && energy.IsEmpty)
            {
                EndSpinSlash();
                return;
            }

            Vector2 weaponPosition = Controller.transform.position;
            Vector2 pointerTarget = Controller.Sensor.GetReachableAimTargetPosition(Controller.WallAndEnvironmentLayer);
            Vector2 targetDirection = _catchUpTarget - weaponPosition;
            Vector2 pointerDirection = pointerTarget - weaponPosition;
            float weaponRadius = Controller.Movement.WeaponRadius;
            bool isLargeTurn = targetDirection.magnitude > weaponRadius &&
                               pointerDirection.magnitude > weaponRadius &&
                               Vector2.Distance(pointerTarget, _catchUpTarget) > weaponRadius &&
                               Vector2.Angle(targetDirection, pointerDirection) >= _droneRetargetAngle;

            if (isLargeTurn)
            {
                _catchUpTarget = pointerTarget;
                Controller.Movement.StopFollow();
                Controller.Combat.ResetDroneAttackSegment(weaponPosition);
            }

            bool isBlocked = Controller.Movement.HandleHoverMovement(
                _catchUpTarget, true, Controller.WallAndEnvironmentLayer);

            bool reachedTarget = Vector2.Distance(Controller.transform.position, _catchUpTarget) <= weaponRadius;
            bool blockedWithNewPointer = isBlocked &&
                                         Vector2.Distance(pointerTarget, _catchUpTarget) > weaponRadius;
            if (reachedTarget || blockedWithNewPointer)
            {
                _catchUpTarget = pointerTarget;
                if (blockedWithNewPointer)
                    Controller.Movement.StopFollow();
                Controller.Combat.ResetDroneAttackSegment(Controller.transform.position);
            }
            return;
        }

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

