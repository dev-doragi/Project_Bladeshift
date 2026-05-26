using UnityEngine;

public class PlayerAimGuideView : MonoBehaviour
{
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private RectTransform _guideRoot;
    [SerializeField] private float _visibleThreshold = 0.15f;
    [SerializeField] private float _rotationOffset = 0f;
    [SerializeField] private bool _showOnlyWhenGamepadLookActive = false;

    private void LateUpdate()
    {
        if (_playerController == null || _guideRoot == null)
            return;

        Vector2 aimDirection = _playerController.AimDirection;
        float threshold = Mathf.Max(0f, _visibleThreshold);
        bool hasAimDirection = aimDirection.sqrMagnitude >= threshold * threshold;
        bool isGamepadLookActive = !_showOnlyWhenGamepadLookActive ||
                                   (InputReader.Instance != null && InputReader.Instance.IsGamepadLookActive());
        bool visible = hasAimDirection && isGamepadLookActive;

        if (_guideRoot.gameObject.activeSelf != visible)
            _guideRoot.gameObject.SetActive(visible);

        if (!visible)
            return;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        _guideRoot.rotation = Quaternion.Euler(0f, 0f, angle + _rotationOffset);
    }
}
