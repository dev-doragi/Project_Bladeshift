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

        [Range(0f, 1f)] public float HitTimeNormalized;

        [Header("Hit")]
        public int Damage;
        public float HitRadius;
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
    [SerializeField] private float _defaultHitRadius = 1.2f;

    [Header("Motion")]
    [SerializeField] private bool _invertAngleByFacing = true;
    [SerializeField] private bool _invertYOffsetByFacing = false;

    [Header("Trail")]
    [SerializeField] private TrailRenderer _slashTrail;
    [SerializeField] private bool _useTrail = true;

    private AttackPhase _phase = AttackPhase.Idle;
    private int _comboIndex;
    private int _activeStepIndex;
    private float _phaseTime;
    private float _lastComboEndTime;
    private bool _didHit;
    private bool _bufferedInput;
    private float _lockedFacingSign = 1f;

    private void Awake()
    {
        EnsureDefaultTargetLayer();
        EnsureDefaultComboSteps();

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
        _didHit = false;
        _bufferedInput = false;
        _lockedFacingSign = ResolveFacingSign();

        SetTrail(false);
        ApplyPose(Vector2.zero, 0f);
    }

    private void TickWindup(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.WindupDuration);
        float eased = EaseInOut(t);

        ApplyPose(
            Vector2.LerpUnclamped(Vector2.zero, ResolveOffset(step.WindupOffset), eased),
            Mathf.LerpUnclamped(0f, ResolveSignedAngle(step.WindupAngle), eased)
        );

        if (t < 1f)
            return;

        _phase = AttackPhase.Strike;
        _phaseTime = 0f;
        SetTrail(true);
    }

    private void TickStrike(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.StrikeDuration);
        float eased = EaseSlash(t);

        Vector2 start = ResolveOffset(step.WindupOffset);
        Vector2 control = ResolveOffset(step.ArcOffset);
        Vector2 end = ResolveOffset(step.StrikeOffset);

        Vector2 offset = QuadraticBezier(start, control, end, eased);

        float angleA = ResolveSignedAngle(step.WindupAngle);
        float angleB = ResolveSignedAngle(step.ArcAngle);
        float angleC = ResolveSignedAngle(step.StrikeAngle);

        float angle = t < 0.5f
            ? Mathf.LerpUnclamped(angleA, angleB, EaseOut(t * 2f))
            : Mathf.LerpUnclamped(angleB, angleC, EaseOut((t - 0.5f) * 2f));

        ApplyPose(offset, angle);

        if (!_didHit && t >= step.HitTimeNormalized)
        {
            _didHit = true;
            PerformHit(step);
        }

        if (t < 1f)
            return;

        if (!_didHit)
            PerformHit(step);

        _phase = AttackPhase.Return;
        _phaseTime = 0f;
        SetTrail(false);
    }

    private void TickReturn(MeleeComboStep step)
    {
        _phaseTime += Time.deltaTime;

        float t = NormalizeTime(_phaseTime, step.ReturnDuration);
        float eased = EaseInOut(t);

        ApplyPose(
            Vector2.LerpUnclamped(ResolveOffset(step.StrikeOffset), Vector2.zero, eased),
            Mathf.LerpUnclamped(ResolveSignedAngle(step.StrikeAngle), 0f, eased)
        );

        if (t < 1f)
            return;

        FinishStep();
    }

    private void FinishStep()
    {
        Controller.MeleeHoverFollow?.ClearAttackPose();
        SetTrail(false);

        _phase = AttackPhase.Idle;
        _phaseTime = 0f;
        _lastComboEndTime = Time.time;

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
        Controller?.MeleeHoverFollow?.ClearAttackPose();
        SetTrail(false);

        _phase = AttackPhase.Idle;
        _phaseTime = 0f;
        _didHit = false;
        _bufferedInput = false;
        _lastComboEndTime = Time.time;
    }

    private void ApplyPose(Vector2 localOffset, float localAngle)
    {
        Controller.MeleeHoverFollow?.SetAttackPose(localOffset, localAngle);
    }

    private void PerformHit(MeleeComboStep step)
    {
        if (Controller == null || Controller.Combat == null)
            return;

        WeaponMeleeHoverFollow hoverFollow = Controller.MeleeHoverFollow;

        Vector3 origin = hoverFollow != null
            ? hoverFollow.CurrentTargetPosition
            : transform.position;

        Vector2 forward = hoverFollow != null
            ? hoverFollow.CurrentForward
            : Vector2.right * _lockedFacingSign;

        int damage = step.Damage > 0 ? step.Damage : _defaultDamage;
        float radius = step.HitRadius > 0f ? step.HitRadius : _defaultHitRadius;

        Controller.Combat.PerformMeleeDamage(origin, radius, damage, _targetLayer, forward);
    }

    private Vector2 ResolveOffset(Vector2 offset)
    {
        float y = _invertYOffsetByFacing ? offset.y * _lockedFacingSign : offset.y;
        return new Vector2(offset.x, y);
    }

    private float ResolveSignedAngle(float angle)
    {
        if (!_invertAngleByFacing)
            return angle;

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
                WindupOffset = new Vector2(-0.8f, 0.35f),
                ArcOffset = new Vector2(2.8f, 1.4f),
                StrikeOffset = new Vector2(5.5f, 0.2f),
                WindupAngle = -35f,
                ArcAngle = 70f,
                StrikeAngle = 150f,
                WindupDuration = 0.07f,
                StrikeDuration = 0.16f,
                ReturnDuration = 0.12f,
                HitTimeNormalized = 0.55f,
                Damage = 1,
                HitRadius = 1.4f
            },
            new MeleeComboStep
            {
                Name = "Slash_02",
                WindupOffset = new Vector2(-0.7f, -0.25f),
                ArcOffset = new Vector2(3.2f, -1.25f),
                StrikeOffset = new Vector2(6.0f, 0.35f),
                WindupAngle = 35f,
                ArcAngle = -80f,
                StrikeAngle = -165f,
                WindupDuration = 0.06f,
                StrikeDuration = 0.17f,
                ReturnDuration = 0.13f,
                HitTimeNormalized = 0.5f,
                Damage = 1,
                HitRadius = 1.5f
            },
            new MeleeComboStep
            {
                Name = "Slash_03",
                WindupOffset = new Vector2(-1.0f, 0.45f),
                ArcOffset = new Vector2(3.8f, 1.8f),
                StrikeOffset = new Vector2(7.2f, -0.25f),
                WindupAngle = -45f,
                ArcAngle = 95f,
                StrikeAngle = 210f,
                WindupDuration = 0.08f,
                StrikeDuration = 0.2f,
                ReturnDuration = 0.16f,
                HitTimeNormalized = 0.58f,
                Damage = 2,
                HitRadius = 1.8f
            }
        };
    }
}