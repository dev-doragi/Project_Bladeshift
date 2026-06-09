using System.Collections.Generic;

public sealed class WeaponActionService
{
    private readonly WeaponController _controller;
    private readonly WeaponModeController _modeController;
    private readonly WeaponActionModule _remotePrimaryModule;
    private readonly WeaponActionModule _remoteSecondaryModule;
    private readonly WeaponActionModule _meleePrimaryModule;
    private readonly WeaponActionModule _meleeSecondaryModule;
    private bool _blockNextPrimaryRelease;

    public WeaponActionService(
        WeaponController controller,
        WeaponModeController modeController,
        WeaponActionModule remotePrimaryModule,
        WeaponActionModule remoteSecondaryModule,
        WeaponActionModule meleePrimaryModule,
        WeaponActionModule meleeSecondaryModule)
    {
        _controller = controller;
        _modeController = modeController;
        _remotePrimaryModule = remotePrimaryModule;
        _remoteSecondaryModule = remoteSecondaryModule;
        _meleePrimaryModule = meleePrimaryModule;
        _meleeSecondaryModule = meleeSecondaryModule;
    }

    public WeaponActionAvailability GetAvailability(WeaponActionCommand command)
    {
        if (_controller == null)
            return WeaponActionAvailability.Blocked(WeaponMode.Remote, WeaponState.Grounded, "Missing controller");

        if (_controller.IsActionInputBlocked)
            return WeaponActionAvailability.Blocked(_controller.CurrentMode, _controller.CurrentState, "Action input blocked");

        return WeaponActionAvailability.Available(_controller.CurrentMode, _controller.CurrentState);
    }

    public bool TryHandleCommand(WeaponActionCommand command)
    {
        WeaponActionAvailability availability = GetAvailability(command);
        if (!availability.IsAvailable)
            return false;

        if (command.ActionType == WeaponActionInputType.Primary)
        {
            HandlePrimary(command);
            return true;
        }

        HandleSecondary(command);
        return true;
    }

    public void TickFrame()
    {
        foreach (WeaponActionModule module in EnumerateUniqueModules())
            module.OnFrameTick();
    }

    public void TickFixed()
    {
        foreach (WeaponActionModule module in EnumerateUniqueModules())
            module.OnTick();
    }

    private void HandlePrimary(WeaponActionCommand command)
    {
        WeaponActionModule primaryModule = GetPrimaryModule();
        WeaponActionModule secondaryModule = GetSecondaryModule();

        if (command.IsStarted)
        {
            WeaponActionInputContext inputContext = command.ToInputContext();
            primaryModule?.SetActiveInputContext(inputContext);
            _blockNextPrimaryRelease = false;
            if (secondaryModule != null && secondaryModule.TryHandlePinnedPrimary())
                return;
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
            primaryModule?.ClearActiveInputContext(WeaponActionInputType.Primary);
            return;
        }

        primaryModule?.OnRelease();
        primaryModule?.ClearActiveInputContext(WeaponActionInputType.Primary);
    }

    private void HandleSecondary(WeaponActionCommand command)
    {
        WeaponActionModule secondaryModule = GetSecondaryModule();
        if (command.IsStarted)
        {
            WeaponActionInputContext inputContext = command.ToInputContext();
            secondaryModule?.SetActiveInputContext(inputContext);
            if (secondaryModule != null && secondaryModule.TryHandlePinnedSecondary())
                return;
            secondaryModule?.OnPress();
            return;
        }

        secondaryModule?.OnRelease();
        secondaryModule?.ClearActiveInputContext(WeaponActionInputType.Secondary);
    }

    private WeaponActionModule GetPrimaryModule()
    {
        return _modeController != null && _modeController.CurrentMode == WeaponMode.Melee
            ? _meleePrimaryModule
            : _remotePrimaryModule;
    }

    private WeaponActionModule GetSecondaryModule()
    {
        return _modeController != null && _modeController.CurrentMode == WeaponMode.Melee
            ? _meleeSecondaryModule
            : _remoteSecondaryModule;
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
        if (module != null)
            modules.Add(module);
    }
}
