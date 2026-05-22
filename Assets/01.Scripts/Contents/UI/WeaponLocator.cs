using UnityEngine;

public class WeaponLocator : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Transform _weaponTarget;
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private RectTransform _icon;
    [SerializeField] private float _edgePadding = 64f;
    [SerializeField] private float _visibleViewportMargin = 0.02f;
    [SerializeField] private float _rotationOffset = -90f;

    private const float Epsilon = 0.0001f;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void Update()
    {
        if (!HasRequiredReferences())
        {
            SetIconVisible(false);
            return;
        }

        Vector3 viewport = _targetCamera.WorldToViewportPoint(_weaponTarget.position);
        if (IsInsideViewport(viewport))
        {
            SetIconVisible(false);
            return;
        }

        Vector2 viewportOffset = new Vector2(
            viewport.x - 0.5f,
            viewport.y - 0.5f
        );

        Vector2 localTargetOffset = new Vector2(
            viewportOffset.x * _canvasRect.rect.width,
            viewportOffset.y * _canvasRect.rect.height
        );

        if (viewport.z < 0f)
        {
            localTargetOffset *= -1f;
        }

        if (localTargetOffset.sqrMagnitude <= Epsilon * Epsilon)
        {
            SetIconVisible(false);
            return;
        }

        float halfWidth = _canvasRect.rect.width * 0.5f - _edgePadding;
        float halfHeight = _canvasRect.rect.height * 0.5f - _edgePadding;
        halfWidth = Mathf.Max(0f, halfWidth);
        halfHeight = Mathf.Max(0f, halfHeight);

        Vector2 dir = localTargetOffset.normalized;

        float scaleX = Mathf.Abs(dir.x) > Epsilon
            ? halfWidth / Mathf.Abs(dir.x)
            : float.PositiveInfinity;
        float scaleY = Mathf.Abs(dir.y) > Epsilon
            ? halfHeight / Mathf.Abs(dir.y)
            : float.PositiveInfinity;

        float scale = Mathf.Min(scaleX, scaleY);
        Vector2 edgePosition = dir * scale;

        _icon.anchoredPosition = edgePosition;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _icon.localRotation = Quaternion.Euler(0f, 0f, angle + _rotationOffset);

        SetIconVisible(true);
    }

    private bool HasRequiredReferences()
    {
        return _targetCamera != null
            && _weaponTarget != null
            && _canvasRect != null
            && _icon != null;
    }

    private bool IsInsideViewport(Vector3 viewport)
    {
        float min = 0f + _visibleViewportMargin;
        float max = 1f - _visibleViewportMargin;
        return viewport.z > 0f
            && viewport.x >= min
            && viewport.x <= max
            && viewport.y >= min
            && viewport.y <= max;
    }

    private void SetIconVisible(bool visible)
    {
        if (_icon == null) return;
        if (_icon.gameObject.activeSelf == visible) return;
        _icon.gameObject.SetActive(visible);
    }

    private void AutoAssignReferences()
    {
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        if (_canvasRect == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                _canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
        }

        if (_icon == null)
        {
            Transform iconChild = transform.Find("Icon");
            if (iconChild != null)
            {
                _icon = iconChild as RectTransform;
            }

            if (_icon == null)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    RectTransform childRect = transform.GetChild(i) as RectTransform;
                    if (childRect != null)
                    {
                        _icon = childRect;
                        break;
                    }
                }
            }
        }
    }
}
