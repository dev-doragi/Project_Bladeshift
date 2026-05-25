using DG.Tweening;
using UnityEngine;

public class UI_HpBar : MonoBehaviour
{
    [SerializeField] private RectTransform _bar;

    [Header("Heart Prefabs")]
    [SerializeField] private GameObject _filledHeartPrefab;
    [SerializeField] private GameObject _emptyHeartPrefab;

    [Header("Animation")]
    [SerializeField] private float _punchScaleMultiplier = 1.2f;
    [SerializeField] private float _punchDuration = 0.08f;
    [SerializeField] private float _shrinkDuration = 0.12f;
    [SerializeField] private float _growDuration = 0.18f;

    private PlayerHealth _playerHealth;
    private HeartSlot[] _slots;
    private int _currentHp;
    private int _maxHp;

    private void OnEnable()
    {
        EventBus.Instance.Subscribe<PlayerHpChangedEvent>(HandleHpChanged);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
            return;

        EventBus.Instance.Unsubscribe<PlayerHpChangedEvent>(HandleHpChanged);
    }

    public void Bind(PlayerHealth playerHealth)
    {
        _playerHealth = playerHealth;
        Refresh();
    }

    public void Unbind()
    {
        _playerHealth = null;
        ClearSlots();
    }

    public void Refresh()
    {
        if (_playerHealth == null)
        {
            ClearSlots();
            return;
        }

        ApplyHp(_playerHealth.CurrentHp, _playerHealth.MaxHp, true);
    }

    private void HandleHpChanged(PlayerHpChangedEvent evt)
    {
        ApplyHp(evt.CurrentHp, evt.MaxHp, false);
    }

    private void ApplyHp(int currentHp, int maxHp, bool immediate)
    {
        int nextMaxHp = Mathf.Max(1, maxHp);
        int nextCurrentHp = Mathf.Clamp(currentHp, 0, nextMaxHp);

        if (_slots == null || _maxHp != nextMaxHp)
        {
            CreateSlots(nextMaxHp);

            _maxHp = nextMaxHp;
            _currentHp = nextCurrentHp;

            RefreshImmediate();
            return;
        }

        if (immediate)
        {
            _currentHp = nextCurrentHp;
            RefreshImmediate();
            return;
        }

        int prevHp = _currentHp;
        _currentHp = nextCurrentHp;

        if (_currentHp < prevHp)
        {
            for (int i = prevHp - 1; i >= _currentHp; i--)
            {
                PlayLoseHeart(i);
            }

            return;
        }

        if (_currentHp > prevHp)
        {
            for (int i = prevHp; i < _currentHp; i++)
            {
                PlayGainHeart(i);
            }

            return;
        }

        RefreshImmediate();
    }

    private void CreateSlots(int count)
    {
        ClearSlots();

        _slots = new HeartSlot[count];

        for (int i = 0; i < count; i++)
        {
            GameObject filledObject = Instantiate(_filledHeartPrefab, _bar);
            GameObject emptyObject = Instantiate(_emptyHeartPrefab, _bar);

            filledObject.name = $"Heart_Filled_{i}";
            emptyObject.name = $"Heart_Empty_{i}";

            emptyObject.SetActive(false);

            Transform filledTransform = filledObject.transform;
            Transform emptyTransform = emptyObject.transform;

            _slots[i] = new HeartSlot(
                filledObject,
                emptyObject,
                filledTransform,
                emptyTransform,
                filledTransform.localScale,
                emptyTransform.localScale);
        }
    }

    private void ClearSlots()
    {
        if (_bar == null)
            return;

        foreach (Transform child in _bar)
        {
            child.DOKill();
            Destroy(child.gameObject);
        }

        _slots = null;
        _currentHp = 0;
        _maxHp = 0;
    }

    private void RefreshImmediate()
    {
        if (_slots == null)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            bool isFilled = i < _currentHp;
            SetHeartStateImmediate(i, isFilled);
        }
    }

    private void SetHeartStateImmediate(int index, bool isFilled)
    {
        if (index < 0 || index >= _slots.Length)
            return;

        HeartSlot slot = _slots[index];

        slot.FilledTransform.DOKill();
        slot.EmptyTransform.DOKill();

        slot.FilledTransform.localScale = slot.FilledBaseScale;
        slot.EmptyTransform.localScale = slot.EmptyBaseScale;

        slot.FilledObject.SetActive(isFilled);
        slot.EmptyObject.SetActive(!isFilled);
    }

    private void PlayLoseHeart(int index)
    {
        if (index < 0 || index >= _slots.Length)
            return;

        HeartSlot slot = _slots[index];

        slot.FilledTransform.DOKill();
        slot.EmptyTransform.DOKill();

        slot.FilledObject.SetActive(true);
        slot.EmptyObject.SetActive(false);
        slot.FilledTransform.localScale = slot.FilledBaseScale;

        Vector3 punchScale = slot.FilledBaseScale * _punchScaleMultiplier;

        slot.FilledTransform.DOScale(punchScale, _punchDuration)
            .OnComplete(() =>
            {
                slot.FilledTransform.DOScale(Vector3.zero, _shrinkDuration)
                    .OnComplete(() =>
                    {
                        slot.FilledObject.SetActive(false);
                        slot.EmptyObject.SetActive(true);
                        slot.EmptyTransform.localScale = Vector3.zero;
                        slot.EmptyTransform.DOScale(slot.EmptyBaseScale, _growDuration).SetEase(Ease.OutBack);
                    });
            });
    }

    private void PlayGainHeart(int index)
    {
        if (index < 0 || index >= _slots.Length)
            return;

        HeartSlot slot = _slots[index];

        slot.FilledTransform.DOKill();
        slot.EmptyTransform.DOKill();

        slot.EmptyObject.SetActive(true);
        slot.FilledObject.SetActive(false);
        slot.EmptyTransform.localScale = slot.EmptyBaseScale;

        slot.EmptyTransform.DOScale(Vector3.zero, _shrinkDuration)
            .OnComplete(() =>
            {
                slot.EmptyObject.SetActive(false);
                slot.FilledObject.SetActive(true);
                slot.FilledTransform.localScale = Vector3.zero;
                slot.FilledTransform.DOScale(slot.FilledBaseScale, _growDuration).SetEase(Ease.OutBack);
            });
    }

    private sealed class HeartSlot
    {
        public GameObject FilledObject { get; }
        public GameObject EmptyObject { get; }
        public Transform FilledTransform { get; }
        public Transform EmptyTransform { get; }
        public Vector3 FilledBaseScale { get; }
        public Vector3 EmptyBaseScale { get; }

        public HeartSlot(
            GameObject filledObject,
            GameObject emptyObject,
            Transform filledTransform,
            Transform emptyTransform,
            Vector3 filledBaseScale,
            Vector3 emptyBaseScale)
        {
            FilledObject = filledObject;
            EmptyObject = emptyObject;
            FilledTransform = filledTransform;
            EmptyTransform = emptyTransform;
            FilledBaseScale = filledBaseScale;
            EmptyBaseScale = emptyBaseScale;
        }
    }
}