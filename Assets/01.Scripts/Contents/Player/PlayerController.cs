using UnityEngine;

[RequireComponent(typeof(PlatformerMotor2D))]
public class PlayerController : MonoBehaviour
{
    private PlatformerMotor2D _motor;

    [SerializeField] private float _controlRadius = 20f;
    [SerializeField] private bool _showControlRadiusGizmo = true;

    [Header("Aim Provider")]
    [SerializeField] private WeaponAimCursor _weaponAimCursor;
    [SerializeField] private WeaponSensor _weaponSensor;
    [SerializeField] private WeaponModeController _weaponModeController;

    private Camera _mainCamera;

    public float ControlRadius => _controlRadius;
    public int FacingSign { get; private set; } = 1;
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public bool IsMoving => Mathf.Abs(MoveInput.x) > 0.01f;
    public bool IsDashing => _motor != null && _motor.IsDashing;
    public bool IsJumping => _motor != null && _motor.IsJumping;
    public bool IsFacingLeft => FacingSign < 0;

    private void Awake()
    {
        _motor = GetComponent<PlatformerMotor2D>();
        _controlRadius = Mathf.Max(0f, _controlRadius);
        _mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<MoveInputEvent>(OnMoveInput);
            EventBus.Instance.Subscribe<JumpInputEvent>(OnJumpInput);
            EventBus.Instance.Subscribe<DashInputEvent>(OnDashInput);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<MoveInputEvent>(OnMoveInput);
            EventBus.Instance.Unsubscribe<JumpInputEvent>(OnJumpInput);
            EventBus.Instance.Unsubscribe<DashInputEvent>(OnDashInput);
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
        UpdateAimDirection();
    }

    public void SetAimProvider(WeaponAimCursor aimCursor, WeaponSensor weaponSensor)
    {
        _weaponAimCursor = aimCursor;
        _weaponSensor = weaponSensor;
    }

    public void SetWeaponModeController(WeaponModeController modeController)
    {
        _weaponModeController = modeController;
    }

    private void OnMoveInput(MoveInputEvent evt)
    {
        MoveInput = evt.Direction;
        _motor.SetHorizontalInput(evt.Direction.x);
    }

    private void OnJumpInput(JumpInputEvent evt)
    {
        if (evt.IsStarted)
            _motor.RequestJump();
        else
            _motor.CancelJump();
    }

    private void OnDashInput(DashInputEvent evt)
    {
        if (!evt.IsStarted)
            return;

        Vector2 dashDirection;

        if (Mathf.Abs(MoveInput.x) > 0.01f)
            dashDirection = new Vector2(Mathf.Sign(MoveInput.x), 0f);
        else
            dashDirection = new Vector2(FacingSign, 0f);

        _motor.RequestDash(dashDirection);
    }

    private void UpdateAimDirection()
    {
        if (InputReader.Instance == null)
            return;

        if (InputReader.Instance.IsInputBlocked)
            return;

        _weaponAimCursor?.Tick();

        if (_weaponModeController != null && _weaponModeController.CurrentMode == WeaponMode.Melee)
        {
            UpdateMeleeAimDirectionFromLook();
            return;
        }

        Vector2 lookInput = InputReader.Instance.GetLookInput();
        if (InputReader.Instance.IsGamepadLookActive() && lookInput.sqrMagnitude > 0.0001f)
        {
            AimDirection = lookInput.normalized;
            FacingSign = AimDirection.x >= 0f ? 1 : -1;
            return;
        }

        Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null)
            return;

        Vector2 origin = transform.position;
        Vector2 aimWorld = ResolveAimWorldPosition(cam);
        Vector2 dir = aimWorld - origin;

        if (dir.sqrMagnitude <= 0.0001f)
            return;

        AimDirection = dir.normalized;
        FacingSign = AimDirection.x >= 0f ? 1 : -1;
    }

    private Vector2 ResolveAimWorldPosition(Camera cam)
    {
        if (_weaponAimCursor != null && _weaponAimCursor.IsInitialized)
            return _weaponAimCursor.AimWorldPosition;

        if (_weaponSensor != null)
            return _weaponSensor.GetMouseWorldPosition();

        Vector2 screenPos = InputReader.Instance.GetMousePosition();
        return cam.ScreenToWorldPoint(screenPos);
    }

    private void UpdateMeleeAimDirectionFromLook()
    {
        Vector2 lookInput = InputReader.Instance.GetLookInput();
        float absX = Mathf.Abs(lookInput.x);
        if (InputReader.Instance.IsGamepadLookActive() && absX > 0.15f)
        {
            FacingSign = lookInput.x >= 0f ? 1 : -1;
            AimDirection = new Vector2(FacingSign, 0f);
            return;
        }

        if (Mathf.Abs(MoveInput.x) > 0.01f)
            FacingSign = MoveInput.x >= 0f ? 1 : -1;

        AimDirection = new Vector2(FacingSign, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!_showControlRadiusGizmo)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _controlRadius);
    }
}
