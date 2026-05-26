using UnityEngine;

[RequireComponent(typeof(PlatformerMotor2D))]
public class PlayerController : MonoBehaviour
{
    private PlatformerMotor2D _motor;
    [SerializeField] private float _controlRadius = 20f;
    [SerializeField] private bool _showControlRadiusGizmo = true;

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

    private void Update()
    {
        UpdateAimDirection();
    }

    private void Start()
    {
        EventBus.Instance?.Publish(new PlayerSpawnedEvent
        {
            Player = this
        });
    }

    private void OnMoveInput(MoveInputEvent evt)
    {
        MoveInput = evt.Direction;

        _motor.SetHorizontalInput(evt.Direction.x);
    }

    private void OnJumpInput(JumpInputEvent evt)
    {
        if (evt.IsStarted) _motor.RequestJump();
        else _motor.CancelJump();
    }

    private void OnDashInput(DashInputEvent evt)
    {
        if (!evt.IsStarted) return;

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

        Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null)
            return;

        Vector2 origin = transform.position;
        float aimRadius = Mathf.Max(0f, _controlRadius);
        Vector2 fallbackWorldPosition = AimDirection.sqrMagnitude > 0.0001f
            ? origin + AimDirection.normalized * aimRadius
            : origin;
        Vector2 aimWorld = InputReader.Instance.GetAimWorldPosition(origin, aimRadius, cam, fallbackWorldPosition);
        Vector2 dir = aimWorld - origin;

        if (dir.sqrMagnitude <= 0.0001f)
            return;

        AimDirection = dir.normalized;
        FacingSign = AimDirection.x >= 0f ? 1 : -1;
    }

    private void OnDrawGizmos()
    {
        if (!_showControlRadiusGizmo) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _controlRadius);
    }
}
