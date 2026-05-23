using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private Transform _meleeMirrorRoot;

    private PlayerController _controller;
    private Vector3 _initialMeleeMirrorScale;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();

        if (_meleeMirrorRoot != null)
            _initialMeleeMirrorScale = _meleeMirrorRoot.localScale;
    }

    private void LateUpdate()
    {
        UpdateVisualDirection();
    }

    private void UpdateVisualDirection()
    {
        bool isFacingLeft = _controller != null && _controller.IsFacingLeft;

        if (_bodyRenderer != null)
            _bodyRenderer.flipX = isFacingLeft;

        UpdateMeleeMirror(isFacingLeft);
    }

    private void UpdateMeleeMirror(bool isFacingLeft)
    {
        if (_meleeMirrorRoot == null)
            return;

        Vector3 nextScale = _initialMeleeMirrorScale;
        nextScale.x = isFacingLeft
            ? -Mathf.Abs(_initialMeleeMirrorScale.x)
            : Mathf.Abs(_initialMeleeMirrorScale.x);

        _meleeMirrorRoot.localScale = nextScale;
    }
}