using UnityEngine;

public abstract class WeaponActionModule : MonoBehaviour
{
    protected WeaponController Controller;
    public virtual bool BlocksPrimaryInput => false;

    public virtual void Initialize(WeaponController controller)
    {
        Controller = controller;
    }

    public virtual void OnPress() { }
    public virtual void OnTick() { }
    public virtual void OnRelease() { }
    public virtual bool TryHandlePinnedPrimary() { return false; }
    public virtual bool TryHandlePinnedSecondary() { return false; }
    public virtual bool TryExecutePinnedFinisher() { return false; }
}
