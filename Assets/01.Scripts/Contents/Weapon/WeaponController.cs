using UnityEngine;
using System.Collections;
using System.Reflection;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
[RequireComponent(typeof(WeaponStateMachine), typeof(WeaponModeController), typeof(WeaponActionRouter))]
[RequireComponent(typeof(WeaponMeleeHoverFollow))]
[RequireComponent(typeof(WeaponMovement), typeof(WeaponCombat))]
[RequireComponent(typeof(WeaponSensor), typeof(WeaponView))]
[RequireComponent(typeof(WeaponCapture), typeof(WeaponLinkEnergy))]
[RequireComponent(typeof(WeaponEmbeddedAttack))]
[RequireComponent(typeof(WeaponAimCursor))]
public class WeaponController : MonoBehaviour
{
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

    private Rigidbody2D _rb;
    private Collider2D _weaponCollider;
    private WeaponMovement _movement;
    private WeaponCombat _combat;
    private WeaponSensor _sensor;
    private WeaponView _view;
    private WeaponCapture _capture;
    private WeaponEmbeddedAttack _embeddedAttack;
    private WeaponAimCursor _aimCursor;
    private WeaponLinkEnergy _linkEnergy;
    private WeaponStateMachine _stateMachine;
    private WeaponModeController _modeController;
    private WeaponActionRouter _actionRouter;
    private WeaponMeleeHoverFollow _meleeHoverFollow;
    private SpinSlashModule _spinSlashModule;
    private ThrustPierceModule _thrustPierceModule;
    private PlayerController _playerController;
    private Camera _mainCamera;
    private MethodInfo _handleLinkEnergyDepletedMethod;
    private bool _isModeSwitchInProgress;
    private bool _isMeleeRechargeWaiting;

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
    public float SlowMotionScale => _slowMotionScale;
    public float SlowMotionHoldDuration => _slowMotionHoldDuration;
    public float ThrustDragThreshold => _thrustDragThreshold;
    public float RecallEnergyCost => _recallEnergyCost;
    public LayerMask WallAndEnvironmentLayer => _wallAndEnvironmentLayer;
    public WeaponState CurrentState => _stateMachine != null ? _stateMachine.CurrentState : WeaponState.Grounded;
    public WeaponMode CurrentMode => _modeController != null ? _modeController.CurrentMode : WeaponMode.Remote;
    public bool IsOffline => _linkEnergy != null && _linkEnergy.IsOffline;
    public bool IsActionInputBlocked =>
        IsAutoReturnInProgress ||
        IsDockWaiting ||
        _isModeSwitchInProgress;

    private bool IsAutoReturnInProgress => _thrustPierceModule != null && _thrustPierceModule.IsAutoReturning;
    private bool IsDockWaiting => _thrustPierceModule != null && _thrustPierceModule.IsDockWaiting;
    private bool IsDepletionSequenceActive => _linkEnergy != null && (_linkEnergy.IsDepletedRechargeMode || _linkEnergy.IsOffline);

    private void Awake()
    {
        CacheComponents();
        if (!ResolvePlayerContext())
        {
            enabled = false;
            return;
        }

        InitializeSubsystems();
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
        _aimCursor = GetComponent<WeaponAimCursor>();
        if (_aimCursor == null)
            _aimCursor = gameObject.AddComponent<WeaponAimCursor>();
        _linkEnergy = GetComponent<WeaponLinkEnergy>();
        _stateMachine = GetComponent<WeaponStateMachine>();
        _modeController = GetComponent<WeaponModeController>();
        _actionRouter = GetComponent<WeaponActionRouter>();
        _meleeHoverFollow = GetComponent<WeaponMeleeHoverFollow>();
        _spinSlashModule = GetComponent<SpinSlashModule>();
        _thrustPierceModule = GetComponent<ThrustPierceModule>();
        if (_thrustPierceModule != null)
            _handleLinkEnergyDepletedMethod = _thrustPierceModule.GetType().GetMethod("HandleLinkEnergyDepleted", BindingFlags.Instance | BindingFlags.NonPublic);
        _mainCamera = Camera.main;
    }

    private bool ResolvePlayerContext()
    {
        if (_playerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                _playerTransform = playerObject.transform;
        }

        if (_playerTransform == null)
        {
            Debug.LogError("[WeaponController] Player object missing.");
            return false;
        }

        _playerController = _playerTransform.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("[WeaponController] PlayerController missing on player object.");
            return false;
        }

        return true;
    }

    private void InitializeSubsystems()
    {
        _fullRechargeDelayAfterReturn = Mathf.Max(1.5f, _fullRechargeDelayAfterReturn);
        _movement.CacheRigidbody(_rb);
        _stateMachine.Initialize(_rb, _weaponCollider);
        _meleeHoverFollow.Initialize(_playerController, _rb, _weaponCollider);
        _modeController.Initialize(_stateMachine);
        _sensor.Initialize(_playerTransform, _mainCamera, ControlRadius);
        _aimCursor?.Initialize(_playerTransform, _mainCamera, ControlRadius);
        _aimCursor?.SetFallbackGamepadStartPosition(transform.position);
        _sensor.SetAimCursor(_aimCursor);
        _view.Initialize(_sensor.GetPlayerTransform(), ControlRadius, _combat, _stateMachine, _modeController, _linkEnergy);
        _embeddedAttack.Initialize(this);
        _actionRouter.Initialize(this, _modeController);
        _stateMachine.ChangeState(WeaponState.Grounded);
        _modeController.ApplyCurrentMode();
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<WeaponModeToggleEvent>(OnWeaponModeToggle);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<WeaponModeToggleEvent>(OnWeaponModeToggle);
        _isModeSwitchInProgress = false;
        _isMeleeRechargeWaiting = false;
    }

    private void OnWeaponModeToggle(WeaponModeToggleEvent _)
    {
        if (!CanToggleWeaponMode()) return;

        if (_modeController != null && _modeController.CurrentMode == WeaponMode.Remote)
        {
            StartCoroutine(RemoteToMeleeSwitchRoutine());
            return;
        }

        if (_capture != null && _capture.HasCapturedTarget)
            _capture.ForceReleaseCapturedTarget();

        _modeController?.ToggleMode();
    }

    private bool CanToggleWeaponMode()
    {
        if (_modeController == null) return false;
        if (_isMeleeRechargeWaiting) return false;
        if (_isMeleeRechargeWaiting) return false;
        if (_isModeSwitchInProgress) return false;
        if (IsActionInputBlocked) return false;
        if (IsDepletionSequenceActive) return false;
        if (_playerTransform == null) return false;

        if (_spinSlashModule != null && _spinSlashModule.IsSlashing) return false;
        if (_thrustPierceModule != null)
        {
            if (_thrustPierceModule.IsAiming) return false;
            if (_thrustPierceModule.IsPinningFlightActive) return false;
        }

        WeaponState state = CurrentState;
        if (state == WeaponState.Slashing ||
            state == WeaponState.Thrusting ||
            state == WeaponState.PinningFlight ||
            state == WeaponState.Pinned ||
            state == WeaponState.Returning)
            return false;

        float controlRadius = ControlRadius;
        if (controlRadius <= 0f) return false;

        float distance = Vector2.Distance(_playerTransform.position, transform.position);
        if (distance > controlRadius) return false;

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

        _isMeleeRechargeWaiting = true;

        while (_modeController != null &&
               _modeController.CurrentMode == WeaponMode.Melee &&
               _linkEnergy != null &&
               !_linkEnergy.IsFull)
        {
            yield return null;
        }

        _isMeleeRechargeWaiting = false;
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
        if (_thrustPierceModule == null || _handleLinkEnergyDepletedMethod == null) return;
        _handleLinkEnergyDepletedMethod.Invoke(_thrustPierceModule, null);
    }

    private void OnDrawGizmos()
    {
        if (_view == null) _view = GetComponent<WeaponView>();
        _view?.DrawGizmos();
    }
}
