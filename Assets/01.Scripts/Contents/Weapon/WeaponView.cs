using UnityEngine;

public class WeaponView : MonoBehaviour
{
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private LineRenderer _connectionLine;
    [Header("Gizmo Display")]
    [SerializeField] private bool _showControlRadius = true;
    [SerializeField] private bool _showMouseCaptureRadius = true;
    [SerializeField] private bool _showMouseCaptureMaintainRadius = true;
    [SerializeField] private bool _showSlashRadius = true;
    [Header("Gizmo Values")]
    [SerializeField] private float _mouseCaptureRadius = 0f;
    [SerializeField] private float _mouseCaptureMaintainRadius = 0f;

    private Transform _playerTransform;
    private float _controlRadius;
    private float _slashRadius;

    public void Initialize(Transform playerTransform, float controlRadius, WeaponCombat combat)
    {
        _playerTransform = playerTransform;
        _controlRadius = controlRadius;
        if (combat != null) _slashRadius = combat.SlashRadius;
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
