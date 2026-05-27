using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class BelialBossCore : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private BelialBossPart _head;
    [SerializeField] private BelialBossPart _leftHand;
    [SerializeField] private BelialBossPart _rightHand;

    [Header("Head Motion")]
    [SerializeField] private Transform _headMotionRoot;
    [SerializeField, Min(0f)] private float _headDropDistance = 3f;
    [SerializeField, Min(0.01f)] private float _headDropDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _headRiseDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float _headVulnerableDuration = 5f;

    [Header("Clear")]
    [SerializeField] private bool _publishStageClearOnDeath;
    [SerializeField] private int _stageIndex;

    private Vector3 _headRaisedLocalPosition;
    private Vector3 _headDroppedLocalPosition;
    private Tween _headTween;
    private Coroutine _headExposeRoutine;
    private bool _isHeadExposed;
    private bool _isBossDead;

    public bool IsHeadExposed => _isHeadExposed;
    public bool IsBossDead => _isBossDead;

    private void Awake()
    {
        RegisterPart(_head);
        RegisterPart(_leftHand);
        RegisterPart(_rightHand);

        if (_headMotionRoot == null && _head != null)
            _headMotionRoot = _head.transform;

        if (_headMotionRoot != null)
        {
            _headRaisedLocalPosition = _headMotionRoot.localPosition;
            _headDroppedLocalPosition = _headRaisedLocalPosition + Vector3.down * _headDropDistance;
        }

        SetHeadExposed(false, true);
    }

    public void RegisterPart(BelialBossPart part)
    {
        if (part == null)
            return;

        part.Bind(this);

        switch (part.Role)
        {
            case BelialBossPartRole.Head:
                if (_head == null) _head = part;
                break;
            case BelialBossPartRole.LeftHand:
                if (_leftHand == null) _leftHand = part;
                break;
            case BelialBossPartRole.RightHand:
                if (_rightHand == null) _rightHand = part;
                break;
        }
    }

    public void NotifyHandDisabled(BelialBossPart hand)
    {
        if (_isBossDead || _isHeadExposed)
            return;

        if (_leftHand == null || _rightHand == null)
            return;

        if (_leftHand.IsTemporarilyDisabled && _rightHand.IsTemporarilyDisabled)
            OpenHeadPhase();
    }

    public void NotifyHandRecovered(BelialBossPart hand)
    {
    }

    public void NotifyHeadDestroyed(BelialBossPart head)
    {
        if (_isBossDead)
            return;

        _isBossDead = true;

        if (_headExposeRoutine != null)
        {
            StopCoroutine(_headExposeRoutine);
            _headExposeRoutine = null;
        }

        _headTween?.Kill();
        _leftHand?.SetExternalAttackLock(true);
        _rightHand?.SetExternalAttackLock(true);

        EventBus.Instance?.Publish(new CameraShakeEvent { Intensity = ShakeIntensity.Strong });

        if (_publishStageClearOnDeath)
        {
            EventBus.Instance?.Publish(new StageClearedEvent
            {
                StageIndex = _stageIndex,
                IsFinalStage = false
            });
        }
    }

    private void OpenHeadPhase()
    {
        SetHeadExposed(true, false);

        if (_headExposeRoutine != null)
            StopCoroutine(_headExposeRoutine);

        _headExposeRoutine = StartCoroutine(HeadExposeRoutine());
    }

    private IEnumerator HeadExposeRoutine()
    {
        yield return new WaitForSeconds(_headVulnerableDuration);

        if (!_isBossDead)
            SetHeadExposed(false, false);

        _headExposeRoutine = null;
    }

    private void SetHeadExposed(bool exposed, bool immediate)
    {
        _isHeadExposed = exposed;
        _head?.SetHeadVulnerable(exposed);

        if (_headMotionRoot == null)
            return;

        Vector3 target = exposed ? _headDroppedLocalPosition : _headRaisedLocalPosition;
        float duration = exposed ? _headDropDuration : _headRiseDuration;

        _headTween?.Kill();

        if (immediate)
        {
            _headMotionRoot.localPosition = target;
            return;
        }

        _headTween = _headMotionRoot
            .DOLocalMove(target, duration)
            .SetEase(exposed ? Ease.OutBack : Ease.InQuad);

        EventBus.Instance?.Publish(new CameraShakeEvent
        {
            Intensity = exposed ? ShakeIntensity.Medium : ShakeIntensity.Weak
        });
    }

    private void OnDestroy()
    {
        _headTween?.Kill();
    }
}
