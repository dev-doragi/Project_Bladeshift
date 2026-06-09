using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class WeaponStateMachine : MonoBehaviour
{
    [SerializeField] private WeaponState _initialState = WeaponState.Grounded;

    private Rigidbody2D _rb;
    private Collider2D _collider;

    public WeaponState CurrentState { get; private set; } = WeaponState.Grounded;
    public WeaponPinSource PinSource { get; private set; } = WeaponPinSource.None;
    public bool IsPinnedToWall => CurrentState == WeaponState.Pinned && PinSource == WeaponPinSource.Wall;
    public bool IsPinnedToEnemy => IsPinnedToCaptureEnemy || IsPinnedToEmbeddedEnemy;
    public bool IsPinnedToCaptureEnemy => CurrentState == WeaponState.Pinned &&
                                          (PinSource == WeaponPinSource.Enemy || PinSource == WeaponPinSource.EnemyCapture);
    public bool IsPinnedToEmbeddedEnemy => CurrentState == WeaponState.Pinned && PinSource == WeaponPinSource.EnemyEmbedded;
    public event Action<WeaponState, WeaponState> StateChanged;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        CurrentState = _initialState;
    }

    public void Initialize(Rigidbody2D rb, Collider2D weaponCollider)
    {
        _rb = rb != null ? rb : GetComponent<Rigidbody2D>();
        _collider = weaponCollider != null ? weaponCollider : GetComponent<Collider2D>();
        CurrentState = _initialState;
        ApplyPhysicsMode(CurrentState);
    }

    public void ChangeState(WeaponState newState)
    {
        if (CurrentState == newState) return;

        WeaponState previousState = CurrentState;
        CurrentState = newState;
        if (CurrentState != WeaponState.Pinned)
            ClearPinSource();
        EventBus.Instance?.Publish(new WeaponStateChangeEvent { NewState = CurrentState });
        ApplyPhysicsMode(CurrentState);
        StateChanged?.Invoke(previousState, CurrentState);
    }

    public void SetPinSource(WeaponPinSource source)
    {
        PinSource = source;
    }

    public void ClearPinSource()
    {
        PinSource = WeaponPinSource.None;
    }

    public void ForceApplyCurrentState()
    {
        ApplyPhysicsMode(CurrentState);
    }

    private void ApplyPhysicsMode(WeaponState state)
    {
        if (_rb == null || _collider == null) return;

        UpdateCollisionInteractions();

        switch (state)
        {
            case WeaponState.Grounded:
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _collider.isTrigger = false;
                break;

            case WeaponState.Controlled:
            case WeaponState.Slashing:
            case WeaponState.Thrusting:
            case WeaponState.PinningFlight:
            case WeaponState.Pinned:
            case WeaponState.Returning:
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _collider.isTrigger = true;
                break;
        }
    }

    private void UpdateCollisionInteractions()
    {
        int weaponLayer = gameObject.layer;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        int environmentLayer = LayerMask.NameToLayer("Environment");

        CollisionPolicyService.SetIgnoreLayerCollision(weaponLayer, enemyLayer, true);
        CollisionPolicyService.SetIgnoreLayerCollision(weaponLayer, environmentLayer, false);
    }
}
