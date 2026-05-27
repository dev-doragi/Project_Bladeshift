using System.Collections.Generic;
using UnityEngine;

public class WeaponActionRouter : MonoBehaviour
{
    [SerializeField] private WeaponController _controller;
    [SerializeField] private WeaponModeController _modeController;
    [SerializeField] private WeaponActionModule _remotePrimaryModule;
    [SerializeField] private WeaponActionModule _remoteSecondaryModule;
    [SerializeField] private WeaponActionModule _meleePrimaryModule;
    [SerializeField] private WeaponActionModule _meleeSecondaryModule;
    private bool _blockNextPrimaryRelease;

    public void Initialize(WeaponController controller, WeaponModeController modeController)
    {
        _controller = controller != null ? controller : GetComponent<WeaponController>();
        _modeController = modeController != null ? modeController : GetComponent<WeaponModeController>();

        if (_remotePrimaryModule == null) _remotePrimaryModule = GetComponent<SpinSlashModule>();
        if (_remoteSecondaryModule == null) _remoteSecondaryModule = GetComponent<ThrustPierceModule>();

        if (_meleePrimaryModule == null)
        {
            foreach (WeaponActionModule module in GetComponents<WeaponActionModule>())
            {
                if (module == null) continue;
                if (module == _remotePrimaryModule || module == _remoteSecondaryModule) continue;
                _meleePrimaryModule = module;
                break;
            }
        }

        _remotePrimaryModule?.Initialize(_controller);
        _remoteSecondaryModule?.Initialize(_controller);
        _meleePrimaryModule?.Initialize(_controller);
        _meleeSecondaryModule?.Initialize(_controller);
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<PrimaryAttackEvent>(OnPrimaryAttack);
        EventBus.Instance?.Subscribe<SecondaryAttackEvent>(OnSecondaryAttack);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<PrimaryAttackEvent>(OnPrimaryAttack);
        EventBus.Instance?.Unsubscribe<SecondaryAttackEvent>(OnSecondaryAttack);
    }

    private void FixedUpdate()
    {
        foreach (WeaponActionModule module in EnumerateUniqueModules())
        {
            module.OnTick();
        }
    }

    private void Update()
    {
        foreach (WeaponActionModule module in EnumerateUniqueModules())
        {
            module.OnFrameTick();
        }
    }

    private void OnPrimaryAttack(PrimaryAttackEvent evt)
    {
        if (_controller != null && _controller.IsActionInputBlocked) return;

        WeaponActionModule primaryModule = GetPrimaryModule();
        WeaponActionModule secondaryModule = GetSecondaryModule();

        if (evt.IsStarted)
        {
            _blockNextPrimaryRelease = false;
            if (secondaryModule != null && secondaryModule.TryHandlePinnedPrimary()) return;
            if (secondaryModule != null && secondaryModule.BlocksPrimaryInput)
            {
                _blockNextPrimaryRelease = true;
                return;
            }

            primaryModule?.OnPress();
            return;
        }

        if (_blockNextPrimaryRelease)
        {
            _blockNextPrimaryRelease = false;
            return;
        }

        primaryModule?.OnRelease();
    }

    private void OnSecondaryAttack(SecondaryAttackEvent evt)
    {
        if (_controller != null && _controller.IsActionInputBlocked) return;

        WeaponActionModule secondaryModule = GetSecondaryModule();

        if (evt.IsStarted)
        {
            if (secondaryModule != null && secondaryModule.TryHandlePinnedSecondary()) return;
            secondaryModule?.OnPress();
        }
        else
        {
            secondaryModule?.OnRelease();
        }
    }

    private WeaponActionModule GetPrimaryModule()
    {
        if (_modeController != null && _modeController.CurrentMode == WeaponMode.Melee)
            return _meleePrimaryModule;

        return _remotePrimaryModule;
    }

    private WeaponActionModule GetSecondaryModule()
    {
        if (_modeController != null && _modeController.CurrentMode == WeaponMode.Melee)
            return _meleeSecondaryModule;

        return _remoteSecondaryModule;
    }

    private IEnumerable<WeaponActionModule> EnumerateUniqueModules()
    {
        HashSet<WeaponActionModule> modules = new HashSet<WeaponActionModule>();
        TryAdd(_remotePrimaryModule, modules);
        TryAdd(_remoteSecondaryModule, modules);
        TryAdd(_meleePrimaryModule, modules);
        TryAdd(_meleeSecondaryModule, modules);
        return modules;
    }

    private static void TryAdd(WeaponActionModule module, HashSet<WeaponActionModule> modules)
    {
        if (module != null) modules.Add(module);
    }
}
