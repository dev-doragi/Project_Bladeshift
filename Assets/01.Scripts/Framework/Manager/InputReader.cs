using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-140)]
[RequireComponent(typeof(PlayerInput))]
/// <summary>
/// [BladeShift PoC] Input System 액션을 읽어 공통 이벤트로 발행하는 매니저입니다.
/// </summary>
public class InputReader : Singleton<InputReader>
{
    [Header("Action Map Names")]
    [SerializeField] private string _playerActionMapName = "Player";
    [SerializeField] private string _systemActionMapName = "System";

    [Header("Player Actions")]
    [SerializeField] private string _moveActionName = "Move";
    [SerializeField] private string _jumpActionName = "Jump";
    [SerializeField] private string _dashActionName = "Dash";
    [SerializeField] private string _primaryAttackActionName = "PrimaryAttack";     // 좌클릭/기본공격
    [SerializeField] private string _secondaryAttackActionName = "SecondaryAttack"; // 우클릭/특수공격
    [SerializeField] private string _toggleWeaponModeActionName = "ToggleWeaponMode";
    [SerializeField] private string _lookActionName = "Look";
    [SerializeField] private string _pointActionName = "Point";
    [SerializeField] private string _scrollActionName = "Scroll";
    [SerializeField] private string _rotateActionName = "Rotate";

    [Header("System Actions")]
    [SerializeField] private string _pauseActionName = "Pause";

    [Header("Behavior")]
    [SerializeField] private bool _useGameStateInputGate = true;

    [Header("Aim")]
    [SerializeField] private float _gamepadAimDeadzone = 0.2f;

    private PlayerInput _playerInput;
    private InputActionMap _playerMap;
    private InputActionMap _systemMap;

    // Actions
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _dashAction;
    private InputAction _primaryAttackAction;
    private InputAction _secondaryAttackAction;
    private InputAction _toggleWeaponModeAction;
    private InputAction _lookAction;
    private InputAction _pointAction;
    private InputAction _scrollAction;
    private InputAction _rotateAction;
    private InputAction _pauseAction;

    public bool IsPointerOverUI { get; private set; }
    private bool _isInputBlocked = false;
    private bool _isSecondaryAttackStartedFromGamepad;
    public bool IsInputBlocked => _isInputBlocked;

    protected override void OnBootstrap()
    {
        _playerInput = GetComponent<PlayerInput>();

        if (_playerInput == null || _playerInput.actions == null)
        {
            Debug.LogError("[InputReader] PlayerInput 또는 InputActionAsset이 설정되지 않았습니다.");
            return;
        }

        // 맵 찾기
        _playerMap = _playerInput.actions.FindActionMap(_playerActionMapName, false);
        _systemMap = _playerInput.actions.FindActionMap(_systemActionMapName, false);

        // 액션 바인딩
        if (_playerMap != null)
        {
            _moveAction = _playerMap.FindAction(_moveActionName, false);
            _jumpAction = _playerMap?.FindAction(_jumpActionName, false);
            _dashAction = _playerMap.FindAction(_dashActionName, false);
            _primaryAttackAction = _playerMap.FindAction(_primaryAttackActionName, false);
            _secondaryAttackAction = _playerMap.FindAction(_secondaryAttackActionName, false);
            _toggleWeaponModeAction = _playerMap.FindAction(_toggleWeaponModeActionName, false);
            _lookAction = _playerMap.FindAction(_lookActionName, false);
            _pointAction = _playerMap.FindAction(_pointActionName, false);
            _scrollAction = _playerMap.FindAction(_scrollActionName, false);
            _rotateAction = _playerMap.FindAction(_rotateActionName, false);
        }

        if (_systemMap != null)
        {
            _pauseAction = _systemMap.FindAction(_pauseActionName, false);
        }

        BindEvents();

        _playerMap?.Enable();
        _systemMap?.Enable();

        if (EventBus.Instance != null)
        {
            if (_useGameStateInputGate)
                EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Subscribe<PlayerDeathStartedEvent>(OnPlayerDeathStarted);
        }
    }

    private void OnDisable()
    {
        UnbindEvents();

        if (EventBus.Instance != null)
        {
            if (_useGameStateInputGate)
                EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Instance.Unsubscribe<PlayerDeathStartedEvent>(OnPlayerDeathStarted);
        }

        _playerMap?.Disable();
        _systemMap?.Disable();
    }

    private void Update()
    {
        if (EventSystem.current != null)
        {
            IsPointerOverUI = EventSystem.current.IsPointerOverGameObject();
        }
    }

    private void BindEvents()
    {
        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
        }

        if (_jumpAction != null)
        {
            _jumpAction.started += OnJumpStarted;
            _jumpAction.canceled += OnJumpCanceled;
        }

        if (_dashAction != null)
        {
            _dashAction.started += OnDashStarted;
        }

        if (_primaryAttackAction != null)
        {
            _primaryAttackAction.started += OnPrimaryAttackStarted;
            _primaryAttackAction.canceled += OnPrimaryAttackCanceled;
        }

        if (_secondaryAttackAction != null)
        {
            _secondaryAttackAction.started += OnSecondaryAttackStarted;
            _secondaryAttackAction.canceled += OnSecondaryAttackCanceled;
        }

        if (_toggleWeaponModeAction != null) _toggleWeaponModeAction.performed += OnToggleWeaponModePerformed;
        if (_rotateAction != null) _rotateAction.performed += OnRotatePerformed;
        if (_scrollAction != null) _scrollAction.performed += OnScrollPerformed;
        if (_pauseAction != null) _pauseAction.performed += OnPausePerformed;
    }

    private void UnbindEvents()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
        }

        if (_jumpAction != null)
        {
            _jumpAction.started -= OnJumpStarted;
            _jumpAction.canceled -= OnJumpCanceled;
        }

        if (_dashAction != null)
        {
            _dashAction.started -= OnDashStarted;
        }

        if (_primaryAttackAction != null)
        {
            _primaryAttackAction.started -= OnPrimaryAttackStarted;
            _primaryAttackAction.canceled -= OnPrimaryAttackCanceled;
        }

        if (_secondaryAttackAction != null)
        {
            _secondaryAttackAction.started -= OnSecondaryAttackStarted;
            _secondaryAttackAction.canceled -= OnSecondaryAttackCanceled;
        }

        if (_toggleWeaponModeAction != null) _toggleWeaponModeAction.performed -= OnToggleWeaponModePerformed;
        if (_rotateAction != null) _rotateAction.performed -= OnRotatePerformed;
        if (_scrollAction != null) _scrollAction.performed -= OnScrollPerformed;
        if (_pauseAction != null) _pauseAction.performed -= OnPausePerformed;
    }

    #region Callbacks

    private void OnMovePerformed(InputAction.CallbackContext ctx) => PublishIfAllowed(new MoveInputEvent { Direction = ctx.ReadValue<Vector2>() });
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => PublishIfAllowed(new MoveInputEvent { Direction = Vector2.zero });

    private void OnJumpStarted(InputAction.CallbackContext _) => PublishIfAllowed(new JumpInputEvent { IsStarted = true });
    private void OnJumpCanceled(InputAction.CallbackContext _) => PublishIfAllowed(new JumpInputEvent { IsStarted = false });
    private void OnDashStarted(InputAction.CallbackContext _) => PublishIfAllowed(new DashInputEvent { IsStarted = true });

    private void OnPrimaryAttackStarted(InputAction.CallbackContext _) => PublishIfAllowed(new PrimaryAttackEvent { IsStarted = true });
    private void OnPrimaryAttackCanceled(InputAction.CallbackContext _) => PublishIfAllowed(new PrimaryAttackEvent { IsStarted = false });

    private void OnSecondaryAttackStarted(InputAction.CallbackContext ctx)
    {
        _isSecondaryAttackStartedFromGamepad = ctx.control != null && ctx.control.device is Gamepad;
        PublishIfAllowed(new SecondaryAttackEvent { IsStarted = true });
    }

    private void OnSecondaryAttackCanceled(InputAction.CallbackContext _)
    {
        PublishIfAllowed(new SecondaryAttackEvent { IsStarted = false });
    }

    private void OnRotatePerformed(InputAction.CallbackContext _) => PublishIfAllowed(new RotateEvent());

    private void OnToggleWeaponModePerformed(InputAction.CallbackContext _) => PublishIfAllowed(new WeaponModeToggleEvent());

    private void OnScrollPerformed(InputAction.CallbackContext ctx)
    {
        float scrollValue = ctx.ReadValue<Vector2>().y;
        if (Mathf.Abs(scrollValue) > 0.01f)
            PublishIfAllowed(new ScrollEvent { Delta = scrollValue });
    }

    private void OnPausePerformed(InputAction.CallbackContext _)
    {
        if (_isInputBlocked) return;
        EventBus.Instance?.Publish(new PausePressedEvent());
    }

    #endregion

    #region Utils & Gates

    private void PublishIfAllowed<T>(T evt) where T : struct
    {
        if (_isInputBlocked) return;
        EventBus.Instance?.Publish(evt);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (_playerMap == null) return;

        if (evt.NewState == GameState.Playing) _playerMap.Enable();
        else _playerMap.Disable();
    }

    public void SetInputBlocked(bool blocked)
    {
        _isInputBlocked = blocked;

        if (blocked)
        {
            _playerMap?.Disable();
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing)
            _playerMap?.Enable();
    }

    private void OnPlayerDeathStarted(PlayerDeathStartedEvent evt)
    {
        BlockGameplayInputImmediately();
    }

    private void BlockGameplayInputImmediately()
    {
        EventBus.Instance?.Publish(new MoveInputEvent { Direction = Vector2.zero });
        EventBus.Instance?.Publish(new JumpInputEvent { IsStarted = false });
        EventBus.Instance?.Publish(new PrimaryAttackEvent { IsStarted = false });
        EventBus.Instance?.Publish(new SecondaryAttackEvent { IsStarted = false });

        _isInputBlocked = true;
        _playerMap?.Disable();
    }

    public Vector2 GetMousePosition() => _pointAction?.ReadValue<Vector2>() ?? Vector2.zero;
    public Vector2 GetMouseDelta() => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    public Vector2 GetLookInput() => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
    public bool IsGamepadLookActive()
    {
        Vector2 lookInput = GetLookInput();
        float deadzone = Mathf.Max(0f, _gamepadAimDeadzone);
        return lookInput.sqrMagnitude >= deadzone * deadzone && IsLookInputFromGamepad();
    }

    public bool IsGamepadAimActive() => IsGamepadLookActive();
    public bool IsSecondaryAttackStartedFromGamepad() => _isSecondaryAttackStartedFromGamepad;
    public bool IsGamepadControlSchemeActive()
    {
        if (_playerInput != null && !string.IsNullOrEmpty(_playerInput.currentControlScheme))
        {
            if (_playerInput.currentControlScheme.Contains("Gamepad"))
                return true;
        }

        return IsLookInputFromGamepad();
    }

    public Vector2 GetAimWorldPosition(Vector2 origin, float radius, Camera camera, Vector2 fallbackWorldPosition)
    {
        if (camera == null)
            return fallbackWorldPosition;

        if (!PointerWorldPositionUtility.TryGetMouseWorldPosition(
                camera,
                this,
                ignorePointerOutsideScreen: true,
                out Vector2 worldPosition))
        {
            return fallbackWorldPosition;
        }

        return worldPosition;
    }

    public bool IsLookInputFromGamepad()
    {
        return _lookAction?.activeControl?.device is Gamepad;
    }

    public bool IsMouseAiming()
    {
        if (Mouse.current == null)
            return false;

        Vector2 mouseDelta = GetMouseDelta();
        return mouseDelta.sqrMagnitude > 0.0001f;
    }

    #endregion
}
