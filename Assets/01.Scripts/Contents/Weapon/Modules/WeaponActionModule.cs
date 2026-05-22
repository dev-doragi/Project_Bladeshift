using UnityEngine;

public abstract class WeaponActionModule : MonoBehaviour
{
    protected WeaponController Controller;
    protected WeaponLinkEnergy LinkEnergy;
    protected WeaponMovement Movement;
    protected WeaponCombat Combat;
    protected WeaponSensor Sensor;
    protected WeaponCapture Capture;
    protected WeaponView View;

    public virtual void Initialize(
        WeaponController controller,
        WeaponLinkEnergy linkEnergy,
        WeaponMovement movement,
        WeaponCombat combat,
        WeaponSensor sensor,
        WeaponCapture capture,
        WeaponView view)
    {
        Controller = controller;
        LinkEnergy = linkEnergy;
        Movement = movement;
        Combat = combat;
        Sensor = sensor;
        Capture = capture;
        View = view;
    }

    public virtual void OnPress() { }
    public virtual void OnTick() { }
    public virtual void OnRelease() { }
    public virtual bool TryHandlePinnedPrimary() { return false; }
    public virtual bool TryHandlePinnedSecondary() { return false; }
}
