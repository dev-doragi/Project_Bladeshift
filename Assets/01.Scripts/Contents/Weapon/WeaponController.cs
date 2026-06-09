using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[RequireComponent(typeof(WeaponStateMachine), typeof(WeaponModeController), typeof(WeaponActionRouter))]
[RequireComponent(typeof(WeaponMeleeHoverFollow))]
[RequireComponent(typeof(WeaponMovement), typeof(WeaponCombat))]
[RequireComponent(typeof(WeaponSensor), typeof(WeaponView))]
[RequireComponent(typeof(WeaponCapture), typeof(WeaponLinkEnergy))]
[RequireComponent(typeof(WeaponEmbeddedAttack))]
public class WeaponController : MonoBehaviour
{
    [System.Flags]
    private enum AimCursorBlockReason
    {
        None = 0,
        NonRemoteMode = 1 << 0,
        UnsupportedWeaponState = 1 << 1,
        ModeSwitchInProgress = 1 << 2,
        AutoReturnInProgress = 1 << 3,
        DockWaiting = 1 << 4,
        DepletionSequence = 1 << 5,
        ThrustAiming = 1 << 6,
        PinningFlight = 1 << 7,
        SpinSlashing = 1 << 8
    }

    [Header("1. Core References")]
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Transform _droneDockPivot;

    [Header("2. Combat/Control Tuning")]
    [SerializeField] private float _slowMotionScale = 0.2f;
    [SerializeField] private float _slowMotionHoldDuration = 999f;
    [SerializeField] private LayerMask _wallAndEnvironmentLayer;
    [SerializeField] private float _thrustDragThreshold = 2.0f;
    [SerializeField] private float _recallEnergyCost = 0f;
    [SerializeField] private float _fullRechargeDelayAfterReturn = 1.5f;
    [SerializeField] private string _platformLayerName = "Platform";

    private Rigidbody2D _rb;
    private Collider2D _weaponCollider;
    private WeaponMovement _movement;
    private WeaponCombat _combat;
    private WeaponSensor _sensor;
    private WeaponView _view;
    private WeaponCapture _capture;
    private WeaponEmbeddedAttack _embeddedAttack;
    [SerializeField] private WeaponAimCursor _aimCursor;
    private WeaponLinkEnergy _linkEnergy;
    private WeaponStateMachine _stateMachine;
    private WeaponModeController _modeController;
    private WeaponActionRouter _actionRouter;
    private WeaponMeleeHoverFollow _meleeHoverFollow;
    private SpinSlashModule _spinSlashModule;
    private ThrustPierceModule _thrustPierceModule;
    private PlayerController _playerController;
    [SerializeField] private MouseWorldProxyFollower _mouseWorldProxyFollower;
    private Camera _mainCamera;
    private bool _isModeSwitchInProgress;
    private Coroutine _meleeModeRechargeRoutine;
    private int _platformLayer = -1;
    private int _weaponLayer = -1;
    private WeaponAimPresentation _aimPresentation;
    private WeaponModeService _modeService;
    private bool _isInitialized;

    public Rigidbody2D Rigidbody => _rb;
    public Collider2D WeaponCollider => _weaponCollider;
    public WeaponMovement Movement => _movement;
    public WeaponCombat Combat => _combat;
    public WeaponSensor Sensor => _sensor;
    public WeaponView View => _view;
    public WeaponCapture Capture => _capture;
    public WeaponEmbeddedAttack EmbeddedAttack => _embeddedAttack;
    public WeaponAimCursor AimCursor => _aimCursor;
    public WeaponLinkEnergy LinkEnergy => _linkEnergy;
    public WeaponStateMachine StateMachine => _stateMachine;
    public WeaponModeController ModeController => _modeController;
    public WeaponActionRouter ActionRouter => _actionRouter;
    public WeaponMeleeHoverFollow MeleeHoverFollow => _meleeHoverFollow;
    public Transform PlayerTransform => _playerTransform;
    public Transform DroneDockPivot => _droneDockPivot;
    public float ControlRadius => _playerController != null ? _playerController.ControlRadius : 0f;
    public Vector2 PlayerAimDirection
    {
        get
        {
            if (_playerController != null && _playerController.AimDirection.sqrMagnitude > 0.0001f)
                return _playerController.AimDirection.normalized;

            return Vector2.right;
        }
    }
    public float SlowMotionScale => _slowMotionScale;
    public float SlowMotionHoldDuration => _slowMotionHoldDuration;
    public float ThrustDragThreshold => _thrustDragThreshold;
    public float RecallEnergyCost => _recallEnergyCost;
    public LayerMask WallAndEnvironmentLayerForPin => _wallAndEnvironmentLayer;
    public LayerMask WallAndEnvironmentLayer
    {
        get
        {
            int maskValue = _wallAndEnvironmentLayer.value;
            if (CurrentMode == WeaponMode.Remote && _platformLayer >= 0 && _platformLayer <= 31)
                maskValue &= ~(1 << _platformLayer);
            return maskValue;
        }
    }
    public WeaponState CurrentState => _stateMachine != null ? _stateMachine.CurrentState : WeaponState.Grounded;
    public WeaponMode CurrentMode => _modeController != null ? _modeController.CurrentMode : WeaponMode.Remote;
    public bool IsOffline => _linkEnergy != null && _linkEnergy.IsOffline;
    public bool IsActionInputBlocked =>
        IsAutoReturnInProgress ||
        (CurrentMode == WeaponMode.Remote && IsDockWaiting) ||
        _isModeSwitchInProgress;

    private bool IsAutoReturnInProgress => _thrustPierceModule != null && _thrustPierceModule.IsAutoReturning;
    private bool IsDockWaiting => _thrustPierceModule != null && _thrustPierceModule.IsDockWaiting;
    private bool IsDepletionSequenceActive => _linkEnergy != null && (_linkEnergy.IsDepletedRechargeMode || _linkEnergy.IsOffline);

    private void Awake()
    {
        CacheComponents();
        CreateServices();
        TryBindSerializedPlayerContext();
        TryBindExistingPlayerContext();
        TryInitializeSubsystems();
    }

    private void CacheComponents()
    {
        _rb = GetComponent<Rigidbody2D>();
        _weaponCollider = GetComponent<Collider2D>();
        _movement = GetComponent<WeaponMovement>();
        _combat = GetComponent<WeaponCombat>();
        _sensor = GetComponent<WeaponSensor>();
        _view = GetComponent<WeaponView>();
        _capture = GetComponent<WeaponCapture>();
        _embeddedAttack = GetComponent<WeaponEmbeddedAttack>();
        if (_embeddedAttack == null)
            _embeddedAttack = gameObject.AddComponent<WeaponEmbeddedAttack>();
        if (_aimCursor == null)
            _aimCursor = GetComponent<WeaponAimCursor>();
        if (_aimCursor == null)
            _aimCursor = GetComponentInChildren<WeaponAimCursor>(true);
        _linkEnergy = GetComponent<WeaponLinkEnergy>();
        _stateMachine = GetComponent<WeaponStateMachine>();
        _modeController = GetComponent<WeaponModeController>();
        _actionRouter = GetComponent<WeaponActionRouter>();
        _meleeHoverFollow = GetComponent<WeaponMeleeHoverFollow>();
        _spinSlashModule = GetComponent<SpinSlashModule>();
        _thrustPierceModule = GetComponent<ThrustPierceModule>();
        _mainCamera = Camera.main;
        _weaponLayer = gameObject.layer;
        _platformLayer = LayerMask.NameToLayer(_platformLayerName);
        if (_mouseWorldProxyFollower == null)
            _mouseWorldProxyFollower = GetComponentInChildren<MouseWorldProxyFollower>(true);
    }

    private void CreateServices()
    {
        _aimPresentation = new WeaponAimPresentation();
        _modeService = new WeaponModeService(this);
    }

    private void TryBindSerializedPlayerContext()
    {
        if (_playerTransform == null)
            return;

        BindPlayer(_playerTransform.GetComponent<PlayerController>());
    }

    private void TryBindExistingPlayerContext()
    {
        if (_playerController != null)
            return;

        PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
        if (foundPlayer != null)
            BindPlayer(foundPlayer);
    }

    private void BindPlayer(PlayerController playerController)
    {
        if (playerController == null)
            return;

        if (_playerController != null && _playerController != playerController)
            _isInitialized = false;

        _playerController = playerController;
        _playerTransform = playerController.transform;
    }

    private bool TryInitializeSubsystems()
    {
        if (_isInitialized)
            return true;
        if (_playerController == null || _playerTransform == null)
            return false;

        _fullRechargeDelayAfterReturn = Mathf.Max(1.5f, _fullRechargeDelayAfterReturn);
        _movement.CacheRigidbody(_rb);
        _stateMachine.Initialize(_rb, _weaponCollider);
        _meleeHoverFollow.Initialize(_playerController, _rb, _weaponCollider);
        _modeController.Initialize(_stateMachine);
        _sensor.Initialize(_playerTransform, _mainCamera, ControlRadius);
        _aimCursor?.Initialize(_playerTransform, _mainCamera, ControlRadius);
        _aimCursor?.SetWallMask(_wallAndEnvironmentLayer);
        _aimCursor?.SetFallbackGamepadStartPosition(transform.position);
        _sensor.SetAimCursor(_aimCursor);
        _mouseWorldProxyFollower?.SetAimCursor(_aimCursor);
        _mouseWorldProxyFollower?.SetPlayerTransform(_playerTransform);
        _playerController?.SetAimProvider(_aimCursor, _sensor);
        _playerController?.SetWeaponModeController(_modeController);
        _view.Initialize(_sensor.GetPlayerTransform(), ControlRadius, _combat, _stateMachine, _modeController, _linkEnergy);
        _embeddedAttack.Initialize(this);
        _actionRouter.Initialize(this, _modeController);
        _aimPresentation.Initialize(_aimCursor, _mouseWorldProxyFollower, () => CurrentMode, EvaluateAimCursorVisibleFromState);
        _stateMachine.ChangeState(WeaponState.Grounded);
        _modeController.ApplyCurrentMode();
        _aimPresentation.ApplyModePolicy(resetCursorPosition: true);
        ApplyWeaponPlatformCollisionPolicy(_modeController.CurrentMode);
        _aimPresentation.RefreshVisibility();
        _isInitialized = true;
        return true;
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
        EventBus.Instance?.Subscribe<WeaponModeToggleEvent>(OnWeaponModeToggle);
        if (_modeController != null)
            _modeController.ModeChanged += OnWeaponModeChanged;
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
        EventBus.Instance?.Unsubscribe<WeaponModeToggleEvent>(OnWeaponModeToggle);
        if (_modeController != null)
            _modeController.ModeChanged -= OnWeaponModeChanged;
        ApplyWeaponPlatformCollisionPolicy(WeaponMode.Melee);
        _isModeSwitchInProgress = false;
        if (_meleeModeRechargeRoutine != null)
        {
            StopCoroutine(_meleeModeRechargeRoutine);
            _meleeModeRechargeRoutine = null;
        }
    }

    private void Update()
    {
        if (!_isInitialized && !TryInitializeSubsystems())
            return;

        _aimCursor?.Tick();
        _aimPresentation?.RefreshVisibility();
    }

    private void OnPlayerSpawned(PlayerSpawnedEvent evt)
    {
        if (evt.Player == null)
            return;

        BindPlayer(evt.Player);
        TryInitializeSubsystems();
    }

    private void OnWeaponModeToggle(WeaponModeToggleEvent _)
    {
        TryToggleMode();
    }

    public bool TryToggleMode()
    {
        if (!CanToggleWeaponMode())
            return false;

        if (_modeController != null && _modeController.CurrentMode == WeaponMode.Remote)
        {
            StartCoroutine(RemoteToMeleeSwitchRoutine());
            return true;
        }

        if (_capture != null && _capture.HasCapturedTarget)
            _capture.ForceReleaseCapturedTarget();

        _modeController?.ToggleMode();
        return true;
    }

    public PlayerAimState GetAimState()
    {
        return _playerController != null ? _playerController.AimState : PlayerAimState.Default;
    }

    public bool TryStartPrimaryAction(WeaponActionCommand command)
    {
        return _actionRouter != null && _actionRouter.TryStartPrimaryAction(command);
    }

    public bool TryStartSecondaryAction(WeaponActionCommand command)
    {
        return _actionRouter != null && _actionRouter.TryStartSecondaryAction(command);
    }

    private void OnWeaponModeChanged(WeaponMode previousMode, WeaponMode newMode)
    {
        if (newMode != WeaponMode.Melee)
        {
            if (_meleeModeRechargeRoutine != null)
            {
                StopCoroutine(_meleeModeRechargeRoutine);
                _meleeModeRechargeRoutine = null;
            }
        }

        bool shouldReset = previousMode != newMode;
        _aimPresentation?.ApplyModePolicy(shouldReset);
        ApplyWeaponPlatformCollisionPolicy(newMode);
        _aimPresentation?.RefreshVisibility();
    }

    private bool EvaluateAimCursorVisibleFromState()
    {
        return GetAimCursorBlockReasons() == AimCursorBlockReason.None;
    }

    private AimCursorBlockReason GetAimCursorBlockReasons()
    {
        AimCursorBlockReason reasons = AimCursorBlockReason.None;

        if (_modeController == null || _modeController.CurrentMode != WeaponMode.Remote)
            reasons |= AimCursorBlockReason.NonRemoteMode;

        if (!IsAimCursorSupportedWeaponState(CurrentState))
            reasons |= AimCursorBlockReason.UnsupportedWeaponState;

        if (_isModeSwitchInProgress)
            reasons |= AimCursorBlockReason.ModeSwitchInProgress;

        if (IsAutoReturnInProgress)
            reasons |= AimCursorBlockReason.AutoReturnInProgress;

        if (IsDockWaiting)
            reasons |= AimCursorBlockReason.DockWaiting;

        if (IsDepletionSequenceActive)
            reasons |= AimCursorBlockReason.DepletionSequence;

        if (_thrustPierceModule != null && _thrustPierceModule.IsAiming)
            reasons |= AimCursorBlockReason.ThrustAiming;

        if (_thrustPierceModule != null && _thrustPierceModule.IsPinningFlightActive)
            reasons |= AimCursorBlockReason.PinningFlight;

        if (_spinSlashModule != null && _spinSlashModule.IsSlashing)
            reasons |= AimCursorBlockReason.SpinSlashing;

        return reasons;
    }

    private bool IsAimCursorSupportedWeaponState(WeaponState state)
    {
        return state == WeaponState.Grounded || state == WeaponState.Controlled;
    }

    private void ApplyWeaponPlatformCollisionPolicy(WeaponMode mode)
    {
        if (_weaponLayer < 0 || _weaponLayer > 31)
            return;
        if (_platformLayer < 0 || _platformLayer > 31)
            return;

        bool ignoreInRemoteMode = mode == WeaponMode.Remote;
        CollisionPolicyService.SetIgnoreLayerCollision(_weaponLayer, _platformLayer, ignoreInRemoteMode);
    }

    private bool CanToggleWeaponMode()
    {
        if (!_isInitialized && !TryInitializeSubsystems()) return false;
        if (_modeService == null || !_modeService.CanToggleMode()) return false;
        if (_isModeSwitchInProgress) return false;
        if (IsActionInputBlocked) return false;
        if (_playerTransform == null) return false;

        if (_spinSlashModule != null && _spinSlashModule.IsSlashing) return false;
        if (_thrustPierceModule != null)
        {
            if (_thrustPierceModule.IsAiming) return false;
            if (_thrustPierceModule.IsPinningFlightActive) return false;
        }

        return true;
    }

    private IEnumerator RemoteToMeleeSwitchRoutine()
    {
        if (_isModeSwitchInProgress)
            yield break;

        _isModeSwitchInProgress = true;

        if (_capture != null && _capture.HasCapturedTarget)
            _capture.ForceReleaseCapturedTarget();

        _stateMachine?.ClearPinSource();

        if (_movement == null)
        {
            _isModeSwitchInProgress = false;
            yield break;
        }

        bool returnCompleted = false;
        bool returnSucceeded = false;

        ChangeState(WeaponState.Returning);

        _movement.ExecuteReturn(
            GetModeSwitchReturnTargetPosition,
            ControlRadius,
            (currentPos, targetPos) => Vector2.Distance(currentPos, targetPos) <= 0.25f,
            success =>
            {
                if (returnCompleted)
                    return;

                returnCompleted = true;
                returnSucceeded = success;
            });

        while (!returnCompleted)
            yield return null;

        if (!returnSucceeded)
        {
            _isModeSwitchInProgress = false;
            yield break;
        }

        _modeController?.SetMode(WeaponMode.Melee);
        _meleeHoverFollow?.EnableFollow();

        _isModeSwitchInProgress = false;

        if (_linkEnergy == null || _linkEnergy.IsFull)
            yield break;

        if (_modeController == null || _modeController.CurrentMode != WeaponMode.Melee)
            yield break;

        BeginMeleeModeRecharge();
    }

    private void BeginMeleeModeRecharge()
    {
        if (_linkEnergy == null)
            return;
        if (_modeController == null || _modeController.CurrentMode != WeaponMode.Melee)
            return;

        if (_meleeModeRechargeRoutine != null)
        {
            StopCoroutine(_meleeModeRechargeRoutine);
            _meleeModeRechargeRoutine = null;
        }

        _meleeModeRechargeRoutine = StartCoroutine(MeleeModeRechargeRoutine());
    }

    private IEnumerator MeleeModeRechargeRoutine()
    {
        if (_modeController == null || _modeController.CurrentMode != WeaponMode.Melee)
        {
            _meleeModeRechargeRoutine = null;
            yield break;
        }

        float rechargeDuration = Mathf.Max(0.01f, _fullRechargeDelayAfterReturn);
        yield return StartCoroutine(_linkEnergy.RechargeToFullAndUnlockOverDuration(rechargeDuration));

        if (_modeController == null || _modeController.CurrentMode != WeaponMode.Melee)
        {
            _meleeModeRechargeRoutine = null;
            yield break;
        }

        _meleeModeRechargeRoutine = null;
    }

    private Vector2 GetModeSwitchReturnTargetPosition()
    {
        if (_meleeHoverFollow != null && _meleeHoverFollow.MeleeHoverHoldPoint != null)
            return _meleeHoverFollow.MeleeHoverHoldPoint.position;

        Transform dock = _droneDockPivot != null ? _droneDockPivot : _playerTransform;
        if (dock != null)
            return dock.position;

        return _rb != null ? _rb.position : (Vector2)transform.position;
    }

    public void ChangeState(WeaponState newState)
    {
        _stateMachine?.ChangeState(newState);
    }

    public void PublishSlowMotion()
    {
        EventBus.Instance?.Publish(new SlowMotionEvent
        {
            TargetTimeScale = _slowMotionScale,
            Duration = _slowMotionHoldDuration
        });
    }

    public void ResetTimeScale()
    {
        TimeManager.Instance?.ResetTime();
    }

    public void TryEnterExistingRechargePathFromOffline()
    {
        if (_linkEnergy == null || !_linkEnergy.IsOffline) return;
        if (_thrustPierceModule == null) return;

        // Melee mode should recharge energy only, without forcing dock-return flow.
        if (_modeController != null && _modeController.CurrentMode == WeaponMode.Melee)
        {
            BeginMeleeModeRecharge();
            return;
        }

        _thrustPierceModule.HandleLinkEnergyDepleted();
    }

    public void SetAimCursorVisible(bool isVisible)
    {
        _aimCursor?.SetCursorVisible(isVisible);
    }

    private void OnDrawGizmos()
    {
        if (_view == null) _view = GetComponent<WeaponView>();
        _view?.DrawGizmos();
    }
}
