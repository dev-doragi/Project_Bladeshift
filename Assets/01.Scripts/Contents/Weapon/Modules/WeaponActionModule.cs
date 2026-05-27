using UnityEngine;

public abstract class WeaponActionModule : MonoBehaviour
{
    protected WeaponController Controller;
    private WeaponActionInputContext? _activePrimaryInputContext;
    private WeaponActionInputContext? _activeSecondaryInputContext;
    public virtual bool BlocksPrimaryInput => false;

    public virtual void Initialize(WeaponController controller)
    {
        Controller = controller;
    }

    public virtual void OnPress() { }
    public virtual void OnFrameTick() { }
    public virtual void OnTick() { }
    public virtual void OnRelease() { }
    public virtual bool TryHandlePinnedPrimary() { return false; }
    public virtual bool TryHandlePinnedSecondary() { return false; }
    public virtual bool TryExecutePinnedFinisher() { return false; }

    public void SetActiveInputContext(WeaponActionInputContext context)
    {
        if (context.ActionType == WeaponActionInputType.Primary)
            _activePrimaryInputContext = context;
        else
            _activeSecondaryInputContext = context;
    }

    public void ClearActiveInputContext(WeaponActionInputType actionType)
    {
        if (actionType == WeaponActionInputType.Primary)
            _activePrimaryInputContext = null;
        else
            _activeSecondaryInputContext = null;
    }

    protected WeaponActionInputContext? GetActiveInputContext(WeaponActionInputType actionType)
    {
        return actionType == WeaponActionInputType.Primary
            ? _activePrimaryInputContext
            : _activeSecondaryInputContext;
    }

    protected bool IsActionFromGamepad(WeaponActionInputType actionType)
    {
        WeaponActionInputContext? context = GetActiveInputContext(actionType);
        return context.HasValue && context.Value.IsGamepad;
    }
}
