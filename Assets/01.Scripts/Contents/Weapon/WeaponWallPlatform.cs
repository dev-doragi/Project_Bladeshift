using UnityEngine;

[DisallowMultipleComponent]
public class WeaponWallPlatform : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponStateMachine _stateMachine;
    [SerializeField] private GameObject _platformObject;

    private void Awake()
    {
        if (_stateMachine == null)
            _stateMachine = GetComponent<WeaponStateMachine>();

        SetPlatformActive(false);
    }

    private void OnEnable()
    {
        if (_stateMachine != null)
            _stateMachine.StateChanged += OnWeaponStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (_stateMachine != null)
            _stateMachine.StateChanged -= OnWeaponStateChanged;

        SetPlatformActive(false);
    }

    private void OnWeaponStateChanged(WeaponState previousState, WeaponState newState)
    {
        Refresh();
    }

    private void Refresh()
    {
        bool active = _stateMachine != null && _stateMachine.IsPinnedToWall;
        SetPlatformActive(active);
    }

    private void SetPlatformActive(bool active)
    {
        if (_platformObject != null)
            _platformObject.SetActive(active);
    }
}