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
    [SerializeField] private bool _ignoreEnemyCollisionWhileDashing = true;
    [SerializeField] private string _enemyLayerName = "Enemy";
    [SerializeField] private bool _useInvincibleLayerWhileDashing = true;
    [SerializeField] private string _invincibleLayerName = "Invincible";

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
    private bool _jumpRequestPending;

    private bool _isDashing;
    private float _lastDashTime = -999f;
    private float _dashTimer;
    private Vector2 _dashDirection;

    private bool _wasGrounded;
    private bool _jumpConsumed;
    private int _enemyLayer = -1;
    private int _dashCollisionPlayerLayer = -1;
    private bool _isIgnoringEnemyCollision;
    private int _dashInvincibleLayer = -1;
    private int _dashOriginalLayer = -1;
    private bool _isDashLayerOverridden;

    public bool IsDashing => _isDashing;
    public bool IsJumping => !_isDashing && _sensor != null && !_sensor.IsGrounded;
    public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
    public float HorizontalInput => _horizontalInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sensor = GetComponent<GroundSensor2D>();
        _defaultGravityScale = _rb.gravityScale;
        _enemyLayer = LayerMask.NameToLayer(_enemyLayerName);
        _dashInvincibleLayer = LayerMask.NameToLayer(_invincibleLayerName);
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
                _coyoteTimer = _coyoteTime;
            else
                _coyoteTimer -= Time.deltaTime;
        }

        _wasGrounded = isGrounded;
    }

    private void FixedUpdate()
    {
        _velocity = _rb.linearVelocity;

        if (_isDashing)
        {
            TickDash();
            _rb.linearVelocity = _velocity;
            return;
        }

        bool isGrounded = _sensor != null && _sensor.IsGrounded;
        bool canJump = !_jumpConsumed && (isGrounded || _coyoteTimer > 0f);

        if (_jumpRequestPending && _jumpBufferTimer > 0f && canJump)
            ExecuteJump();

        HandleHorizontalMovement();
        HandleGravityAndJumpApex();

        _rb.linearVelocity = _velocity;
    }

    public void SetHorizontalInput(float inputX) => _horizontalInput = inputX;

    public void RequestJump()
    {
        _jumpBufferTimer = _jumpBufferTime;
        _jumpRequestPending = true;
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

        SetEnemyCollisionIgnoredForDash(true);
        SetDashInvincibleLayer(true);

        if (_dashLocksGravity)
            _rb.gravityScale = 0f;
    }

    private void OnDisable()
    {
        SetEnemyCollisionIgnoredForDash(false);
        SetDashInvincibleLayer(false);

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
        SetEnemyCollisionIgnoredForDash(false);
        SetDashInvincibleLayer(false);
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

    private void SetEnemyCollisionIgnoredForDash(bool ignored)
    {
        if (!_ignoreEnemyCollisionWhileDashing)
            return;

        if (_enemyLayer < 0 || _enemyLayer > 31)
            return;

        if (ignored)
        {
            if (_isIgnoringEnemyCollision)
                return;

            _dashCollisionPlayerLayer = gameObject.layer;
            if (_dashCollisionPlayerLayer < 0 || _dashCollisionPlayerLayer > 31)
                return;

            Physics2D.IgnoreLayerCollision(_dashCollisionPlayerLayer, _enemyLayer, true);
            _isIgnoringEnemyCollision = true;
            return;
        }

        if (!_isIgnoringEnemyCollision)
            return;

        if (_dashCollisionPlayerLayer >= 0 && _dashCollisionPlayerLayer <= 31)
            Physics2D.IgnoreLayerCollision(_dashCollisionPlayerLayer, _enemyLayer, false);

        _dashCollisionPlayerLayer = -1;
        _isIgnoringEnemyCollision = false;
    }

    private void SetDashInvincibleLayer(bool active)
    {
        if (!_useInvincibleLayerWhileDashing)
            return;

        if (_dashInvincibleLayer < 0 || _dashInvincibleLayer > 31)
            return;

        if (active)
        {
            if (_isDashLayerOverridden)
                return;

            _dashOriginalLayer = gameObject.layer;
            if (_dashOriginalLayer == _dashInvincibleLayer)
                return;

            gameObject.layer = _dashInvincibleLayer;
            _isDashLayerOverridden = true;
            return;
        }

        if (!_isDashLayerOverridden)
            return;

        if (_dashOriginalLayer >= 0 && _dashOriginalLayer <= 31)
            gameObject.layer = _dashOriginalLayer;

        _dashOriginalLayer = -1;
        _isDashLayerOverridden = false;
    }
}
