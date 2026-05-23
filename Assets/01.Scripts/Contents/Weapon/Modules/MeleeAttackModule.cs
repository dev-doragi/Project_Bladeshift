using UnityEngine;

public class MeleeAttackModule : WeaponActionModule
{
    [SerializeField] private Transform _meleePivot;
    [SerializeField] private float _swingAngle = 90f;
    [SerializeField] private float _swingDuration = 0.18f;
    [SerializeField] private float _returnDuration = 0.08f;
    [SerializeField] private int _damage = 1;
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private float _hitRadius = 1.2f;
    [SerializeField, Range(0f, 1f)] private float _hitTimeNormalized = 0.45f;

    private enum AttackPhase
    {
        Idle,
        Swing,
        Return
    }

    private AttackPhase _phase = AttackPhase.Idle;
    private Quaternion _restRotation;
    private Quaternion _swingRotation;
    private float _phaseTime;
    private bool _didHit;
    private bool _hasRestRotation;

    private void Awake()
    {
        EnsureDefaultTargetLayer();
    }

    public override void OnPress()
    {
        if (Controller == null || Controller.CurrentMode != WeaponMode.Melee) return;
        if (_phase != AttackPhase.Idle) return;

        EnsurePivot();
        if (_meleePivot == null) return;

        CacheRestRotation();
        float facingSign = ResolveFacingSign();
        _swingRotation = _restRotation * Quaternion.Euler(0f, 0f, _swingAngle * facingSign);
        _phaseTime = 0f;
        _didHit = false;
        _phase = AttackPhase.Swing;
    }

    public override void OnTick()
    {
        if (Controller == null || _meleePivot == null) return;
        if (_phase == AttackPhase.Idle) return;

        if (_phase == AttackPhase.Swing)
        {
            _phaseTime += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_phaseTime / Mathf.Max(0.0001f, _swingDuration));
            _meleePivot.localRotation = Quaternion.Slerp(_restRotation, _swingRotation, t);

            if (!_didHit && t >= _hitTimeNormalized)
            {
                _didHit = true;
                PerformHit();
            }

            if (_phaseTime >= _swingDuration)
            {
                if (!_didHit) PerformHit();
                _phaseTime = 0f;
                _phase = AttackPhase.Return;
            }

            return;
        }

        _phaseTime += Time.fixedDeltaTime;
        float returnT = Mathf.Clamp01(_phaseTime / Mathf.Max(0.0001f, _returnDuration));
        _meleePivot.localRotation = Quaternion.Slerp(_swingRotation, _restRotation, returnT);
        if (_phaseTime >= _returnDuration)
        {
            _meleePivot.localRotation = _restRotation;
            _phase = AttackPhase.Idle;
            _phaseTime = 0f;
        }
    }

    private void PerformHit()
    {
        if (Controller == null || Controller.Combat == null || _meleePivot == null) return;

        Vector2 forward = _meleePivot.right;
        Controller.Combat.PerformMeleeDamage(_meleePivot.position, _hitRadius, _damage, _targetLayer, forward);
    }

    private float ResolveFacingSign()
    {
        if (Controller == null || Controller.PlayerTransform == null) return 1f;
        PlayerController playerController = Controller.PlayerTransform.GetComponent<PlayerController>();
        if (playerController == null) return 1f;
        return playerController.FacingSign >= 0 ? 1f : -1f;
    }

    private void EnsurePivot()
    {
        if (_meleePivot != null) return;

        WeaponMeleeAttachment attachment = Controller != null ? Controller.MeleeAttachment : null;
        if (attachment != null) _meleePivot = attachment.MeleePivot;
    }

    private void CacheRestRotation()
    {
        if (_hasRestRotation || _meleePivot == null) return;

        _restRotation = _meleePivot.localRotation;
        _hasRestRotation = true;
    }

    private void EnsureDefaultTargetLayer()
    {
        if (_targetLayer.value != 0) return;
        int enemyMask = LayerMask.GetMask("Enemy");
        if (enemyMask != 0) _targetLayer = enemyMask;
    }
}
