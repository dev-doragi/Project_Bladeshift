using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimController : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    [Header("Parameter Names")]
    [SerializeField] private string _isMovingParam = "IsMoving";
    [SerializeField] private string _isDashingParam = "IsDashing";
    [SerializeField] private string _isBackwardMoveParam = "IsBackwardMove";
    [SerializeField] private string _deadParam = "Dead";

    [Header("Backward Move")]
    [SerializeField] private float _backwardThreshold = -0.1f;

    private PlayerController _controller;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_animator == null || _controller == null)
            return;

        Vector2 moveInput = _controller.MoveInput;
        Vector2 aimDirection = _controller.AimDirection;

        bool isMoving = _controller.IsMoving;
        bool isDashing = _controller.IsDashing;
        bool isBackwardMove = false;

        if (moveInput.sqrMagnitude > 0.0001f && aimDirection.sqrMagnitude > 0.0001f)
        {
            float dot = Vector2.Dot(moveInput.normalized, aimDirection.normalized);
            isBackwardMove = dot < _backwardThreshold;
        }

        _animator.SetBool(_isMovingParam, isMoving);
        _animator.SetBool(_isDashingParam, isDashing);
        _animator.SetBool(_isBackwardMoveParam, isBackwardMove);
    }

    public void SetDead(bool isDead)
    {
        if (_animator == null)
            return;

        _animator.SetBool(_deadParam, isDead);
    }
}