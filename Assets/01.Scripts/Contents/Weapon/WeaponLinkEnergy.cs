using UnityEngine;

public class WeaponLinkEnergy : MonoBehaviour
{
    [Header("Link Energy")]
    [SerializeField] private float _maxEnergy = 100f;
    [SerializeField] private float _currentEnergy = 100f;
    [SerializeField] private float _recoverPerSecond = 25f;
    [SerializeField] private float _minDrainPerSecond = 4f;
    [SerializeField] private float _maxDrainPerSecond = 28f;
    [SerializeField, Range(0f, 1f)] private float _recoverRadiusRatio = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _reactivationEnergyRatio = 0.2f;
    [SerializeField] private LinkEnergyDepletionMode _depletionMode = LinkEnergyDepletionMode.DropGrounded;

    private const float Epsilon = 0.0001f;
    private bool _isRecoveryBlocked;
    private bool _spentEnergyThisFrame;

    public float MaxEnergy => _maxEnergy;
    public float CurrentEnergy => _currentEnergy;
    public float Normalized => _maxEnergy <= Epsilon ? 0f : Mathf.Clamp01(_currentEnergy / _maxEnergy);
    public float DistanceRatio { get; private set; }
    public bool IsRecovering { get; private set; }
    public bool IsDraining { get; private set; }
    public bool IsEmpty => _currentEnergy <= Epsilon;
    public bool IsControlLocked { get; private set; }
    public bool CanStartControl => !IsControlLocked && !IsEmpty && !_isRecoveryBlocked;
    public bool SpentEnergyThisFrame => _spentEnergyThisFrame;
    public LinkEnergyDepletionMode DepletionMode => _depletionMode;

    private void Awake()
    {
        _maxEnergy = Mathf.Max(Epsilon, _maxEnergy);
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);
        _recoverPerSecond = Mathf.Max(0f, _recoverPerSecond);
        _minDrainPerSecond = Mathf.Max(0f, _minDrainPerSecond);
        _maxDrainPerSecond = Mathf.Max(_minDrainPerSecond, _maxDrainPerSecond);
        _reactivationEnergyRatio = Mathf.Clamp01(_reactivationEnergyRatio);
        IsControlLocked = _currentEnergy <= Epsilon;
    }

    public void Tick(float distance, float controlRadius, bool isRemoteControlling, float deltaTime)
    {
        float safeDelta = Mathf.Max(0f, deltaTime);
        float safeControlRadius = Mathf.Max(Epsilon, controlRadius);
        float safeDistance = Mathf.Max(0f, distance);
        DistanceRatio = Mathf.Clamp01(safeDistance / safeControlRadius);

        float recoverRadius = safeControlRadius * Mathf.Clamp01(_recoverRadiusRatio);
        bool insideRecoverRadius = safeDistance <= recoverRadius;

        IsRecovering = !_isRecoveryBlocked && !_spentEnergyThisFrame && insideRecoverRadius;
        IsDraining = !_isRecoveryBlocked && !insideRecoverRadius && isRemoteControlling;

        float previousEnergy = _currentEnergy;

        if (IsRecovering)
        {
            _currentEnergy += _recoverPerSecond * safeDelta;
        }
        else if (IsDraining)
        {
            float drainPerSecond = Mathf.Lerp(_minDrainPerSecond, _maxDrainPerSecond, DistanceRatio);
            _currentEnergy -= drainPerSecond * safeDelta;
        }

        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);

        if (_currentEnergy <= Epsilon)
        {
            IsControlLocked = true;
        }
        else if (IsControlLocked && insideRecoverRadius && Normalized >= _reactivationEnergyRatio)
        {
            IsControlLocked = false;
        }

        if (!Mathf.Approximately(previousEnergy, _currentEnergy))
        {
            EventBus.Instance?.Publish(new LinkEnergyChangedEvent
            {
                Current = _currentEnergy,
                Max = _maxEnergy,
                Normalized = Normalized,
                DistanceRatio = DistanceRatio,
                IsRecovering = IsRecovering,
                IsDraining = IsDraining
            });
        }
    }

    public bool IsInsideRecoverRadius(float distance, float controlRadius)
    {
        float safeControlRadius = Mathf.Max(Epsilon, controlRadius);
        float recoverRadius = safeControlRadius * Mathf.Clamp01(_recoverRadiusRatio);
        return Mathf.Max(0f, distance) <= recoverRadius;
    }

    public bool CanSpendEnergy(float amount)
    {
        if (amount <= 0f) return true;
        if (IsControlLocked) return false;
        return _currentEnergy >= amount;
    }

    public bool TrySpendEnergy(float amount)
    {
        if (amount <= 0f) return true;
        if (!CanSpendEnergy(amount)) return false;

        _currentEnergy -= amount;
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);
        _spentEnergyThisFrame = true;

        EventBus.Instance?.Publish(new LinkEnergyChangedEvent
        {
            Current = _currentEnergy,
            Max = _maxEnergy,
            Normalized = Normalized,
            DistanceRatio = DistanceRatio,
            IsRecovering = IsRecovering,
            IsDraining = IsDraining
        });

        if (_currentEnergy <= Epsilon)
        {
            NotifyDepleted();
        }

        return true;
    }

    public bool SpendEnergyOverTime(float amountPerSecond)
    {
        return SpendEnergyOverTime(amountPerSecond, ContinuousEnergySpendMode.StopBeforeEmpty);
    }

    public bool SpendEnergyOverTime(float amountPerSecond, ContinuousEnergySpendMode spendMode)
    {
        if (amountPerSecond <= 0f) return true;
        if (IsControlLocked) return false;

        float deltaCost = Mathf.Max(0f, amountPerSecond * Time.fixedDeltaTime);
        if (deltaCost <= 0f) return true;

        if (_currentEnergy >= deltaCost)
        {
            _currentEnergy -= deltaCost;
            _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, _maxEnergy);
            _spentEnergyThisFrame = true;
            EventBus.Instance?.Publish(new LinkEnergyChangedEvent
            {
                Current = _currentEnergy,
                Max = _maxEnergy,
                Normalized = Normalized,
                DistanceRatio = DistanceRatio,
                IsRecovering = IsRecovering,
                IsDraining = IsDraining
            });
            if (_currentEnergy <= Epsilon)
            {
                NotifyDepleted();
            }
            return true;
        }

        if (spendMode == ContinuousEnergySpendMode.StopBeforeEmpty)
        {
            return false;
        }

        _currentEnergy = 0f;
        _spentEnergyThisFrame = true;
        NotifyDepleted();
        return false;
    }

    public void NotifyDepleted()
    {
        _currentEnergy = 0f;
        IsControlLocked = true;
        EventBus.Instance?.Publish(new LinkEnergyChangedEvent
        {
            Current = _currentEnergy,
            Max = _maxEnergy,
            Normalized = Normalized,
            DistanceRatio = DistanceRatio,
            IsRecovering = false,
            IsDraining = false
        });
    }

    public void BlockRecovery()
    {
        _isRecoveryBlocked = true;
        IsRecovering = false;
        IsDraining = false;
    }

    public void RestoreFullAndUnlock()
    {
        _currentEnergy = _maxEnergy;
        _isRecoveryBlocked = false;
        IsControlLocked = false;
        _spentEnergyThisFrame = false;
        IsRecovering = false;
        IsDraining = false;
        EventBus.Instance?.Publish(new LinkEnergyChangedEvent
        {
            Current = _currentEnergy,
            Max = _maxEnergy,
            Normalized = Normalized,
            DistanceRatio = DistanceRatio,
            IsRecovering = false,
            IsDraining = false
        });
    }

    public void ClearFrameSpendFlag()
    {
        _spentEnergyThisFrame = false;
    }
}
