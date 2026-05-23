using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private Transform _weaponVisualRoot;

    private PlayerController _controller;
    private Vector3 _gunVisualRootScale;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();

        if (_weaponVisualRoot != null)
            _gunVisualRootScale = _weaponVisualRoot.localScale;
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

        if (_weaponVisualRoot != null)
        {
            Vector3 nextScale = _gunVisualRootScale;
            nextScale.y = isFacingLeft ? -Mathf.Abs(_gunVisualRootScale.y) : Mathf.Abs(_gunVisualRootScale.y);
            _weaponVisualRoot.localScale = nextScale;
        }
    }
}
