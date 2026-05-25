using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(GroundSensor2D))]
public class PlatformerMotor2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _maxSpeed = 8f;
    [SerializeField] private float _acceleration = 50f;
    [SerializeField] private float _deceleration = 50f;

    [Header("Jump & Gravity")]
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _fallGravityMultiplier = 2.5f;
    [SerializeField] private float _lowJumpGravityMultiplier = 3f;
    [SerializeField] private float _apexBonus = 0.5f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 18f;
    [SerializeField] private float _dashDuration = 0.16f;
    [SerializeField] private float _dashCooldown = 0.45f;
    [SerializeField] private bool _dashLocksGravity = true;

    [Header("Forgiveness")]
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpBufferTime = 0.15f;

    private Rigidbody2D _rb;
    private GroundSensor2D _sensor;

    private Vector2 _velocity;
    private float _horizontalInput;
    private float _defaultGravityScale;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private bool _isJumpHeld;
    private bool _jumpRequestPending; // 점프 실행 신호 보관용

    private bool _isDashing;
    private float _lastDashTime = -999f;
    private float _dashTimer;
    private Vector2 _dashDirection;

    private bool _wasGrounded;
    private bool _jumpConsumed;

    public bool IsDashing => _isDashing;
    public bool IsJumping => !_isDashing && _sensor != null && !_sensor.IsGrounded;
    public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
    public float HorizontalInput => _horizontalInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sensor = GetComponent<GroundSensor2D>();
        _defaultGravityScale = _rb.gravityScale;
    }

    private void Update()
    {
        _jumpBufferTimer -= Time.deltaTime;

        if (_jumpBufferTimer <= 0f)
            _jumpRequestPending = false;

        bool isGrounded = _sensor != null && _sensor.IsGrounded;
        bool justLeftGround = _wasGrounded && !isGrounded;

        if (isGrounded)
        {
            if (_rb.linearVelocity.y <= 0.01f)
                _jumpConsumed = false;

            _coyoteTimer = 0f;
        }
        else
        {
            if (justLeftGround && !_jumpConsumed)
            {
                _coyoteTimer = _coyoteTime;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
            }
        }

        _wasGrounded = isGrounded;
    }

    private void FixedUpdate()
    {
        _velocity = _rb.linearVelocity;

        // 0. 대시 로직 처리
        if (_isDashing)
        {
            TickDash();
            _rb.linearVelocity = _velocity;
            return;
        }

        // 1. 점프 로직 처리 (입력 버퍼와 코요테 타임 확인)
        bool isGrounded = _sensor != null && _sensor.IsGrounded;
        bool canJump = !_jumpConsumed && (isGrounded || _coyoteTimer > 0f);

        if (_jumpRequestPending && _jumpBufferTimer > 0f && canJump)
        {
            ExecuteJump();
        }

        // 2. 이동 및 중력 처리
        HandleHorizontalMovement();
        HandleGravityAndJumpApex();

        // 3. 최종 속도 적용
        _rb.linearVelocity = _velocity;
    }

    public void SetHorizontalInput(float inputX) => _horizontalInput = inputX;

    public void RequestJump()
    {
        _jumpBufferTimer = _jumpBufferTime;
        _jumpRequestPending = true; // 점프 신호 접수
        _isJumpHeld = true;
    }

    public void CancelJump()
    {
        _isJumpHeld = false;
        _jumpRequestPending = false;
    }

    private void ExecuteJump()
    {
        _jumpRequestPending = false;
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        _jumpConsumed = true;

        _velocity.y = _jumpForce;
    }

    public void RequestDash(Vector2 direction)
    {
        if (_isDashing) return;
        if (Time.time < _lastDashTime + _dashCooldown) return;

        Vector2 dashDir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        _isDashing = true;
        _dashTimer = _dashDuration;
        _lastDashTime = Time.time;
        _dashDirection = dashDir;

        _jumpRequestPending = false;
        _jumpBufferTimer = 0f;
        _isJumpHeld = false;

        if (_dashLocksGravity)
            _rb.gravityScale = 0f;
    }

    private void OnDisable()
    {
        if (_rb != null)
            _rb.gravityScale = _defaultGravityScale;
    }

    private void TickDash()
    {
        _dashTimer -= Time.fixedDeltaTime;

        _velocity = _dashDirection * _dashSpeed;

        if (_dashTimer > 0f)
            return;

        _isDashing = false;
        _velocity = Vector2.zero;
        _rb.gravityScale = _defaultGravityScale;
    }

    private void HandleHorizontalMovement()
    {
        float targetSpeed = _horizontalInput * _maxSpeed;
        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? _acceleration : _deceleration;
        _velocity.x = Mathf.MoveTowards(_velocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
    }

    private void HandleGravityAndJumpApex()
    {
        if (_sensor.IsGrounded)
        {
            _rb.gravityScale = _defaultGravityScale;
            return;
        }

        if (_velocity.y > 0 && !_isJumpHeld)
            _rb.gravityScale = _defaultGravityScale * _lowJumpGravityMultiplier;
        else if (Mathf.Abs(_velocity.y) < 2f)
            _rb.gravityScale = _defaultGravityScale * _apexBonus;
        else if (_velocity.y < 0f)
            _rb.gravityScale = _defaultGravityScale * _fallGravityMultiplier;
        else
            _rb.gravityScale = _defaultGravityScale;
    }
}