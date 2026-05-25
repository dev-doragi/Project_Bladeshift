using System;
using UnityEngine;
using UnityEngine.Serialization;

public class WeaponModeController : MonoBehaviour
{
    [SerializeField] private WeaponMode _initialMode = WeaponMode.Remote;
    [SerializeField] private WeaponStateMachine _stateMachine;
    [FormerlySerializedAs("_meleeAttachment")]
    [SerializeField] private WeaponMeleeHoverFollow _meleeHoverFollow;

    public WeaponMode CurrentMode { get; private set; } = WeaponMode.Remote;
    public event Action<WeaponMode, WeaponMode> ModeChanged;

    private void Awake()
    {
        CurrentMode = _initialMode;
    }

    public void Initialize(WeaponStateMachine stateMachine)
    {
        _stateMachine = stateMachine != null ? stateMachine : GetComponent<WeaponStateMachine>();
        if (_meleeHoverFollow == null) _meleeHoverFollow = GetComponent<WeaponMeleeHoverFollow>();
        CurrentMode = _initialMode;
        ApplyCurrentMode();
    }

    public void ApplyCurrentMode()
    {
        if (CurrentMode == WeaponMode.Melee)
        {
            _stateMachine?.ChangeState(WeaponState.Grounded);
            _meleeHoverFollow?.EnableFollow();
            return;
        }

        _meleeHoverFollow?.DisableFollow();
        _stateMachine?.ChangeState(WeaponState.Grounded);
        _stateMachine?.ForceApplyCurrentState();
    }

    public void ToggleMode()
    {
        SetMode(CurrentMode == WeaponMode.Remote ? WeaponMode.Melee : WeaponMode.Remote);
    }

    public void SetMode(WeaponMode newMode)
    {
        if (CurrentMode == newMode) return;

        WeaponMode previousMode = CurrentMode;
        CurrentMode = newMode;

        ApplyCurrentMode();

        EventBus.Instance?.Publish(new WeaponModeChangeEvent
        {
            PreviousMode = previousMode,
            NewMode = CurrentMode
        });
        ModeChanged?.Invoke(previousMode, CurrentMode);
    }

}
