using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] private UI_HpBar _hpBar;

    private PlayerController _playerController;
    private PlayerHealth _playerHealth;

    public void Bind(PlayerController playerController)
    {
        Unbind();

        if (playerController == null)
            return;

        _playerController = playerController;
        _playerHealth = _playerController.GetComponent<PlayerHealth>();

        if (_hpBar != null)
            _hpBar.Bind(_playerHealth);
    }

    public void Unbind()
    {
        if (_hpBar != null)
            _hpBar.Unbind();

        _playerController = null;
        _playerHealth = null;
    }

    public void Refresh()
    {
        if (_hpBar != null)
            _hpBar.Refresh();
    }
}