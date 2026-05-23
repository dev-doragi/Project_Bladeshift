using UnityEngine;

public class WeaponView : MonoBehaviour
{
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private LineRenderer _connectionLine;
    [SerializeField] private Color _recoverColor = Color.cyan;
    [SerializeField] private Color _stableColor = Color.white;
    [SerializeField] private Color _drainColor = Color.yellow;
    [SerializeField] private Color _criticalColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float _criticalEnergyRatio = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _blinkEnergyRatio = 0.1f;
    [SerializeField] private float _blinkInterval = 0.12f;
    [Header("Gizmo Display")]
    [SerializeField] private bool _showControlRadius = true;
    [SerializeField] private bool _showMouseCaptureRadius = true;
    [SerializeField] private bool _showMouseCaptureMaintainRadius = true;
    [SerializeField] private bool _showSlashRadius = true;
    [Header("Gizmo Values")]
    [SerializeField] private float _mouseCaptureRadius = 0f;
    [SerializeField] private float _mouseCaptureMaintainRadius = 0f;

    private Transform _playerTransform;
    private WeaponStateMachine _stateMachine;
    private WeaponModeController _modeController;
    private WeaponLinkEnergy _linkEnergy;
    private float _controlRadius;
    private float _slashRadius;

    public void Initialize(Transform playerTransform, float controlRadius, WeaponCombat combat)
    {
        _playerTransform = playerTransform;
        _controlRadius = controlRadius;
        if (combat != null) _slashRadius = combat.SlashRadius;
    }

    public void Initialize(
        Transform playerTransform,
        WeaponStateMachine stateMachine,
        WeaponModeController modeController,
        WeaponLinkEnergy linkEnergy)
    {
        _playerTransform = playerTransform;
        _stateMachine = stateMachine;
        _modeController = modeController;
        _linkEnergy = linkEnergy;
    }

    private void LateUpdate()
    {
        RefreshConnectionLine();
    }

    public void Configure(Transform playerTransform, float controlRadius, float mouseCaptureRadius, float mouseCaptureMaintainRadius, float slashRadius)
    {
        _mouseCaptureRadius = mouseCaptureRadius;
        _mouseCaptureMaintainRadius = mouseCaptureMaintainRadius;
        _slashRadius = slashRadius;
        Initialize(playerTransform, controlRadius, null);
    }

    public void SetRotationZ(float zAngle)
    {
        Vector3 euler = transform.eulerAngles;
        euler.z = zAngle;
        transform.eulerAngles = euler;
    }

    public void ShowTrajectory(Vector3 start, Vector3 end)
    {
        if (_trajectoryLine == null) return;

        Vector3 direction = end - start;
        float maxLength = Mathf.Max(0f, _controlRadius);
        Vector3 clampedEnd = start + Vector3.ClampMagnitude(direction, maxLength);

        _trajectoryLine.enabled = true;
        _trajectoryLine.positionCount = 2;
        _trajectoryLine.SetPosition(0, start);
        _trajectoryLine.SetPosition(1, clampedEnd);
    }

    public void HideTrajectory()
    {
        if (_trajectoryLine != null)
        {
            _trajectoryLine.enabled = false;
        }
    }

    public void RenderConnectionLine(Vector3 playerPos, Vector3 weaponPos, bool isVisible, Color color)
    {
        if (_connectionLine == null) return;

        if (!isVisible)
        {
            _connectionLine.enabled = false;
            return;
        }

        _connectionLine.enabled = true;
        _connectionLine.startColor = color;
        _connectionLine.endColor = color;
        _connectionLine.positionCount = 2;
        _connectionLine.SetPosition(0, playerPos);
        _connectionLine.SetPosition(1, weaponPos);
    }

    private void RefreshConnectionLine()
    {
        if (!ShouldShowConnectionLine())
        {
            SetConnectionLineVisible(false);
            return;
        }

        if (_connectionLine == null || _playerTransform == null)
        {
            SetConnectionLineVisible(false);
            return;
        }

        _connectionLine.enabled = true;
        _connectionLine.positionCount = 2;
        _connectionLine.SetPosition(0, _playerTransform.position);
        _connectionLine.SetPosition(1, transform.position);

        Color color = GetConnectionColor();
        _connectionLine.startColor = color;
        _connectionLine.endColor = color;
    }

    private bool ShouldShowConnectionLine()
    {
        if (_connectionLine == null || _playerTransform == null || _stateMachine == null || _modeController == null)
            return false;

        if (_modeController.CurrentMode == WeaponMode.Melee)
            return false;

        WeaponState currentState = _stateMachine.CurrentState;
        if (currentState == WeaponState.Grounded)
            return false;

        bool isLinkedState = currentState == WeaponState.Controlled
            || currentState == WeaponState.Slashing
            || currentState == WeaponState.PinningFlight
            || currentState == WeaponState.Pinned
            || currentState == WeaponState.Returning;

        if (!isLinkedState)
            return false;

        if (_linkEnergy != null && _linkEnergy.IsEmpty)
            return false;

        if (IsBlinkingNow())
            return false;

        return true;
    }

    private bool IsBlinkingNow()
    {
        if (_linkEnergy == null)
            return false;

        float normalized = _linkEnergy.Normalized;
        if (normalized > _blinkEnergyRatio)
            return false;

        if (!_linkEnergy.IsDraining)
            return false;

        if (_blinkInterval <= 0f)
            return false;

        return Mathf.FloorToInt(Time.unscaledTime / _blinkInterval) % 2 == 0;
    }

    private Color GetConnectionColor()
    {
        if (_linkEnergy == null)
            return _stableColor;

        float normalized = _linkEnergy.Normalized;

        if (_linkEnergy.IsEmpty || normalized <= _blinkEnergyRatio)
            return _criticalColor;

        if (normalized <= _criticalEnergyRatio)
            return Color.Lerp(_drainColor, _criticalColor, Mathf.InverseLerp(_blinkEnergyRatio, _criticalEnergyRatio, normalized));

        if (_linkEnergy.IsDraining)
            return _drainColor;

        if (_linkEnergy.IsRecovering)
            return _recoverColor;

        return _stableColor;
    }

    private void SetConnectionLineVisible(bool visible)
    {
        if (_connectionLine == null)
            return;

        _connectionLine.enabled = visible;
    }

    public void DrawGizmos()
    {
        if (_playerTransform == null) return;

        if (_showControlRadius)
        {
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(_playerTransform.position, _controlRadius);
        }

        if (_showMouseCaptureRadius && _mouseCaptureRadius > 0f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _mouseCaptureRadius);
        }

        if (_showMouseCaptureMaintainRadius && _mouseCaptureMaintainRadius > 0f)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _mouseCaptureMaintainRadius);
        }

        if (_showSlashRadius && _slashRadius > 0f)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_slashRadius, _slashRadius * 0.57f, 0.01f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
