using System.Collections.Generic;
using UnityEngine;

public class WeaponActionRouter : MonoBehaviour, IWeaponActionHandler
{
    [SerializeField] private WeaponController _controller;
    [SerializeField] private WeaponModeController _modeController;
    [SerializeField] private WeaponActionModule _remotePrimaryModule;
    [SerializeField] private WeaponActionModule _remoteSecondaryModule;
    [SerializeField] private WeaponActionModule _meleePrimaryModule;
    [SerializeField] private WeaponActionModule _meleeSecondaryModule;
    private WeaponActionService _actionService;

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
        _actionService = new WeaponActionService(
            _controller,
            _modeController,
            _remotePrimaryModule,
            _remoteSecondaryModule,
            _meleePrimaryModule,
            _meleeSecondaryModule);
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<WeaponActionCommandEvent>(OnWeaponActionCommand);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<WeaponActionCommandEvent>(OnWeaponActionCommand);
    }

    private void FixedUpdate()
    {
        _actionService?.TickFixed();
    }

    private void Update()
    {
        _actionService?.TickFrame();
    }

    public WeaponActionAvailability GetAvailability(WeaponActionCommand command)
    {
        return _actionService != null
            ? _actionService.GetAvailability(command)
            : WeaponActionAvailability.Blocked(WeaponMode.Remote, WeaponState.Grounded, "Action service unavailable");
    }

    public bool TryStartPrimaryAction(WeaponActionCommand command)
    {
        return command.ActionType == WeaponActionInputType.Primary &&
               _actionService != null &&
               _actionService.TryHandleCommand(command);
    }

    public bool TryStartSecondaryAction(WeaponActionCommand command)
    {
        return command.ActionType == WeaponActionInputType.Secondary &&
               _actionService != null &&
               _actionService.TryHandleCommand(command);
    }

    private void OnWeaponActionCommand(WeaponActionCommandEvent evt)
    {
        if (evt.Command.ActionType == WeaponActionInputType.Primary)
            TryStartPrimaryAction(evt.Command);
        else
            TryStartSecondaryAction(evt.Command);
    }
}
