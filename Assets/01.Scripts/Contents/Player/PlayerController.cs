using UnityEngine;

[RequireComponent(typeof(PlatformerMotor2D))]
public class PlayerController : MonoBehaviour
{
    private PlatformerMotor2D _motor;
    private PlayerAimResolver _aimResolver;

    [SerializeField] private float _controlRadius = 20f;
    [SerializeField] private bool _showControlRadiusGizmo = true;

    [Header("Aim Provider")]
    [SerializeField] private WeaponAimCursor _weaponAimCursor;
    [SerializeField] private WeaponSensor _weaponSensor;
    [SerializeField] private WeaponModeController _weaponModeController;

    private Camera _mainCamera;
    private PlayerAimState _aimState = PlayerAimState.Default;

    public float ControlRadius => _controlRadius;
    public int FacingSign => _aimState.FacingSign;
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection => _aimState.AimDirection;
    public PlayerAimState AimState => _aimState;
    public bool IsMoving => Mathf.Abs(MoveInput.x) > 0.01f;
    public bool IsDashing => _motor != null && _motor.IsDashing;
    public bool IsJumping => _motor != null && _motor.IsJumping;
    public bool IsFacingLeft => FacingSign < 0;

    private void Awake()
    {
        _motor = GetComponent<PlatformerMotor2D>();
        _aimResolver = GetComponent<PlayerAimResolver>();
        if (_aimResolver == null)
            _aimResolver = gameObject.AddComponent<PlayerAimResolver>();
        _controlRadius = Mathf.Max(0f, _controlRadius);
        _mainCamera = Camera.main;
        _aimResolver.Initialize(_mainCamera);
        _aimResolver.Configure(_weaponAimCursor, _weaponSensor, _weaponModeController);
        _aimState = PlayerAimState.Default;
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<PlayerLocomotionCommandEvent>(OnLocomotionCommand);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<PlayerLocomotionCommandEvent>(OnLocomotionCommand);
        }
    }

    private void Start()
    {
        EventBus.Instance?.Publish(new PlayerSpawnedEvent
        {
            Player = this
        });
    }

    private void Update()
    {
        if (_aimResolver == null)
            return;

        _aimState = _aimResolver.Resolve(transform, MoveInput, _aimState.FacingSign == 0 ? 1 : _aimState.FacingSign);
    }

    public void SetAimProvider(WeaponAimCursor aimCursor, WeaponSensor weaponSensor)
    {
        _weaponAimCursor = aimCursor;
        _weaponSensor = weaponSensor;
        _aimResolver?.Configure(_weaponAimCursor, _weaponSensor, _weaponModeController);
    }

    public void SetWeaponModeController(WeaponModeController modeController)
    {
        _weaponModeController = modeController;
        _aimResolver?.Configure(_weaponAimCursor, _weaponSensor, _weaponModeController);
    }

    private void OnLocomotionCommand(PlayerLocomotionCommandEvent evt)
    {
        switch (evt.Command.Type)
        {
            case PlayerLocomotionCommandType.Move:
                MoveInput = evt.Command.MoveVector;
                _motor.SetHorizontalInput(evt.Command.MoveVector.x);
                break;
            case PlayerLocomotionCommandType.Jump:
                if (evt.Command.IsStarted)
                    _motor.RequestJump();
                else
                    _motor.CancelJump();
                break;
            case PlayerLocomotionCommandType.Dash:
                if (!evt.Command.IsStarted)
                    return;

                Vector2 dashDirection = evt.Command.DashVector;
                if (Mathf.Abs(MoveInput.x) > 0.01f)
                    dashDirection = new Vector2(Mathf.Sign(MoveInput.x), 0f);
                else if (dashDirection.sqrMagnitude <= 0.0001f)
                    dashDirection = new Vector2(FacingSign, 0f);

                _motor.RequestDash(dashDirection);
                break;
        }
    }

    private void OnDrawGizmos()
    {
        if (!_showControlRadiusGizmo)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _controlRadius);
    }
}
