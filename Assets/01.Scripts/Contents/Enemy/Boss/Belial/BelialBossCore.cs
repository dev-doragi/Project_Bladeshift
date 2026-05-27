using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class BelialBossCore : MonoBehaviour
{
    private enum BelialPatternType
    {
        ShootDynamic,
        SweepLeftLow,
        SweepRightMid,
        SweepLeftHigh,
        SweepRightLow
    }

    [Header("Parts")]
    [SerializeField] private BelialBossPart _head;
    [SerializeField] private BelialBossPart _leftHand;
    [SerializeField] private BelialBossPart _rightHand;

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
        BelialPatternType.SweepLeftLow,
        BelialPatternType.SweepRightMid,
        BelialPatternType.SweepLeftHigh,
        BelialPatternType.SweepRightLow
    };

    private Coroutine _battleRoutine;
    private bool _battleStarted;
    private bool _isBossGroggy;
    private bool _handsGroggyConsumed;
    private int _patternIndex;
    private Color _headDefaultColor = Color.white;

    private void Awake()
    {
        if (_head != null) _head.Bind(this);
        if (_leftHand != null) _leftHand.Bind(this);
        if (_rightHand != null) _rightHand.Bind(this);

        if (_headRenderer == null && _head != null)
            _headRenderer = _head.GetComponentInChildren<SpriteRenderer>();

        if (_headRenderer != null)
            _headDefaultColor = _headRenderer.color;
    }

    private void OnEnable()
    {
        _head?.SetBattleActive(false);
        _leftHand?.SetBattleActive(false);
        _rightHand?.SetBattleActive(false);

        if (_applyPreBattleVisualOnEnable && !_battleStarted)
            ApplyPreBattleVisualState();

        if (_autoStartPatternOnEnable)
            StartBattle();
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
        _leftHand?.CacheInitialPose();
        _rightHand?.CacheInitialPose();
        _eyeTracker?.CacheDefaults();
        ApplyBattleVisualState();

        _head?.SetAttackLocked(false);
        _leftHand?.SetAttackLocked(false);
        _rightHand?.SetAttackLocked(false);
        _head?.SetBattleActive(true);
        _leftHand?.SetBattleActive(true);
        _rightHand?.SetBattleActive(true);
        _leftHand?.SetContactDamageEnabled(false);
        _rightHand?.SetContactDamageEnabled(false);

        _head?.StartIdleBob();
        _leftHand?.StartIdleBob();
        _rightHand?.StartIdleBob();

        if (_battleRoutine != null)
            StopCoroutine(_battleRoutine);

        _battleRoutine = StartCoroutine(BattleRoutine());
    }

    public void NotifyHandDisabled(BelialBossPart hand)
    {
        if (!_battleStarted || _isBossGroggy || _handsGroggyConsumed)
            return;

        if (_leftHand == null || _rightHand == null)
            return;

        if (_leftHand.IsDisabledForBoss && _rightHand.IsDisabledForBoss)
            _handsGroggyConsumed = true;
    }

    private IEnumerator BattleRoutine()
    {
        if (_patternStartDelay > 0f)
            yield return new WaitForSeconds(_patternStartDelay);

        while (true)
        {
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
        if (_isBossGroggy || !_handsGroggyConsumed)
            return false;

        if (_leftHand == null || _rightHand == null)
            return false;

        return _leftHand.IsDisabledForBoss && _rightHand.IsDisabledForBoss;
    }

    private IEnumerator BossGroggyRoutine()
    {
        _isBossGroggy = true;

        _leftHand?.SetAttackLocked(true);
        _rightHand?.SetAttackLocked(true);
        _leftHand?.StopIdleBob();
        _rightHand?.StopIdleBob();
        _head?.StopIdleBob();

        _head?.SetVisualTint(_headGroggyColor);

        _eyeTracker?.SetGroggyEyes(true);
        _eyeTracker?.ForceEyeColorWhite();

        yield return new WaitForSeconds(_bossGroggyDuration);

        _head?.SetVisualTint(_headDefaultColor);

        _eyeTracker?.SetGroggyEyes(false);
        _eyeTracker?.ForceEyeColorWhite();

        _leftHand?.RestoreFromBossGroggy();
        _rightHand?.RestoreFromBossGroggy();

        _leftHand?.SetAttackLocked(false);
        _rightHand?.SetAttackLocked(false);

        _head?.StartIdleBob();
        _leftHand?.StartIdleBob();
        _rightHand?.StartIdleBob();

        _isBossGroggy = false;
        _handsGroggyConsumed = false;
    }

    private IEnumerator ExecutePattern(BelialPatternType pattern)
    {
        _leftHand?.SetContactDamageEnabled(false);
        _rightHand?.SetContactDamageEnabled(false);

        switch (pattern)
        {
            case BelialPatternType.ShootDynamic:
                yield return ShootDynamic();
                break;
            case BelialPatternType.SweepLeftLow:
                yield return Sweep(_leftHand, _rightHandAnchors, 0);
                break;
            case BelialPatternType.SweepRightMid:
                yield return Sweep(_rightHand, _leftHandAnchors, 1);
                break;
            case BelialPatternType.SweepLeftHigh:
                yield return Sweep(_leftHand, _rightHandAnchors, 2);
                break;
            case BelialPatternType.SweepRightLow:
                yield return Sweep(_rightHand, _leftHandAnchors, 0);
                break;
        }
    }

    private IEnumerator ShootDynamic()
    {
        Transform player = ResolvePlayerTarget();
        if (player == null)
            yield break;

        bool leftIsPrimary = IsLeftHandPrimaryByDistance(player.position);
        int primaryLayer = leftIsPrimary
            ? GetClosestAnchorLayerIndex(_leftHandAnchors, player.position)
            : GetClosestAnchorLayerIndex(_rightHandAnchors, player.position);

        int secondaryLayer = GetRandomRemainingLayer(primaryLayer);
        Transform leftAnchor = leftIsPrimary
            ? GetAnchor(_leftHandAnchors, primaryLayer)
            : GetAnchor(_leftHandAnchors, secondaryLayer);
        Transform rightAnchor = leftIsPrimary
            ? GetAnchor(_rightHandAnchors, secondaryLayer)
            : GetAnchor(_rightHandAnchors, primaryLayer);

        yield return MoveHandsToAnchors(leftAnchor, rightAnchor);
        yield return FireHands(player);
        yield return ReturnHandsToIdle();
    }

    private IEnumerator Sweep(BelialBossPart sweeper, Transform[] targetAnchors, int targetLayer)
    {
        if (sweeper == null || sweeper.IsDisabledForBoss)
            yield break;

        Transform[] sourceAnchors = sweeper.Role == BelialBossPartRole.LeftHand ? _leftHandAnchors : _rightHandAnchors;
        Transform sourceAnchor = GetAnchor(sourceAnchors, targetLayer);
        Transform targetAnchor = GetAnchor(targetAnchors, targetLayer);
        if (sourceAnchor == null || targetAnchor == null)
            yield break;

        sweeper.StopIdleBob();
        sweeper.SetAttackLocked(true);
        sweeper.SetContactDamage(_sweepContactDamage, _sweepContactDamageInterval);

        yield return sweeper.MoveToAnchor(sourceAnchor, _handMoveDuration);
        yield return sweeper.PlaySweepTelegraph(targetAnchor, _sweepDuration, _sweepTelegraphDelay);
        sweeper.SetContactDamageEnabled(true);
        yield return sweeper.SweepTo(targetAnchor, _sweepDuration);

        sweeper.SetContactDamageEnabled(false);
        sweeper.SetAttackLocked(false);

        yield return sweeper.ReturnToInitialPose(_handMoveDuration);
        sweeper.StartIdleBob();
    }

    private IEnumerator MoveHandsToAnchors(Transform leftAnchor, Transform rightAnchor)
    {
        bool leftDone = true;
        bool rightDone = true;

        if (_leftHand != null && !_leftHand.IsDisabledForBoss && leftAnchor != null)
        {
            _leftHand.StopIdleBob();
            _leftHand.SetAttackLocked(true);
            leftDone = false;
            StartCoroutine(RunAndMarkDone(_leftHand.MoveToAnchor(leftAnchor, _handMoveDuration), () => leftDone = true));
        }

        if (_rightHand != null && !_rightHand.IsDisabledForBoss && rightAnchor != null)
        {
            _rightHand.StopIdleBob();
            _rightHand.SetAttackLocked(true);
            rightDone = false;
            StartCoroutine(RunAndMarkDone(_rightHand.MoveToAnchor(rightAnchor, _handMoveDuration), () => rightDone = true));
        }

        while (!leftDone || !rightDone)
            yield return null;
    }

    private IEnumerator FireHands(Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning("[BelialBossCore] FireHands skipped: player target not found.", this);
            yield break;
        }

        bool leftDone = true;
        bool rightDone = true;

        if (_leftHand != null && !_leftHand.IsDisabledForBoss)
        {
            _leftHand.SetAttackLocked(false);
            leftDone = false;
            StartCoroutine(RunAndMarkDone(_leftHand.ChargeAndFire(player, _chargeDuration), () => leftDone = true));
        }

        if (_rightHand != null && !_rightHand.IsDisabledForBoss)
        {
            _rightHand.SetAttackLocked(false);
            rightDone = false;
            StartCoroutine(RunAndMarkDone(_rightHand.ChargeAndFire(player, _chargeDuration), () => rightDone = true));
        }

        while (!leftDone || !rightDone)
            yield return null;

        _leftHand?.SetAttackLocked(true);
        _rightHand?.SetAttackLocked(true);

        if (_afterFireDelay > 0f)
            yield return new WaitForSeconds(_afterFireDelay);
    }

    private IEnumerator ReturnHandsToIdle()
    {
        bool leftDone = true;
        bool rightDone = true;

        if (_leftHand != null && !_leftHand.IsDisabledForBoss)
        {
            leftDone = false;
            StartCoroutine(RunAndMarkDone(_leftHand.ReturnToInitialPose(_handMoveDuration), () =>
            {
                _leftHand.SetAttackLocked(false);
                _leftHand.StartIdleBob();
                leftDone = true;
            }));
        }

        if (_rightHand != null && !_rightHand.IsDisabledForBoss)
        {
            rightDone = false;
            StartCoroutine(RunAndMarkDone(_rightHand.ReturnToInitialPose(_handMoveDuration), () =>
            {
                _rightHand.SetAttackLocked(false);
                _rightHand.StartIdleBob();
                rightDone = true;
            }));
        }

        while (!leftDone || !rightDone)
            yield return null;
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
        _leftHand?.SetVisualAlpha(_handPreBattleAlpha);
        _rightHand?.SetVisualAlpha(_handPreBattleAlpha);

        if (_hideEyesBeforeBattle)
            _eyeTracker?.SetEyesVisible(false);
        _eyeTracker?.ForceEyeColorWhite();
    }

    private void ApplyBattleVisualState()
    {
        _head?.SetVisualTint(_headDefaultColor);
        _head?.SetVisualAlpha(1f);
        _leftHand?.SetVisualTint(Color.white);
        _rightHand?.SetVisualTint(Color.white);
        _leftHand?.SetVisualAlpha(1f);
        _rightHand?.SetVisualAlpha(1f);
        _eyeTracker?.SetEyesVisible(true);
        _eyeTracker?.ForceEyeColorWhite();
    }

    private Transform ResolvePlayerTarget()
    {
        if (PlayerController.ActivePlayer != null)
            return PlayerController.ActivePlayer.transform;

        PlayerController found = FindFirstObjectByType<PlayerController>();
        return found != null ? found.transform : null;
    }

    private bool IsLeftHandPrimaryByDistance(Vector3 playerPos)
    {
        if (_leftHand == null || _leftHand.IsDisabledForBoss)
            return false;
        if (_rightHand == null || _rightHand.IsDisabledForBoss)
            return true;

        float leftSqr = (_leftHand.transform.position - playerPos).sqrMagnitude;
        float rightSqr = (_rightHand.transform.position - playerPos).sqrMagnitude;
        return leftSqr <= rightSqr;
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
}
