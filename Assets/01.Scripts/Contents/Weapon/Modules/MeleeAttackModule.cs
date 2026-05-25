using System.Collections.Generic;
using UnityEngine;

public class MeleeAttackModule : WeaponActionModule
{
    [System.Serializable]
    private struct MeleeComboStep
    {
        public string Name;

        [Header("Motion")]
        public Vector2 WindupOffset;
        public Vector2 ArcOffset;
        public Vector2 StrikeOffset;

        public float WindupAngle;
        public float ArcAngle;
        public float StrikeAngle;

        public float WindupDuration;
        public float StrikeDuration;
        public float ReturnDuration;

        public AnimationCurve StrikeCurve;
        public AnimationCurve ReturnCurve;

        [Header("Hit Sweep")]
        public int Damage;
        public float HitRadius;
        public int SweepSamples;
    }

    private enum AttackPhase
    {
        Idle,
        Windup,
        Strike,
        Return
    }

    [Header("Combo")]
    [SerializeField] private MeleeComboStep[] _comboSteps;
    [SerializeField] private float _comboResetDelay = 0.65f;

    [Header("Hit")]
    [SerializeField] private int _defaultDamage = 1;
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private float _defaultHitRadius = 1.5f;
    [SerializeField] private int _hitBufferSize = 32;
    [SerializeField] private float _knockbackPower = 10f;
    [SerializeField] private bool _hitTriggers = true;

    [Header("Trail")]
    [SerializeField] private TrailRenderer _slashTrail;
    [SerializeField] private bool _useTrail = true;

    private AttackPhase _phase = AttackPhase.Idle;
    private int _comboIndex;
    private int _activeStepIndex;
    private float _phaseTime;
    private float _lastComboEndTime;
    private bool _bufferedInput;
    private float _lockedFacingSign = 1f;

    private Vector2 _previousSweepPosition;
    private bool _hasPreviousSweepPosition;

    private Collider2D[] _hitBuffer;
    private readonly HashSet<IDamageable> _hitTargets = new();

    private ContactFilter2D _hitFilter;
    private int _cachedTargetLayerMask;
    private bool _cachedHitTriggers;
    private bool _hasCachedHitFilter;

    private void Awake()
    {
        EnsureDefaultTargetLayer();
        EnsureDefaultComboSteps();
        EnsureHitBuffer();

        if (_slashTrail != null)
            _slashTrail.emitting = false;
    }

    private void Reset()
    {
        _comboSteps = CreateDefaultComboSteps();
    }

    public override void OnPress()
    {
        if (!CanUseMelee())
            return;

        EnsureDefaultComboSteps();

        if (_phase == AttackPhase.Idle)
        {
            if (Time.time - _lastComboEndTime > _comboResetDelay)
                _comboIndex = 0;

            StartStep(_comboIndex);
            return;
        }

        _bufferedInput = true;
    }

    public override void OnTick()
    {
        if (_phase == AttackPhase.Idle)
            return;

        if (!CanUseMelee())
        {
            CancelAttack();
            return;
        }

        MeleeComboStep step = _comboSteps[_activeStepIndex];

        switch (_phase)
        {
            case AttackPhase.Windup:
                TickWindup(step);
                break;

            case AttackPhase.Strike:
                TickStrike(step);
                break;

            case AttackPhase.Return:
                TickReturn(step);
                break;
        }
    }

    public override void OnRelease()
    {
    }

    private void StartStep(int index)
    {
        if (_comboSteps == null || _comboSteps.Length == 0)
            return;

        _activeStepIndex = Mathf.Clamp(index, 0, _comboSteps.Length - 1);
        _phase = AttackPhase.Windup;
        _phaseTime = 0f;
        _bufferedInput = false;
        _lockedFacingSign = ResolveFacingSign();

        _hitTargets.Clear();
        _hasPreviousSweepPosition = false;
        Controller.MeleeHoverFollow?.BeginAttackAnchor();

        SetTrail(false);
        ApplyPose(Vector2.zero, 0f);
    }

    private void TickWindup(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.WindupDuration);
        float eased = EaseInOut(t);

        Vector2 offset = Vector2.LerpUnclamped(Vector2.zero, ResolveOffset(step.WindupOffset), eased);
        float angle = Mathf.LerpUnclamped(0f, ResolveAngle(step.WindupAngle), eased);

        ApplyPose(offset, angle);

        if (t < 1f)
            return;

        _phase = AttackPhase.Strike;
        _phaseTime = 0f;
        _hasPreviousSweepPosition = false;
        SetTrail(true);
    }

    private void TickStrike(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.StrikeDuration);
        float curvedT = EvaluateCurve(step.StrikeCurve, t, EaseSlash(t));

        Vector2 start = ResolveOffset(step.WindupOffset);
        Vector2 control = ResolveOffset(step.ArcOffset);
        Vector2 end = ResolveOffset(step.StrikeOffset);

        Vector2 offset = QuadraticBezier(start, control, end, curvedT);

        float angleA = ResolveAngle(step.WindupAngle);
        float angleB = ResolveAngle(step.ArcAngle);
        float angleC = ResolveAngle(step.StrikeAngle);

        float angle = curvedT < 0.5f
            ? Mathf.LerpUnclamped(angleA, angleB, EaseOut(curvedT * 2f))
            : Mathf.LerpUnclamped(angleB, angleC, EaseOut((curvedT - 0.5f) * 2f));

        ApplyPose(offset, angle);

        if (TryEvaluateWorldPose(offset, angle, out Vector3 worldPosition, out Quaternion worldRotation))
        {
            PerformSweepHit(step, worldPosition, worldRotation);

            _previousSweepPosition = worldPosition;
            _hasPreviousSweepPosition = true;
        }

        if (t < 1f)
            return;

        _phase = AttackPhase.Return;
        _phaseTime = 0f;
        SetTrail(false);
    }

    private void TickReturn(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.ReturnDuration);
        float curvedT = EvaluateCurve(step.ReturnCurve, t, EaseInOut(t));

        Vector2 offset = Vector2.LerpUnclamped(ResolveOffset(step.StrikeOffset), Vector2.zero, curvedT);
        float angle = Mathf.LerpUnclamped(ResolveAngle(step.StrikeAngle), 0f, curvedT);

        ApplyPose(offset, angle);

        if (t < 1f)
            return;

        FinishStep();
    }

    private void FinishStep()
    {
        Controller.MeleeHoverFollow?.EndAttackAnchor();
        Controller.MeleeHoverFollow?.ClearAttackPose();
        SetTrail(false);

        _phase = AttackPhase.Idle;
        _phaseTime = 0f;
        _lastComboEndTime = Time.time;
        _hasPreviousSweepPosition = false;

        int nextIndex = (_activeStepIndex + 1) % _comboSteps.Length;

        if (_bufferedInput)
        {
            _comboIndex = nextIndex;
            StartStep(_comboIndex);
            return;
        }

        _comboIndex = nextIndex;
    }

    private void CancelAttack()
    {
        Controller?.MeleeHoverFollow?.EndAttackAnchor();
        Controller?.MeleeHoverFollow?.ClearAttackPose();
        SetTrail(false);

        _phase = AttackPhase.Idle;
        _phaseTime = 0f;
        _bufferedInput = false;
        _lastComboEndTime = Time.time;
        _hasPreviousSweepPosition = false;
        _hitTargets.Clear();
    }

    private void ApplyPose(Vector2 resolvedOffset, float resolvedAngle)
    {
        Controller.MeleeHoverFollow?.SetAttackPose(resolvedOffset, resolvedAngle);
    }

    private bool TryEvaluateWorldPose(Vector2 resolvedOffset, float resolvedAngle, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = transform.position;
        worldRotation = transform.rotation;

        WeaponMeleeHoverFollow hoverFollow = Controller != null ? Controller.MeleeHoverFollow : null;
        if (hoverFollow == null)
            return false;

        return hoverFollow.TryEvaluateAttackWorldPose(resolvedOffset, resolvedAngle, out worldPosition, out worldRotation);
    }

    private void PerformSweepHit(MeleeComboStep step, Vector3 currentPosition, Quaternion currentRotation)
    {
        EnsureHitBuffer();

        float radius = step.HitRadius > 0f ? step.HitRadius : _defaultHitRadius;
        int damage = step.Damage > 0 ? step.Damage : _defaultDamage;
        int samples = Mathf.Max(1, step.SweepSamples);

        Vector2 start = _hasPreviousSweepPosition ? _previousSweepPosition : currentPosition;
        Vector2 end = currentPosition;
        Vector2 forward = currentRotation * Vector3.right;

        for (int i = 0; i <= samples; i++)
        {
            float t = samples <= 0 ? 1f : i / (float)samples;
            Vector2 samplePoint = Vector2.Lerp(start, end, t);

            int count = Physics2D.OverlapCircle(samplePoint, radius, _hitFilter, _hitBuffer);

            for (int h = 0; h < count; h++)
            {
                Collider2D hit = _hitBuffer[h];
                if (hit == null)
                    continue;

                IDamageable damageable = ResolveDamageable(hit);
                if (damageable == null || damageable.IsDead)
                    continue;

                if (!_hitTargets.Add(damageable))
                    continue;

                Vector2 hitPoint = hit.ClosestPoint(samplePoint);
                Vector2 knockbackDirection = forward.sqrMagnitude > 0.0001f
                    ? forward.normalized
                    : Vector2.right * _lockedFacingSign;

                damageable.TakeDamage(new DamageData
                {
                    Damage = damage,
                    AttackerTeam = TeamType.Player,
                    HitPoint = hitPoint,
                    KnockbackForce = knockbackDirection * _knockbackPower,
                    IsPiercing = false
                });
            }
        }
    }

    private IDamageable ResolveDamageable(Collider2D hit)
    {
        if (hit.TryGetComponent<IDamageable>(out var damageable))
            return damageable;

        return hit.GetComponentInParent<IDamageable>();
    }

    private Vector2 ResolveOffset(Vector2 offset)
    {
        return new Vector2(offset.x * _lockedFacingSign, offset.y);
    }

    private float ResolveAngle(float angle)
    {
        return angle * _lockedFacingSign;
    }

    private float ResolveFacingSign()
    {
        if (Controller == null || Controller.PlayerTransform == null)
            return 1f;

        PlayerController playerController = Controller.PlayerTransform.GetComponent<PlayerController>();
        if (playerController == null)
            return 1f;

        return playerController.FacingSign >= 0 ? 1f : -1f;
    }

    private bool CanUseMelee()
    {
        return Controller != null && Controller.CurrentMode == WeaponMode.Melee;
    }

    private float NormalizeTime(float time, float duration)
    {
        return Mathf.Clamp01(time / Mathf.Max(0.0001f, duration));
    }

    private Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private float EvaluateCurve(AnimationCurve curve, float t, float fallback)
    {
        if (curve == null || curve.length == 0)
            return fallback;

        return curve.Evaluate(t);
    }

    private float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseSlash(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }

    private void SetTrail(bool emitting)
    {
        if (!_useTrail || _slashTrail == null)
            return;

        _slashTrail.emitting = emitting;
    }

    private void EnsureHitBuffer()
    {
        int size = Mathf.Max(8, _hitBufferSize);

        if (_hitBuffer == null || _hitBuffer.Length != size)
            _hitBuffer = new Collider2D[size];

        if (_hasCachedHitFilter &&
            _cachedTargetLayerMask == _targetLayer.value &&
            _cachedHitTriggers == _hitTriggers)
        {
            return;
        }

        _hitFilter = new ContactFilter2D();
        _hitFilter.useLayerMask = true;
        _hitFilter.SetLayerMask(_targetLayer);
        _hitFilter.useTriggers = _hitTriggers;
        _hitFilter.useDepth = false;
        _hitFilter.useNormalAngle = false;

        _cachedTargetLayerMask = _targetLayer.value;
        _cachedHitTriggers = _hitTriggers;
        _hasCachedHitFilter = true;
    }

    private void EnsureDefaultTargetLayer()
    {
        if (_targetLayer.value != 0)
            return;

        int enemyMask = LayerMask.GetMask("Enemy");
        if (enemyMask != 0)
            _targetLayer = enemyMask;
    }

    private void EnsureDefaultComboSteps()
    {
        if (_comboSteps != null && _comboSteps.Length > 0)
            return;

        _comboSteps = CreateDefaultComboSteps();
    }

    private MeleeComboStep[] CreateDefaultComboSteps()
    {
        return new[]
        {
            new MeleeComboStep
            {
                Name = "Slash_01",
                WindupOffset = new Vector2(-0.8f, 0.45f),
                ArcOffset = new Vector2(2.45f, 0.85f),
                StrikeOffset = new Vector2(5.2f, -1.15f),
                WindupAngle = -45f,
                ArcAngle = 35f,
                StrikeAngle = 115f,
                WindupDuration = 0.07f,
                StrikeDuration = 0.18f,
                ReturnDuration = 0.12f,
                StrikeCurve = null,
                ReturnCurve = null,
                Damage = 10,
                HitRadius = 1.0f,
                SweepSamples = 7
            },
            new MeleeComboStep
            {
                Name = "Slash_02",
                WindupOffset = new Vector2(-0.7f, -0.2f),
                ArcOffset = new Vector2(2.9f, -1.65f),
                StrikeOffset = new Vector2(5.6f, -0.15f),
                WindupAngle = 30f,
                ArcAngle = -75f,
                StrikeAngle = -145f,
                WindupDuration = 0.06f,
                StrikeDuration = 0.18f,
                ReturnDuration = 0.12f,
                StrikeCurve = null,
                ReturnCurve = null,
                Damage = 15,
                HitRadius = 1.05f,
                SweepSamples = 8
            },
            new MeleeComboStep
            {
                Name = "Slash_03",
                WindupOffset = new Vector2(-0.95f, 0.55f),
                ArcOffset = new Vector2(3.2f, 1.0f),
                StrikeOffset = new Vector2(6.5f, -1.35f),
                WindupAngle = -55f,
                ArcAngle = 45f,
                StrikeAngle = 135f,
                WindupDuration = 0.08f,
                StrikeDuration = 0.22f,
                ReturnDuration = 0.14f,
                StrikeCurve = null,
                ReturnCurve = null,
                Damage = 10,
                HitRadius = 1.15f,
                SweepSamples = 9
            }
        };
    }
}
