using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public sealed class BelialBossCore : MonoBehaviour
{
    [System.Serializable]
    private sealed class HandRig
    {
        public string Label = "Hand";
        public BelialBossPart Hand;
        public Transform[] Anchors = new Transform[3];
        [System.NonSerialized] public float PatternStateElapsed;
    }

    private enum BelialPatternType
    {
        ShootDynamic,
        SweepLow,
        SweepMid,
        SweepHigh
    }

    [Header("Parts")]
    [SerializeField] private BelialBossPart _head;
    [SerializeField] private BelialBossPart _leftHand;
    [SerializeField] private BelialBossPart _rightHand;
    [SerializeField] private HandRig[] _additionalHands = new HandRig[0];

    [Header("Anchors")]
    [SerializeField] private Transform[] _leftHandAnchors = new Transform[3];
    [SerializeField] private Transform[] _rightHandAnchors = new Transform[3];

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _headRenderer;
    [SerializeField] private Color _headGroggyColor = Color.red;
    [SerializeField] private Color _headPreBattleColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField, Range(0f, 1f)] private float _handPreBattleAlpha = 0f;
    [SerializeField] private bool _hideEyesBeforeBattle = true;
    [SerializeField] private bool _applyPreBattleVisualOnEnable = true;
    [SerializeField] private BelialEyeTracker _eyeTracker;

    [Header("Timings")]
    [SerializeField, Min(0.1f)] private float _bossGroggyDuration = 5f;
    [SerializeField, Min(0f)] private float _patternStartDelay = 1f;
    [SerializeField, Min(0.05f)] private float _patternInterval = 0.8f;
    [SerializeField, Min(0.05f)] private float _handMoveDuration = 0.4f;
    [SerializeField, Min(0.05f)] private float _chargeDuration = 0.7f;
    [SerializeField, Min(0f)] private float _afterFireDelay = 0.2f;
    [SerializeField, Min(0.05f)] private float _sweepDuration = 0.5f;
    [SerializeField, Min(0.2f)] private float _handStuckRecoveryDelay = 6f;

    [Header("Sweep")]
    [SerializeField, Min(0)] private int _sweepContactDamage = 1;
    [SerializeField, Min(0.01f)] private float _sweepContactDamageInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float _sweepTelegraphDuration = 0.35f;
    [SerializeField, Min(0f)] private float _sweepTelegraphDelay = 0.12f;

    [Header("Control")]
    [SerializeField] private bool _autoStartPatternOnEnable;

    private readonly BelialPatternType[] _patternLoop =
    {
        BelialPatternType.ShootDynamic,
        BelialPatternType.ShootDynamic,
        BelialPatternType.ShootDynamic,
        BelialPatternType.ShootDynamic,
        BelialPatternType.ShootDynamic,
        BelialPatternType.SweepLow,
        BelialPatternType.SweepMid,
        BelialPatternType.SweepHigh
    };

    private Coroutine _battleRoutine;
    private bool _battleStarted;
    private bool _isBossGroggy;
    private bool _handsGroggyConsumed;
    private bool _isBossDefeated;
    private int _patternIndex;
    private Color _headDefaultColor = Color.white;
    private readonly List<HandRig> _handRigs = new List<HandRig>();

    private void Awake()
    {
        RebuildHandRigs();

        if (_head != null) _head.Bind(this);
        for (int i = 0; i < _handRigs.Count; i++)
            _handRigs[i].Hand?.Bind(this);

        if (_headRenderer == null && _head != null)
            _headRenderer = _head.GetComponentInChildren<SpriteRenderer>();

        if (_headRenderer != null)
            _headDefaultColor = _headRenderer.color;
    }

    private void OnEnable()
    {
        RebuildHandRigs();

        _head?.SetBattleActive(false);
        ForEachHand(h => h.SetBattleActive(false));

        if (_applyPreBattleVisualOnEnable && !_battleStarted)
            ApplyPreBattleVisualState();

        if (_autoStartPatternOnEnable)
            StartBattle();
    }

    private void Update()
    {
        if (!_battleStarted || _isBossDefeated || _isBossGroggy)
            return;

        for (int i = 0; i < _handRigs.Count; i++)
        {
            HandRig rig = _handRigs[i];
            if (rig == null)
                continue;
            rig.PatternStateElapsed = UpdateAndTryRecoverHand(rig.Hand, rig.PatternStateElapsed, rig.Label);
        }
    }

    public void StartBattle()
    {
        if (_battleStarted)
            return;

        _battleStarted = true;
        _patternIndex = 0;
        _isBossGroggy = false;
        _handsGroggyConsumed = false;

        _head?.CacheInitialPose();
        ForEachHand(h => h.CacheInitialPose());
        _eyeTracker?.CacheDefaults();
        ApplyBattleVisualState();

        _head?.SetAttackLocked(false);
        ForEachHand(h => h.SetAttackLocked(false));
        _head?.SetBattleActive(true);
        ForEachHand(h => h.SetBattleActive(true));
        ForEachHand(h => h.SetContactDamageEnabled(false));

        _head?.StartIdleBob();
        ForEachHand(h => h.StartIdleBob());

        if (_battleRoutine != null)
            StopCoroutine(_battleRoutine);

        _battleRoutine = StartCoroutine(BattleRoutine());
    }

    public void NotifyHandDisabled(BelialBossPart hand)
    {
        if (!_battleStarted || _isBossGroggy || _handsGroggyConsumed || _isBossDefeated)
            return;

        if (AreAllHandsDisabled())
            _handsGroggyConsumed = true;
    }

    public void NotifyHandGroggyChanged(BelialBossPart hand, bool isGroggy)
    {
        if (!_battleStarted || _isBossDefeated)
            return;

        if (!isGroggy)
            return;

        for (int i = 0; i < _handRigs.Count; i++)
            _handRigs[i].PatternStateElapsed = 0f;

        ForEachHand(h => h.CancelPatternAction());
        ForEachHand(h => h.SetContactDamageEnabled(false));
    }

    public bool CanHeadTakeDamage()
    {
        if (_isBossDefeated)
            return false;

        if (_handRigs.Count <= 0)
            return false;

        return AreAllHandsDisabled();
    }

    private IEnumerator BattleRoutine()
    {
        if (_patternStartDelay > 0f)
            yield return new WaitForSeconds(_patternStartDelay);

        while (true)
        {
            if (_isBossDefeated)
                yield break;

            if (ShouldEnterBossGroggy())
            {
                yield return BossGroggyRoutine();
                continue;
            }

            BelialPatternType pattern = _patternLoop[_patternIndex % _patternLoop.Length];
            _patternIndex++;

            yield return ExecutePattern(pattern);

            if (_patternInterval > 0f)
                yield return new WaitForSeconds(_patternInterval);
        }
    }

    private bool ShouldEnterBossGroggy()
    {
        if (_isBossDefeated)
            return false;

        if (_isBossGroggy || !_handsGroggyConsumed)
            return false;

        return AreAllHandsDisabled();
    }

    private IEnumerator BossGroggyRoutine()
    {
        if (_isBossDefeated)
            yield break;

        _isBossGroggy = true;

        ForEachHand(h => h.FreezeForBossGroggy());
        ForEachHand(h => h.StopIdleBob());
        _head?.StopIdleBob();

        _head?.SetVisualTint(_headGroggyColor);

        _eyeTracker?.SetGroggyEyes(true);
        _eyeTracker?.ForceEyeColorWhite();

        yield return new WaitForSeconds(_bossGroggyDuration);

        _head?.SetVisualTint(_headDefaultColor);

        _eyeTracker?.SetGroggyEyes(false);
        _eyeTracker?.ForceEyeColorWhite();

        ForEachHand(h => h.RestoreFromBossGroggy());

        _head?.StartIdleBob();
        ForEachHand(h => h.StartIdleBob());

        _isBossGroggy = false;
        _handsGroggyConsumed = false;
    }

    public void NotifyHeadDied()
    {
        if (_isBossDefeated)
            return;

        _isBossDefeated = true;
        _battleStarted = false;

        if (_battleRoutine != null)
        {
            StopCoroutine(_battleRoutine);
            _battleRoutine = null;
        }

        ForEachHand(h => h.SetContactDamageEnabled(false));
        ForEachHand(h => h.SetAttackLocked(true));
        ForEachHand(h =>
        {
            if (!h.IsDead)
                h.ExecuteDeath(Vector2.zero);
        });

        EventBus.Instance?.Publish(new BossHeadDefeatedEvent
        {
            StageIndex = 0,
            IsFinalStage = true
        });
    }

    private IEnumerator ExecutePattern(BelialPatternType pattern)
    {
        if (ShouldAbortPatternExecution())
            yield break;

        ForEachHand(h => h.SetContactDamageEnabled(false));

        switch (pattern)
        {
            case BelialPatternType.ShootDynamic:
                yield return ShootDynamic();
                break;
            case BelialPatternType.SweepLow:
                yield return SweepRandom(0);
                break;
            case BelialPatternType.SweepMid:
                yield return SweepRandom(1);
                break;
            case BelialPatternType.SweepHigh:
                yield return SweepRandom(2);
                break;
        }
    }

    private IEnumerator ShootDynamic()
    {
        if (ShouldAbortPatternExecution())
            yield break;

        Transform player = ResolvePlayerTarget();
        if (player == null)
            yield break;

        List<HandRig> active = GetPatternAvailableRigs();
        if (active.Count <= 0)
            yield break;

        Dictionary<BelialBossPart, Transform> targets = new Dictionary<BelialBossPart, Transform>();
        int reservedLayer = -1;
        int primaryIndex = GetClosestHandRigIndex(active, player.position);
        for (int i = 0; i < active.Count; i++)
        {
            HandRig rig = active[i];
            if (rig == null || rig.Hand == null)
                continue;

            int layer;
            if (i == primaryIndex)
            {
                layer = GetClosestAnchorLayerIndex(rig.Anchors, player.position);
                reservedLayer = layer;
            }
            else
            {
                layer = reservedLayer >= 0 ? GetRandomRemainingLayer(reservedLayer) : Random.Range(0, 3);
            }

            targets[rig.Hand] = GetAnchor(rig.Anchors, layer);
        }

        if (ShouldAbortPatternExecution())
            yield break;

        yield return MoveHandsToAnchors(targets);
        if (ShouldAbortPatternExecution())
            yield break;
        yield return FireHands(player);
        if (ShouldAbortPatternExecution())
            yield break;
        yield return ReturnHandsToIdle();
    }

    private IEnumerator SweepRandom(int targetLayer)
    {
        if (ShouldAbortPatternExecution())
            yield break;

        List<HandRig> active = GetPatternAvailableRigs();
        if (active.Count <= 1)
            yield break;

        int sweeperIndex = Random.Range(0, active.Count);
        HandRig sweeperRig = active[sweeperIndex];
        HandRig targetRig = active[(sweeperIndex + Random.Range(1, active.Count)) % active.Count];
        BelialBossPart sweeper = sweeperRig.Hand;
        if (sweeper == null || !sweeper.CanMoveForPattern)
            yield break;

        Transform[] sourceAnchors = sweeperRig.Anchors;
        Transform[] targetAnchors = targetRig.Anchors;
        Transform sourceAnchor = GetAnchor(sourceAnchors, targetLayer);
        Transform targetAnchor = GetAnchor(targetAnchors, targetLayer);
        if (sourceAnchor == null || targetAnchor == null)
            yield break;

        sweeper.StopIdleBob();
        sweeper.SetAttackLocked(true);
        sweeper.SetContactDamage(_sweepContactDamage, _sweepContactDamageInterval);

        yield return sweeper.MoveToAnchor(sourceAnchor, _handMoveDuration);
        if (ShouldAbortPatternExecution())
        {
            sweeper.CancelPatternAction();
            yield break;
        }
        yield return sweeper.PlaySweepTelegraph(targetAnchor, _sweepDuration, _sweepTelegraphDelay);
        if (ShouldAbortPatternExecution())
        {
            sweeper.CancelPatternAction();
            yield break;
        }
        sweeper.SetContactDamageEnabled(true);
        yield return sweeper.SweepTo(targetAnchor, _sweepDuration);
        if (ShouldAbortPatternExecution())
        {
            sweeper.CancelPatternAction();
            yield break;
        }

        sweeper.SetContactDamageEnabled(false);
        sweeper.SetAttackLocked(false);

        yield return sweeper.ReturnToInitialPose(_handMoveDuration);
        sweeper.StartIdleBob();
    }

    private IEnumerator MoveHandsToAnchors(Dictionary<BelialBossPart, Transform> handTargets)
    {
        if (ShouldAbortPatternExecution())
            yield break;

        int pending = 0;
        for (int i = 0; i < _handRigs.Count; i++)
        {
            HandRig rig = _handRigs[i];
            if (rig == null || rig.Hand == null || !rig.Hand.CanMoveForPattern)
                continue;

            if (!handTargets.TryGetValue(rig.Hand, out Transform anchor) || anchor == null)
                continue;

            rig.Hand.StopIdleBob();
            rig.Hand.SetAttackLocked(true);
            pending++;
            StartCoroutine(RunAndMarkDone(rig.Hand.MoveToAnchor(anchor, _handMoveDuration), () => pending--));
        }

        while (pending > 0)
        {
            if (ShouldAbortPatternExecution())
                yield break;
            yield return null;
        }
    }

    private IEnumerator FireHands(Transform player)
    {
        if (ShouldAbortPatternExecution())
            yield break;

        if (player == null)
        {
            Debug.LogWarning("[BelialBossCore] FireHands skipped: player target not found.", this);
            yield break;
        }

        int pending = 0;
        for (int i = 0; i < _handRigs.Count; i++)
        {
            HandRig rig = _handRigs[i];
            if (rig == null || rig.Hand == null || !rig.Hand.CanAttackInPattern)
                continue;

            rig.Hand.SetAttackLocked(false);
            pending++;
            StartCoroutine(RunAndMarkDone(rig.Hand.ChargeAndFire(player, _chargeDuration), () => pending--));
        }

        while (pending > 0)
        {
            if (ShouldAbortPatternExecution())
                yield break;
            yield return null;
        }

        ForEachHand(h => h.SetAttackLocked(true));

        if (_afterFireDelay > 0f)
            yield return new WaitForSeconds(_afterFireDelay);
    }

    private IEnumerator ReturnHandsToIdle()
    {
        if (ShouldAbortPatternExecution())
            yield break;

        int pending = 0;
        for (int i = 0; i < _handRigs.Count; i++)
        {
            HandRig rig = _handRigs[i];
            if (rig == null || rig.Hand == null || !rig.Hand.CanReturnFromPattern)
                continue;

            pending++;
            StartCoroutine(RunAndMarkDone(rig.Hand.ReturnToInitialPose(_handMoveDuration), () =>
            {
                rig.Hand.SetAttackLocked(false);
                rig.Hand.StartIdleBob();
                pending--;
            }));
        }

        while (pending > 0)
        {
            if (ShouldAbortPatternExecution())
                yield break;
            yield return null;
        }
    }

    private bool ShouldAbortPatternExecution()
    {
        bool bothHandsDisabled = AreAllHandsDisabled();

        return _isBossDefeated ||
               _isBossGroggy ||
               IsAnyHandGroggy() ||
               ShouldEnterBossGroggy() ||
               bothHandsDisabled;
    }

    private float UpdateAndTryRecoverHand(BelialBossPart hand, float elapsed, string handLabel)
    {
        if (hand == null || hand.IsDisabledForBoss || hand.IsDead)
            return 0f;

        if (!IsRecoverablePatternState(hand.CurrentPartState))
            return 0f;

        elapsed += Time.deltaTime;
        if (elapsed < _handStuckRecoveryDelay)
            return elapsed;

        Debug.LogWarning($"[BelialBossCore] {handLabel} hand stuck in {hand.CurrentPartState}. Force recovering to initial pose.", this);
        hand.ForceResetToInitialPose();
        return 0f;
    }

    private static bool IsRecoverablePatternState(BelialBossPartState state)
    {
        return state == BelialBossPartState.PatternMoving ||
               state == BelialBossPartState.Charging ||
               state == BelialBossPartState.Sweeping;
    }

    private bool IsAnyHandGroggy()
    {
        for (int i = 0; i < _handRigs.Count; i++)
        {
            BelialBossPart hand = _handRigs[i].Hand;
            if (hand != null && !hand.IsDisabledForBoss && hand.IsGroggy)
                return true;
        }
        return false;
    }

    private static Transform GetAnchor(Transform[] anchors, int index)
    {
        if (anchors == null || index < 0 || index >= anchors.Length)
            return null;

        return anchors[index];
    }

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }

    private void ApplyPreBattleVisualState()
    {
        _head?.SetVisualTint(_headPreBattleColor);
        _head?.SetVisualAlpha(_headPreBattleColor.a);
        ForEachHand(h => h.SetVisualAlpha(_handPreBattleAlpha));

        if (_hideEyesBeforeBattle)
            _eyeTracker?.SetEyesVisible(false);
        _eyeTracker?.ForceEyeColorWhite();
    }

    private void ApplyBattleVisualState()
    {
        _head?.SetVisualTint(_headDefaultColor);
        _head?.SetVisualAlpha(1f);
        ForEachHand(h =>
        {
            h.SetVisualTint(Color.white);
            h.SetVisualAlpha(1f);
        });
        _eyeTracker?.SetEyesVisible(true);
        _eyeTracker?.ForceEyeColorWhite();
    }

    private Transform ResolvePlayerTarget()
    {
        PlayerController found = FindFirstObjectByType<PlayerController>();
        return found != null ? found.transform : null;
    }

    private int GetClosestHandRigIndex(List<HandRig> rigs, Vector3 playerPos)
    {
        if (rigs == null || rigs.Count == 0)
            return -1;

        int bestIndex = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < rigs.Count; i++)
        {
            HandRig rig = rigs[i];
            if (rig == null || rig.Hand == null)
                continue;
            float sqr = (rig.Hand.transform.position - playerPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int GetClosestAnchorLayerIndex(Transform[] anchors, Vector3 playerPos)
    {
        if (anchors == null || anchors.Length == 0)
            return 1;

        int bestIndex = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < anchors.Length; i++)
        {
            Transform anchor = anchors[i];
            if (anchor == null)
                continue;

            float sqr = (anchor.position - playerPos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int GetRandomRemainingLayer(int usedLayer)
    {
        int[] candidates = { 0, 1, 2 };
        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == usedLayer)
                continue;
            candidates[count] = candidates[i];
            count++;
        }

        if (count <= 0)
            return Mathf.Clamp(usedLayer, 0, 2);

        int random = Random.Range(0, count);
        return candidates[random];
    }

    private IEnumerator RunAndMarkDone(IEnumerator routine, System.Action onDone)
    {
        if (routine != null)
            yield return StartCoroutine(routine);

        onDone?.Invoke();
    }

    private void RebuildHandRigs()
    {
        _handRigs.Clear();
        AddHandRig("Left", _leftHand, _leftHandAnchors);
        AddHandRig("Right", _rightHand, _rightHandAnchors);

        if (_additionalHands == null)
            return;

        for (int i = 0; i < _additionalHands.Length; i++)
        {
            HandRig extra = _additionalHands[i];
            if (extra == null || extra.Hand == null)
                continue;
            AddHandRig(string.IsNullOrWhiteSpace(extra.Label) ? $"Extra{i}" : extra.Label, extra.Hand, extra.Anchors);
        }
    }

    private void AddHandRig(string label, BelialBossPart hand, Transform[] anchors)
    {
        if (hand == null)
            return;

        for (int i = 0; i < _handRigs.Count; i++)
        {
            if (_handRigs[i].Hand == hand)
                return;
        }

        _handRigs.Add(new HandRig
        {
            Label = label,
            Hand = hand,
            Anchors = anchors ?? new Transform[3]
        });
    }

    private List<HandRig> GetPatternAvailableRigs()
    {
        List<HandRig> available = new List<HandRig>();
        for (int i = 0; i < _handRigs.Count; i++)
        {
            HandRig rig = _handRigs[i];
            if (rig?.Hand != null && rig.Hand.CanMoveForPattern)
                available.Add(rig);
        }
        return available;
    }

    private bool AreAllHandsDisabled()
    {
        if (_handRigs.Count == 0)
            return false;

        for (int i = 0; i < _handRigs.Count; i++)
        {
            BelialBossPart hand = _handRigs[i].Hand;
            if (hand != null && !hand.IsDisabledForBoss)
                return false;
        }

        return true;
    }

    private void ForEachHand(System.Action<BelialBossPart> action)
    {
        if (action == null)
            return;

        for (int i = 0; i < _handRigs.Count; i++)
        {
            BelialBossPart hand = _handRigs[i].Hand;
            if (hand != null)
                action(hand);
        }
    }
}


